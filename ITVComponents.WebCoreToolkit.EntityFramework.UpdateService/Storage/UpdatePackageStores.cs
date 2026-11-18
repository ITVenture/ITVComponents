using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Storage
{
    /// <summary>
    /// What a package store reports about a package it has written.
    /// </summary>
    public sealed class StoredPackage
    {
        public StoredPackage(string sha256, long size)
        {
            Sha256 = sha256;
            Size = size;
        }

        /// <summary>Gets the SHA-256 (hex).</summary>
        public string Sha256 { get; }

        /// <summary>Gets the size in bytes.</summary>
        public long Size { get; }
    }

    /// <summary>
    /// Where packages lie. Always streamed - a package of 100 MB must never end up in memory as a whole, not when ten shops
    /// download it at the same time.
    /// </summary>
    public interface IUpdatePackageStore
    {
        /// <summary>
        /// Writes a package and computes its hash and size on the way.
        /// </summary>
        Task<StoredPackage> SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken);

        /// <summary>
        /// Opens a package for reading. The stream is seekable and knows its length (HTTP range requests).
        /// </summary>
        /// <returns>the stream, or null if the package does not exist</returns>
        Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a package; a missing one is fine.
        /// </summary>
        Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Packages as files on a volume. Simple and cheap; the volume must survive a restart of the container and be shared
    /// by all instances.
    /// </summary>
    public sealed class FileUpdatePackageStore : IUpdatePackageStore
    {
        private readonly string root;

        public FileUpdatePackageStore(string root)
        {
            this.root = Path.GetFullPath(root);
            Directory.CreateDirectory(this.root);
        }

        /// <inheritdoc />
        public async Task<StoredPackage> SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken)
        {
            var target = PathOf(storageKey);
            var partial = target + ".partial";
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size = 0;
            try
            {
                await using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        hash.AppendData(buffer, 0, read);
                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        size += read;
                    }
                }

                File.Move(partial, target, true);
            }
            catch
            {
                TryDelete(partial);
                throw;
            }

            return new StoredPackage(Convert.ToHexString(hash.GetHashAndReset()), size);
        }

        /// <inheritdoc />
        public Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken)
        {
            var path = PathOf(storageKey);
            return Task.FromResult<Stream?>(File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true) : null);
        }

        /// <inheritdoc />
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            TryDelete(PathOf(storageKey));
            return Task.CompletedTask;
        }

        private string PathOf(string storageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey) || storageKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || storageKey.Contains(".."))
            {
                throw new ArgumentException($"'{storageKey}' is no valid storage key.", nameof(storageKey));
            }

            return Path.Combine(root, storageKey);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The package file {path} could not be deleted: {ex.OutlineException()}", LogSeverity.Error);
            }
        }
    }

    /// <summary>
    /// Packages in the database, in blocks of <see cref="ChunkSize"/>. Needs no volume; every package grows every backup,
    /// so old releases should be cleaned up (<see cref="Administration.IUpdateReleaseAdministration.CleanupAsync"/>).
    /// </summary>
    /// <remarks>
    /// Written block by block, each block saved and forgotten by its own context; read block by block on demand - a range
    /// request reads only the blocks it touches.
    /// </remarks>
    public sealed class DatabaseUpdatePackageStore<TContext> : IUpdatePackageStore
        where TContext : DbContext, IUpdateServiceContext
    {
        /// <summary>The size of one block: 1 MB.</summary>
        public const int ChunkSize = 1024 * 1024;

        private readonly IDbContextFactory<TContext> contextFactory;

        public DatabaseUpdatePackageStore(IDbContextFactory<TContext> contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        /// <inheritdoc />
        public async Task<StoredPackage> SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken)
        {
            await DeleteAsync(storageKey, cancellationToken).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size = 0;
            var index = 0;
            var buffer = new byte[ChunkSize];
            try
            {
                while (true)
                {
                    var filled = 0;
                    int read;
                    while (filled < ChunkSize && (read = await content.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        filled += read;
                    }

                    if (filled == 0)
                    {
                        break;
                    }

                    hash.AppendData(buffer, 0, filled);
                    size += filled;
                    await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
                    {
                        db.UpdatePackageChunks.Add(new UpdatePackageChunk { StorageKey = storageKey, ChunkIndex = index++, Data = buffer.AsSpan(0, filled).ToArray() });
                        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (filled < ChunkSize)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogEnvironment.LogEvent($"Writing package {storageKey} into the database failed after {size} bytes; the blocks are removed: {ex.OutlineException()}", LogSeverity.Error);
                await DeleteAsync(storageKey, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            return new StoredPackage(Convert.ToHexString(hash.GetHashAndReset()), size);
        }

        /// <inheritdoc />
        public async Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var chunks = await db.UpdatePackageChunks.AsNoTracking().Where(c => c.StorageKey == storageKey)
                .Select(c => new { c.ChunkIndex, Length = c.Data.Length })
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            if (chunks.Count == 0)
            {
                return null;
            }

            return new ChunkReadStream(contextFactory, storageKey, chunks.Sum(c => (long)c.Length));
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            // Stand-ins carrying only the key: the blocks are deleted without loading their data.
            var indexes = await db.UpdatePackageChunks.AsNoTracking().Where(c => c.StorageKey == storageKey)
                .Select(c => c.ChunkIndex).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (indexes.Count == 0)
            {
                return;
            }

            db.UpdatePackageChunks.RemoveRange(indexes.Select(i => new UpdatePackageChunk { StorageKey = storageKey, ChunkIndex = i }));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a package block by block; only the current block is in memory.
        /// </summary>
        private sealed class ChunkReadStream : Stream
        {
            private readonly IDbContextFactory<TContext> contextFactory;
            private readonly string storageKey;
            private readonly long length;
            private long position;
            private int loadedIndex = -1;
            private byte[] loaded = Array.Empty<byte>();

            public ChunkReadStream(IDbContextFactory<TContext> contextFactory, string storageKey, long length)
            {
                this.contextFactory = contextFactory;
                this.storageKey = storageKey;
                this.length = length;
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => length;

            public override long Position
            {
                get => position;
                set => position = Math.Clamp(value, 0, length);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer.AsMemory(offset, count)).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (position >= length || buffer.Length == 0)
                {
                    return 0;
                }

                var index = (int)(position / ChunkSize);
                if (index != loadedIndex)
                {
                    await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                    loaded = await db.UpdatePackageChunks.AsNoTracking()
                        .Where(c => c.StorageKey == storageKey && c.ChunkIndex == index)
                        .Select(c => c.Data)
                        .SingleAsync(cancellationToken).ConfigureAwait(false);
                    loadedIndex = index;
                }

                var inChunk = (int)(position % ChunkSize);
                var count = Math.Min(buffer.Length, loaded.Length - inChunk);
                loaded.AsMemory(inChunk, count).CopyTo(buffer);
                position += count;
                return count;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                Position = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => position + offset,
                    _ => length + offset
                };
                return position;
            }

            public override void Flush()
            {
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
