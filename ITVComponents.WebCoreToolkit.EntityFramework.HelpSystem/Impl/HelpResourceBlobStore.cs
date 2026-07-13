using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Impl
{
    /// <summary>
    /// Built-in reference <see cref="IHelpResourceStore"/> that keeps resource bytes in the
    /// <see cref="HelpResourceBlob"/> table of the host help context. Uses a per-operation context from the
    /// factory (Blazor-safe). Hosts with dedicated media storage register their own implementation instead.
    /// </summary>
    public class HelpResourceBlobStore<TContext> : IHelpResourceStore
        where TContext : DbContext, IHelpSystemContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;

        public HelpResourceBlobStore(IDbContextFactory<TContext> dbFactory)
        {
            this.dbFactory = dbFactory;
        }

        public async Task<string> SaveAsync(byte[] content, string? contentType, string? downloadName, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid().ToString("N");
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.HelpResourceBlobs.Add(new HelpResourceBlob
            {
                FileIdentifier = id,
                ContentType = contentType,
                DownloadName = downloadName,
                Content = content,
                Created = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            return id;
        }

        public async Task<HelpResourceContent?> OpenAsync(string fileIdentifier, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var blob = await db.HelpResourceBlobs.AsNoTracking()
                .FirstOrDefaultAsync(b => b.FileIdentifier == fileIdentifier, cancellationToken);
            if (blob == null)
            {
                return null;
            }

            return new HelpResourceContent
            {
                Content = new MemoryStream(blob.Content, writable: false),
                ContentType = blob.ContentType,
                DownloadName = blob.DownloadName
            };
        }

        public async Task DeleteAsync(string fileIdentifier, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var blob = await db.HelpResourceBlobs
                .FirstOrDefaultAsync(b => b.FileIdentifier == fileIdentifier, cancellationToken);
            if (blob != null)
            {
                db.HelpResourceBlobs.Remove(blob);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
