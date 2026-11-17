using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.UpdateService.Abstractions.Feed;

namespace ITVComponents.UpdateService.Feed
{
    /// <summary>
    /// Reads the manifest of a channel.
    /// </summary>
    public interface IUpdateFeedClient
    {
        /// <summary>
        /// Gets the manifest, or null when it did not change since the last call.
        /// </summary>
        Task<FeedResult?> GetFeedAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// A manifest, and where it came from (relative package addresses are resolved against it).
    /// </summary>
    public sealed class FeedResult
    {
        public FeedResult(UpdateFeed feed, Uri manifestUri)
        {
            Feed = feed;
            ManifestUri = manifestUri;
        }

        /// <summary>Gets the manifest.</summary>
        public UpdateFeed Feed { get; }

        /// <summary>Gets the address of the manifest.</summary>
        public Uri ManifestUri { get; }

        /// <summary>
        /// Resolves the address of a package. Only HTTPS is accepted, unless the feed itself may be plain HTTP.
        /// </summary>
        /// <exception cref="InvalidOperationException">when the address is missing or not allowed</exception>
        public Uri ResolvePackageUri(UpdatePackage package, bool allowInsecure)
        {
            if (string.IsNullOrWhiteSpace(package.Url))
            {
                throw new InvalidOperationException($"Package {package.Id} {package.Version} has no address.");
            }

            var resolved = new Uri(ManifestUri, package.Url);
            UpdateFeedClient.EnsureSecure(resolved, allowInsecure);
            return resolved;
        }
    }

    /// <summary>
    /// Reads the manifest over HTTPS, with <c>ETag</c>, so that a check without news costs almost nothing.
    /// </summary>
    public sealed class UpdateFeedClient : IUpdateFeedClient
    {
        private readonly HttpClient http;
        private readonly Uri manifestUri;
        private readonly bool allowInsecure;
        private EntityTagHeaderValue? lastTag;

        public UpdateFeedClient(HttpClient http, UpdateServiceOptions options)
        {
            this.http = http;
            allowInsecure = options.AllowInsecureFeed;
            if (!Uri.TryCreate(options.FeedUrl.TrimEnd('/') + "/", UriKind.Absolute, out var feedFolder))
            {
                throw new InvalidOperationException($"The feed address '{options.FeedUrl}' is not an absolute address.");
            }

            manifestUri = new Uri(feedFolder, $"{options.Channel}.json");
            EnsureSecure(manifestUri, allowInsecure);
        }

        /// <inheritdoc />
        public async Task<FeedResult?> GetFeedAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUri);
            if (lastTag != null)
            {
                request.Headers.IfNoneMatch.Add(lastTag);
            }

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var feed = Parse(json);
            lastTag = response.Headers.ETag;
            return new FeedResult(feed, manifestUri);
        }

        /// <summary>
        /// Reads a manifest. Packages without id, version or hash are an error of the feed, not something to skip.
        /// </summary>
        /// <exception cref="InvalidOperationException">when the manifest is not usable</exception>
        public static UpdateFeed Parse(string json)
        {
            var feed = JsonSerializer.Deserialize<UpdateFeed>(json)
                       ?? throw new InvalidOperationException("The manifest is empty.");
            var broken = feed.Packages.FirstOrDefault(p => string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Version)
                                                         || string.IsNullOrWhiteSpace(p.Sha256) || string.IsNullOrWhiteSpace(p.Url)
                                                         || !VersionText.TryParse(p.Version, out _));
            if (broken != null)
            {
                throw new InvalidOperationException($"The manifest lists a package without id, valid version, address or SHA-256 ({broken.Id ?? "no id"} {broken.Version ?? "no version"}).");
            }

            return feed;
        }

        internal static void EnsureSecure(Uri uri, bool allowInsecure)
        {
            if (uri.Scheme == Uri.UriSchemeHttps || (allowInsecure && uri.Scheme == Uri.UriSchemeHttp))
            {
                return;
            }

            throw new InvalidOperationException($"The address {uri} is not HTTPS; the update service only loads over HTTPS.");
        }
    }

    /// <summary>
    /// Versions as the feed and Windows Installer write them (<c>1.4.2</c>; a suffix like <c>-beta</c> is ignored).
    /// </summary>
    public static class VersionText
    {
        public static bool TryParse(string? text, out Version version)
        {
            version = new Version(0, 0);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var core = text.Split('-', '+')[0].Trim();
            if (!Version.TryParse(core.Contains('.') ? core : core + ".0", out var parsed))
            {
                return false;
            }

            version = parsed;
            return true;
        }

        /// <summary>
        /// Compares two versions, missing parts counting as 0 (<c>1.4</c> equals <c>1.4.0</c>).
        /// </summary>
        public static int Compare(Version a, Version b)
        {
            return Normalize(a).CompareTo(Normalize(b));
        }

        private static Version Normalize(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        }
    }
}
