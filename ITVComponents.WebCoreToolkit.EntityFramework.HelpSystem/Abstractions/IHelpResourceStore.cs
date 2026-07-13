using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Abstractions
{
    /// <summary>
    /// Plain-DI storage abstraction for help media resources. Deliberately independent of the toolkit plugin
    /// file-handler machinery so the anonymous public viewer can read resources without spinning up the
    /// (security-gated) plugin factory. The admin upload writes through <see cref="SaveAsync"/>; the anonymous
    /// resolver endpoint reads through <see cref="OpenAsync"/>. Both hit the same backend, so a host that
    /// overrides one must override the other. A built-in EF-blob reference implementation ships with the toolkit.
    /// </summary>
    public interface IHelpResourceStore
    {
        /// <summary>Persists the bytes and returns an opaque file identifier to store on the resource-file row.</summary>
        Task<string> SaveAsync(byte[] content, string? contentType, string? downloadName, CancellationToken cancellationToken = default);

        /// <summary>Opens a previously-saved resource by its identifier, or null if it no longer exists.</summary>
        Task<HelpResourceContent?> OpenAsync(string fileIdentifier, CancellationToken cancellationToken = default);

        /// <summary>Removes a previously-saved resource (best-effort; no error if already gone).</summary>
        Task DeleteAsync(string fileIdentifier, CancellationToken cancellationToken = default);
    }

    /// <summary>A resource stream plus its metadata, as returned by <see cref="IHelpResourceStore.OpenAsync"/>.</summary>
    public sealed class HelpResourceContent : IDisposable
    {
        public Stream Content { get; init; } = Stream.Null;

        public string? ContentType { get; init; }

        public string? DownloadName { get; init; }

        public void Dispose() => Content?.Dispose();
    }
}
