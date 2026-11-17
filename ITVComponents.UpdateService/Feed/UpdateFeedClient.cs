using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ITVComponents.Logging;
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
    /// Reads the manifest over HTTPS, with <c>ETag</c>, so that a check without news costs almost nothing - and checks its
    /// signature (<c>&lt;channel&gt;.json.sig</c>).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="UpdateServiceOptions.RequireFeedSignature"/>: a manifest without valid signature is refused.</item>
    /// <item>Not required, but trusted signers configured: a signature that is there is checked; a wrong one refuses the
    /// manifest - a signature that does not fit is a sign of tampering, not a missing feature.</item>
    /// <item>Neither: the signature is not looked at.</item>
    /// </list>
    /// </remarks>
    public sealed class UpdateFeedClient : IUpdateFeedClient
    {
        private readonly HttpClient http;
        private readonly Uri manifestUri;
        private readonly Uri signatureUri;
        private readonly bool allowInsecure;
        private readonly bool requireSignature;
        private readonly string[] trustedSigners;
        private EntityTagHeaderValue? lastTag;

        public UpdateFeedClient(HttpClient http, UpdateServiceOptions options)
        {
            this.http = http;
            allowInsecure = options.AllowInsecureFeed;
            requireSignature = options.RequireFeedSignature;
            trustedSigners = options.FeedSigningThumbprints.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
            if (requireSignature && trustedSigners.Length == 0)
            {
                throw new InvalidOperationException("RequireFeedSignature needs the thumbprints of the feed signers (FeedSigningThumbprints).");
            }

            if (!Uri.TryCreate(options.FeedUrl.TrimEnd('/') + "/", UriKind.Absolute, out var feedFolder))
            {
                throw new InvalidOperationException($"The feed address '{options.FeedUrl}' is not an absolute address.");
            }

            manifestUri = new Uri(feedFolder, $"{options.Channel}.json");
            signatureUri = new Uri(feedFolder, $"{options.Channel}.json.sig");
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
            var manifest = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (requireSignature || trustedSigners.Length != 0)
            {
                await CheckSignatureAsync(manifest, cancellationToken).ConfigureAwait(false);
            }

            var feed = Parse(Encoding.UTF8.GetString(StripByteOrderMark(manifest)));
            lastTag = response.Headers.ETag;
            return new FeedResult(feed, manifestUri);
        }

        private async Task CheckSignatureAsync(byte[] manifest, CancellationToken cancellationToken)
        {
            using var response = await http.GetAsync(signatureUri, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if (requireSignature)
                {
                    throw new InvalidOperationException($"The manifest is not signed ({signatureUri} is missing), and a signature is required.");
                }

                LogEnvironment.LogDebugEvent($"The manifest is not signed ({signatureUri} is missing); a signature is not required.", LogSeverity.Report);
                return;
            }

            response.EnsureSuccessStatusCode();
            var signature = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!FeedSignature.Verify(manifest, signature, trustedSigners, out var reason))
            {
                throw new InvalidOperationException($"The signature of the manifest is refused: {reason}");
            }

            LogEnvironment.LogDebugEvent("The signature of the manifest is valid.", LogSeverity.Report);
        }

        private static byte[] StripByteOrderMark(byte[] bytes)
        {
            return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? bytes[3..] : bytes;
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
