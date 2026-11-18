using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Feed
{
    /// <summary>
    /// A manifest as delivered, with its ETag.
    /// </summary>
    public sealed class BuiltFeed
    {
        public BuiltFeed(byte[] manifest, string entityTag)
        {
            Manifest = manifest;
            EntityTag = entityTag;
        }

        /// <summary>Gets the manifest, byte for byte as delivered (and signed).</summary>
        public byte[] Manifest { get; }

        /// <summary>Gets the ETag (quoted), derived from the content: a regular check without news costs almost nothing.</summary>
        public string EntityTag { get; }
    }

    /// <summary>
    /// Builds the manifest of a product and channel from the releases (P2 of the update service plan): the newest
    /// release of the application that is not withdrawn - withdrawing shows the former one again - and every release of
    /// its modules that is not withdrawn, so that each update service picks the one that fits.
    /// </summary>
    public sealed class UpdateFeedBuilder<TContext>
        where TContext : DbContext, IUpdateServiceContext
    {
        private readonly IDbContextFactory<TContext> contextFactory;

        public UpdateFeedBuilder(IDbContextFactory<TContext> contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        /// <summary>
        /// Builds the manifest.
        /// </summary>
        /// <returns>the manifest, or null if the product does not exist or is a module</returns>
        public async Task<BuiltFeed?> BuildAsync(string productKey, string channel, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var product = await db.UpdateProducts.AsNoTracking()
                .SingleOrDefaultAsync(p => p.ProductKey == productKey && p.Kind == PackageKinds.Base, cancellationToken).ConfigureAwait(false);
            if (product == null)
            {
                return null;
            }

            var modules = await db.UpdateProducts.AsNoTracking().Where(p => p.BaseProductKey == productKey && p.Kind == PackageKinds.Module)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var productIds = modules.Select(m => m.UpdateProductId).Append(product.UpdateProductId).ToList();
            var releases = await db.UpdateReleases.AsNoTracking()
                .Where(r => productIds.Contains(r.UpdateProductId) && r.Channel == channel && !r.Withdrawn)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var feed = new UpdateFeed { Channel = channel };
            var latestBase = releases.Where(r => r.UpdateProductId == product.UpdateProductId)
                .OrderByDescending(r => ParseVersion(r.Version)).FirstOrDefault();
            if (latestBase != null)
            {
                feed.Packages.Add(ToPackage(product, latestBase, null));
            }

            foreach (var module in modules.OrderBy(m => m.ProductKey, StringComparer.Ordinal))
            {
                foreach (var release in releases.Where(r => r.UpdateProductId == module.UpdateProductId).OrderBy(r => ParseVersion(r.Version)))
                {
                    feed.Packages.Add(ToPackage(module, release, productKey));
                }
            }

            // Stable bytes for the same releases: the time of the manifest is the newest publication, not "now".
            feed.Generated = releases.Count == 0 ? null : new DateTimeOffset(DateTime.SpecifyKind(releases.Max(r => r.PublishedUtc), DateTimeKind.Utc));
            var manifest = JsonSerializer.SerializeToUtf8Bytes(feed, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            var tag = "\"" + Convert.ToHexString(SHA256.HashData(manifest))[..32] + "\"";
            return new BuiltFeed(manifest, tag);
        }

        /// <summary>
        /// Finds the release of a package that is offered in a product's feed.
        /// </summary>
        /// <returns>the release, or null if it is not offered there (unknown, withdrawn, other product)</returns>
        public async Task<UpdateRelease?> FindOfferedReleaseAsync(string productKey, int releaseId, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var release = await db.UpdateReleases.AsNoTracking().SingleOrDefaultAsync(r => r.UpdateReleaseId == releaseId && !r.Withdrawn, cancellationToken).ConfigureAwait(false);
            if (release == null)
            {
                return null;
            }

            var product = await db.UpdateProducts.AsNoTracking().SingleOrDefaultAsync(p => p.UpdateProductId == release.UpdateProductId, cancellationToken).ConfigureAwait(false);
            return product != null && (product.ProductKey == productKey || product.BaseProductKey == productKey) ? release : null;
        }

        /// <summary>
        /// The address of a package, relative to the manifest: the whole folder can move to another server.
        /// </summary>
        public static string PackageUrl(UpdateRelease release) => $"packages/{release.UpdateReleaseId}/{Uri.EscapeDataString(release.FileName)}";

        private static UpdatePackage ToPackage(UpdateProduct product, UpdateRelease release, string? baseProductKey)
        {
            return new UpdatePackage
            {
                Id = product.ProductKey,
                Kind = product.Kind,
                Version = release.Version,
                Url = PackageUrl(release),
                Sha256 = release.Sha256,
                Size = release.Size,
                Published = new DateTimeOffset(DateTime.SpecifyKind(release.PublishedUtc, DateTimeKind.Utc)),
                Urgent = release.Urgent,
                MinimumVersion = release.MinimumVersion,
                Requires = baseProductKey != null && !string.IsNullOrWhiteSpace(release.Requires)
                    ? new Dictionary<string, string> { { baseProductKey, release.Requires } }
                    : null
            };
        }

        internal static Version ParseVersion(string text)
        {
            var core = (text ?? string.Empty).Split('-', '+')[0].Trim();
            return Version.TryParse(core.Contains('.') ? core : core + ".0", out var v)
                ? new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0))
                : new Version(0, 0, 0, 0);
        }
    }
}
