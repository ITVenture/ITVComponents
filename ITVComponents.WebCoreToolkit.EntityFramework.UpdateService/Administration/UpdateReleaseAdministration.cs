using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Storage;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Administration
{
    /// <summary>
    /// A release to publish.
    /// </summary>
    public sealed class ReleaseUpload
    {
        public string ProductKey { get; set; } = string.Empty;

        public string Channel { get; set; } = "stable";

        /// <summary>The version - entered, not read from the package: on a Linux server the version inside an MSI is not
        /// at hand. The update service checks it against the package and refuses a mismatch.</summary>
        public string Version { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public bool Urgent { get; set; }

        public string? MinimumVersion { get; set; }

        /// <summary>For a module: the versions of the application it fits (NuGet range).</summary>
        public string? Requires { get; set; }

        /// <summary>When it counts as published; default: now.</summary>
        public DateTime? PublishedUtc { get; set; }

        public string? UploadedBy { get; set; }
    }

    /// <summary>
    /// A module assigned somewhere that has no release for a version of the application - those client apps would keep
    /// the old version (M1).
    /// </summary>
    public sealed class MissingModuleWarning
    {
        public string ModuleProductKey { get; set; } = string.Empty;

        /// <summary>The client apps that have the module assigned (directly or through their template).</summary>
        public List<AffectedClientApp> ClientApps { get; set; } = new List<AffectedClientApp>();
    }

    /// <summary>
    /// A client app concerned by a warning.
    /// </summary>
    public sealed class AffectedClientApp
    {
        public int TenantId { get; set; }

        public int ClientAppId { get; set; }

        public string ClientName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Maintains products and releases (platform-wide). The caller checks the permission; the masks require it.
    /// </summary>
    public interface IUpdateReleaseAdministration
    {
        /// <summary>
        /// The products, applications first.
        /// </summary>
        Task<IReadOnlyList<UpdateProduct>> GetProductsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// The releases of a product in all channels, newest first.
        /// </summary>
        Task<IReadOnlyList<UpdateRelease>> GetReleasesAsync(string productKey, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a product, or returns the existing one with that key.
        /// </summary>
        Task<UpdateProduct> EnsureProductAsync(string productKey, string kind, string displayName, string? baseProductKey, CancellationToken cancellationToken);

        /// <summary>
        /// Publishes a release: the package goes into the store (hash and size computed on the way), then the release row.
        /// </summary>
        Task<UpdateRelease> UploadReleaseAsync(ReleaseUpload upload, Stream content, CancellationToken cancellationToken);

        /// <summary>
        /// Withdraws a release or offers it again. Withdrawing shows the former release again; nobody is downgraded.
        /// </summary>
        Task SetWithdrawnAsync(int releaseId, bool withdrawn, CancellationToken cancellationToken);

        /// <summary>
        /// Marks a release as urgent or not.
        /// </summary>
        Task SetUrgentAsync(int releaseId, bool urgent, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the oldest releases of a channel beyond the newest <paramref name="keep"/>, with their packages - every
        /// package in the database grows every backup.
        /// </summary>
        /// <returns>how many releases were deleted</returns>
        Task<int> CleanupAsync(string productKey, string channel, int keep, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the assigned modules without a release for a version of the application, with the client apps concerned -
        /// to warn before publishing that version (M1).
        /// </summary>
        Task<IReadOnlyList<MissingModuleWarning>> CheckModulesForAsync(string baseProductKey, string channel, string version, CancellationToken cancellationToken);
    }

    /// <summary>
    /// See <see cref="IUpdateReleaseAdministration"/>.
    /// </summary>
    public sealed class UpdateReleaseAdministration<TContext, TClientApp, TClientAppTemplate> : IUpdateReleaseAdministration
        where TContext : DbContext, IUpdateServiceContext, IClientAppContext<TClientApp, TClientAppTemplate>
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        private readonly IDbContextFactory<TContext> contextFactory;
        private readonly IUpdatePackageStore store;

        public UpdateReleaseAdministration(IDbContextFactory<TContext> contextFactory, IUpdatePackageStore store)
        {
            this.contextFactory = contextFactory;
            this.store = store;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<UpdateProduct>> GetProductsAsync(CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var products = await db.UpdateProducts.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            return products.OrderBy(p => p.Kind == PackageKinds.Base ? 0 : 1).ThenBy(p => p.ProductKey, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<UpdateRelease>> GetReleasesAsync(string productKey, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var productId = await db.UpdateProducts.AsNoTracking().Where(p => p.ProductKey == productKey).Select(p => (int?)p.UpdateProductId)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (productId == null)
            {
                LogEnvironment.LogEvent($"Releases of the unknown product '{productKey}' were requested.", LogSeverity.Warning);
                return Array.Empty<UpdateRelease>();
            }

            var releases = await db.UpdateReleases.AsNoTracking().Where(r => r.UpdateProductId == productId)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            return releases.OrderBy(r => r.Channel, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(r => UpdateFeedBuilder<TContext>.ParseVersion(r.Version)).ToList();
        }

        /// <inheritdoc />
        public async Task<UpdateProduct> EnsureProductAsync(string productKey, string kind, string displayName, string? baseProductKey, CancellationToken cancellationToken)
        {
            if (kind != PackageKinds.Base && kind != PackageKinds.Module)
            {
                throw new ArgumentException($"A product is '{PackageKinds.Base}' or '{PackageKinds.Module}', not '{kind}'.", nameof(kind));
            }

            if (kind == PackageKinds.Module && string.IsNullOrWhiteSpace(baseProductKey))
            {
                throw new ArgumentException("A module needs the application it extends.", nameof(baseProductKey));
            }

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var existing = await db.UpdateProducts.SingleOrDefaultAsync(p => p.ProductKey == productKey, cancellationToken).ConfigureAwait(false);
            if (existing != null)
            {
                return existing;
            }

            var product = new UpdateProduct { ProductKey = productKey, Kind = kind, DisplayName = displayName, BaseProductKey = kind == PackageKinds.Module ? baseProductKey : null };
            db.UpdateProducts.Add(product);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogEnvironment.LogEvent($"Update product {productKey} ({kind}) created.", LogSeverity.Report);
            return product;
        }

        /// <inheritdoc />
        public async Task<UpdateRelease> UploadReleaseAsync(ReleaseUpload upload, Stream content, CancellationToken cancellationToken)
        {
            if (UpdateFeedBuilder<TContext>.ParseVersion(upload.Version) == new Version(0, 0, 0, 0))
            {
                throw new ArgumentException($"'{upload.Version}' is not a version.", nameof(upload));
            }

            if (!string.IsNullOrWhiteSpace(upload.Requires))
            {
                VersionRange.Parse(upload.Requires);
            }

            if (string.IsNullOrWhiteSpace(upload.FileName) || upload.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"'{upload.FileName}' is no valid file name.", nameof(upload));
            }

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var product = await db.UpdateProducts.SingleOrDefaultAsync(p => p.ProductKey == upload.ProductKey, cancellationToken).ConfigureAwait(false)
                          ?? throw new InvalidOperationException($"The update product {upload.ProductKey} does not exist.");
            if (await db.UpdateReleases.AnyAsync(r => r.UpdateProductId == product.UpdateProductId && r.Channel == upload.Channel && r.Version == upload.Version, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"{upload.ProductKey} {upload.Version} is already published in channel {upload.Channel}.");
            }

            var storageKey = $"{product.ProductKey}-{upload.Version}-{upload.Channel}-{Guid.NewGuid():N}";
            var stored = await store.SaveAsync(storageKey, content, cancellationToken).ConfigureAwait(false);
            var release = new UpdateRelease
            {
                UpdateProductId = product.UpdateProductId,
                Channel = upload.Channel,
                Version = upload.Version,
                PublishedUtc = upload.PublishedUtc ?? DateTime.UtcNow,
                Urgent = upload.Urgent,
                MinimumVersion = string.IsNullOrWhiteSpace(upload.MinimumVersion) ? null : upload.MinimumVersion,
                Requires = product.Kind == PackageKinds.Module && !string.IsNullOrWhiteSpace(upload.Requires) ? upload.Requires : null,
                Sha256 = stored.Sha256,
                Size = stored.Size,
                StorageKey = storageKey,
                FileName = upload.FileName,
                UploadedBy = upload.UploadedBy
            };
            try
            {
                db.UpdateReleases.Add(release);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The release {upload.ProductKey} {upload.Version} could not be recorded; its package is removed: {ex.OutlineException()}", LogSeverity.Error);
                await store.DeleteAsync(storageKey, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            LogEnvironment.LogEvent($"{upload.ProductKey} {upload.Version} published in channel {upload.Channel} ({stored.Size} bytes, SHA-256 {stored.Sha256}) by {upload.UploadedBy ?? "unknown"}.", LogSeverity.Report);
            return release;
        }

        /// <inheritdoc />
        public async Task SetWithdrawnAsync(int releaseId, bool withdrawn, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var release = await db.UpdateReleases.SingleAsync(r => r.UpdateReleaseId == releaseId, cancellationToken).ConfigureAwait(false);
            release.Withdrawn = withdrawn;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogEnvironment.LogEvent($"Release {releaseId} ({release.Version}, {release.Channel}) {(withdrawn ? "withdrawn" : "offered again")}.", LogSeverity.Report);
        }

        /// <inheritdoc />
        public async Task SetUrgentAsync(int releaseId, bool urgent, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var release = await db.UpdateReleases.SingleAsync(r => r.UpdateReleaseId == releaseId, cancellationToken).ConfigureAwait(false);
            release.Urgent = urgent;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogEnvironment.LogEvent($"Release {releaseId} ({release.Version}, {release.Channel}) is {(urgent ? "" : "no longer ")}urgent.", LogSeverity.Report);
        }

        /// <inheritdoc />
        public async Task<int> CleanupAsync(string productKey, string channel, int keep, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var product = await db.UpdateProducts.SingleOrDefaultAsync(p => p.ProductKey == productKey, cancellationToken).ConfigureAwait(false);
            if (product == null)
            {
                return 0;
            }

            var releases = (await db.UpdateReleases.Where(r => r.UpdateProductId == product.UpdateProductId && r.Channel == channel)
                    .ToListAsync(cancellationToken).ConfigureAwait(false))
                .OrderByDescending(r => UpdateFeedBuilder<TContext>.ParseVersion(r.Version))
                .ToList();
            var newestOffered = releases.FirstOrDefault(r => !r.Withdrawn);
            var obsolete = releases.Skip(Math.Max(1, keep)).Where(r => r != newestOffered).ToList();
            foreach (var release in obsolete)
            {
                await store.DeleteAsync(release.StorageKey, cancellationToken).ConfigureAwait(false);
                db.UpdateReleases.Remove(release);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (obsolete.Count > 0)
            {
                LogEnvironment.LogEvent($"{obsolete.Count} old release(s) of {productKey} in {channel} deleted ({string.Join(", ", obsolete.Select(r => r.Version))}).", LogSeverity.Report);
            }

            return obsolete.Count;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MissingModuleWarning>> CheckModulesForAsync(string baseProductKey, string channel, string version, CancellationToken cancellationToken)
        {
            var target = UpdateFeedBuilder<TContext>.ParseVersion(version);
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var modules = await db.UpdateProducts.AsNoTracking().Where(p => p.BaseProductKey == baseProductKey && p.Kind == PackageKinds.Module)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var warnings = new List<MissingModuleWarning>();
            foreach (var module in modules)
            {
                var releases = await db.UpdateReleases.AsNoTracking()
                    .Where(r => r.UpdateProductId == module.UpdateProductId && r.Channel == channel && !r.Withdrawn)
                    .Select(r => r.Requires).ToListAsync(cancellationToken).ConfigureAwait(false);
                if (releases.Any(requires => Fits(requires, target)))
                {
                    continue;
                }

                var templateIds = await db.ClientAppTemplateModuleAssignments.AsNoTracking().Where(a => a.ModuleProductKey == module.ProductKey)
                    .Select(a => a.ClientAppTemplateId).ToListAsync(cancellationToken).ConfigureAwait(false);
                var directIds = await db.ClientAppModuleAssignments.AsNoTracking().Where(a => a.ModuleProductKey == module.ProductKey)
                    .Select(a => a.ClientAppId).ToListAsync(cancellationToken).ConfigureAwait(false);
                if (templateIds.Count == 0 && directIds.Count == 0)
                {
                    continue;
                }

                var apps = await db.ClientApps.IgnoreQueryFilters().AsNoTracking()
                    // EF.Property: a member read through the interface of the generic type is not translated by every provider.
                    .Where(c => templateIds.Contains(EF.Property<int>(c, nameof(IClientAppCore.ClientAppTemplateId)))
                                || directIds.Contains(EF.Property<int>(c, nameof(IClientAppCore.ClientAppId))))
                    .Select(c => new AffectedClientApp
                    {
                        TenantId = EF.Property<int>(c, nameof(IClientAppCore.TenantId)),
                        ClientAppId = EF.Property<int>(c, nameof(IClientAppCore.ClientAppId)),
                        ClientName = EF.Property<string>(c, nameof(IClientAppCore.ClientName))
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                warnings.Add(new MissingModuleWarning { ModuleProductKey = module.ProductKey, ClientApps = apps });
            }

            return warnings;
        }

        private static bool Fits(string? requires, Version target)
        {
            if (string.IsNullOrWhiteSpace(requires))
            {
                return true;
            }

            try
            {
                return VersionRange.Parse(requires).Includes(target);
            }
            catch (FormatException ex)
            {
                LogEnvironment.LogEvent($"The stored range '{requires}' is not readable; it counts as not fitting: {ex.Message}", LogSeverity.Warning);
                return false;
            }
        }
    }
}
