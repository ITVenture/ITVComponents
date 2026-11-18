using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Feed;

namespace ITVComponents.UpdateService.Packages
{
    /// <summary>
    /// Fetches a package into the service's protected folder.
    /// </summary>
    public interface IPackageDownloader
    {
        /// <summary>
        /// Downloads the package unless it is there already with the right hash, and checks size and SHA-256.
        /// </summary>
        /// <returns>the path of the checked file</returns>
        /// <exception cref="InvalidDataException">when size or hash do not match; the file is deleted</exception>
        Task<string> DownloadAsync(UpdatePackage package, Uri source, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Downloads into a file next to the target and moves it only once size and hash match. The hash protects against
    /// transmission errors; the security lies in the signature check after it.
    /// </summary>
    public sealed class PackageDownloader : IPackageDownloader
    {
        private readonly HttpClient http;
        private readonly string directory;

        public PackageDownloader(HttpClient http, string directory)
        {
            this.http = http;
            this.directory = directory;
        }

        /// <inheritdoc />
        public async Task<string> DownloadAsync(UpdatePackage package, Uri source, CancellationToken cancellationToken)
        {
            var target = Path.Combine(directory, FileNameOf(package, source));
            if (File.Exists(target))
            {
                if (await MatchesAsync(target, package, cancellationToken).ConfigureAwait(false))
                {
                    LogEnvironment.LogDebugEvent($"Package {package.Id} {package.Version} is already downloaded.", LogSeverity.Report);
                    return target;
                }

                LogEnvironment.LogEvent($"The downloaded {target} does not match the manifest any more; it is fetched again.", LogSeverity.Warning);
                File.Delete(target);
            }

            var partial = target + ".partial";
            var meta = partial + ".meta";
            await FetchAsync(package, source, partial, meta, cancellationToken).ConfigureAwait(false);

            if (!await MatchesAsync(partial, package, cancellationToken).ConfigureAwait(false))
            {
                Discard(partial, meta);
                throw new InvalidDataException($"Package {package.Id} {package.Version} from {source} does not match the size or the SHA-256 of the manifest.");
            }

            File.Move(partial, target, true);
            DeleteMeta(meta);
            LogEnvironment.LogEvent($"Package {package.Id} {package.Version} downloaded and its hash checked.", LogSeverity.Report);
            return target;
        }

        /// <summary>
        /// Fetches the package into <paramref name="partial"/>. A part left by an interrupted download (weak line, restart)
        /// is continued with an HTTP range, provided it belongs to the same package (hash in <paramref name="meta"/>). The
        /// range is conditional on the entity tag of the first response: a server that has another file by now answers
        /// with the whole file, which then replaces the part. Whatever arrives, the hash check afterwards decides.
        /// </summary>
        private async Task FetchAsync(UpdatePackage package, Uri source, string partial, string meta, CancellationToken cancellationToken)
        {
            var (offset, entityTag) = Resumable(package, partial, meta);
            if (offset > 0 && package.Size > 0 && offset >= package.Size)
            {
                // Complete already (interrupted between download and check); the check decides.
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            if (offset > 0)
            {
                request.Headers.Range = new RangeHeaderValue(offset, null);
                if (entityTag != null)
                {
                    request.Headers.IfRange = new RangeConditionHeaderValue(entityTag);
                }

                LogEnvironment.LogEvent($"Continuing the download of {package.Id} {package.Version} at byte {offset}.", LogSeverity.Report);
            }

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (offset > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                // The part is not a prefix of what the server has (any more): start over in the next pass.
                LogEnvironment.LogEvent($"The server refused to continue the download of {package.Id} {package.Version} at byte {offset}; the part is discarded.", LogSeverity.Warning);
                Discard(partial, meta);
                throw new InvalidDataException($"The download of package {package.Id} {package.Version} could not be continued; it starts over.");
            }

            response.EnsureSuccessStatusCode();
            var append = offset > 0 && response.StatusCode == HttpStatusCode.PartialContent;
            if (append && response.Content.Headers.ContentRange?.From is { } from && from != offset)
            {
                LogEnvironment.LogEvent($"The server continued the download of {package.Id} {package.Version} at byte {from} instead of {offset}; the part is discarded.", LogSeverity.Warning);
                Discard(partial, meta);
                throw new InvalidDataException($"The download of package {package.Id} {package.Version} could not be continued; it starts over.");
            }

            if (offset > 0 && !append)
            {
                LogEnvironment.LogEvent($"The server sent {package.Id} {package.Version} as a whole instead of from byte {offset}; the part is replaced.", LogSeverity.Report);
            }

            await File.WriteAllLinesAsync(meta, new[] { package.Sha256?.Trim() ?? string.Empty, response.Headers.ETag?.ToString() ?? string.Empty }, cancellationToken).ConfigureAwait(false);
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var file = new FileStream(partial, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);
            // Interrupted here, the part stays for the next pass; the exception reaches the coordinator.
            await body.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// How much of the package is there from an earlier attempt, and the entity tag it came with.
        /// </summary>
        private static (long Offset, EntityTagHeaderValue? EntityTag) Resumable(UpdatePackage package, string partial, string meta)
        {
            if (!File.Exists(partial))
            {
                DeleteMeta(meta);
                return (0, null);
            }

            string[] lines;
            try
            {
                lines = File.Exists(meta) ? File.ReadAllLines(meta) : Array.Empty<string>();
            }
            catch (IOException ex)
            {
                LogEnvironment.LogEvent($"The notes of the partial download {partial} are not readable; it starts over: {ex.Message}", LogSeverity.Warning);
                lines = Array.Empty<string>();
            }

            if (lines.Length == 0 || !string.Equals(lines[0], package.Sha256?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                LogEnvironment.LogEvent($"The partial download {partial} belongs to another package; it starts over.", LogSeverity.Report);
                Discard(partial, meta);
                return (0, null);
            }

            EntityTagHeaderValue? tag = null;
            if (lines.Length > 1 && !string.IsNullOrWhiteSpace(lines[1]) && !EntityTagHeaderValue.TryParse(lines[1], out tag))
            {
                LogEnvironment.LogEvent($"The entity tag '{lines[1]}' of the partial download {partial} is not readable; the range is requested without it.", LogSeverity.Warning);
            }

            return (new FileInfo(partial).Length, tag);
        }

        private static void Discard(string partial, string meta)
        {
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }

            DeleteMeta(meta);
        }

        private static void DeleteMeta(string meta)
        {
            if (File.Exists(meta))
            {
                File.Delete(meta);
            }
        }

        /// <summary>
        /// Indicates whether a file has the size and the SHA-256 of the manifest.
        /// </summary>
        public static async Task<bool> MatchesAsync(string path, UpdatePackage package, CancellationToken cancellationToken)
        {
            var info = new FileInfo(path);
            if (package.Size > 0 && info.Length != package.Size)
            {
                LogEnvironment.LogEvent($"{path} has {info.Length} bytes, the manifest says {package.Size}.", LogSeverity.Warning);
                return false;
            }

            await using var stream = info.OpenRead();
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(hash, package.Sha256?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                LogEnvironment.LogEvent($"{path} has the SHA-256 {hash}, the manifest says {package.Sha256}.", LogSeverity.Warning);
                return false;
            }

            return true;
        }

        private static string FileNameOf(UpdatePackage package, Uri source)
        {
            var extension = Path.GetExtension(source.AbsolutePath);
            var name = $"{package.Id}-{package.Version}{extension}";
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }
    }
}
