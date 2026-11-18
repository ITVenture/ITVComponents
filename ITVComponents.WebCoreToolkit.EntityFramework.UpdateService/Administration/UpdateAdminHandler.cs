using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Agents;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Administration
{
    /// <summary>
    /// The permissions of the update masks.
    /// </summary>
    public static class UpdatePermissions
    {
        /// <summary>Reading products, releases and the modules of templates (platform).</summary>
        public const string ReleasesView = "Updates.Releases.View";

        /// <summary>Publishing, withdrawing, cleaning up; assigning modules to templates (platform).</summary>
        public const string ReleasesWrite = "Updates.Releases.Write";

        /// <summary>Reading the update state of a till - the same permission as the list of client apps.</summary>
        public const string ClientAppsView = "Apps.View";

        /// <summary>Assigning modules to a till, asking it to check - the same permission as editing a client app.</summary>
        public const string ClientAppsWrite = "Apps.Write";
    }

    /// <summary>
    /// Asks whether the acting user holds permissions. Separate, so the handler is testable without a security repository.
    /// </summary>
    public interface IUpdatePermissionGate
    {
        /// <summary>Whether the acting user holds at least one of the permissions.</summary>
        bool Holds(params string[] permissions);
    }

    /// <summary>
    /// The gate over the security repository of the request.
    /// </summary>
    public sealed class ServiceProviderUpdatePermissionGate : IUpdatePermissionGate
    {
        private readonly IServiceProvider services;

        public ServiceProviderUpdatePermissionGate(IServiceProvider services)
        {
            this.services = services;
        }

        /// <inheritdoc />
        public bool Holds(params string[] permissions) => services.VerifyUserPermissions(permissions);
    }

    /// <summary>
    /// A refusal or an error the mask shows as it is.
    /// </summary>
    public sealed class UpdateAdministrationException : Exception
    {
        public UpdateAdministrationException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// The update state of one till, as the tab of the client app shows it.
    /// </summary>
    public sealed class ClientAppUpdateInfo
    {
        /// <summary>The state last reported; null if the till never answered.</summary>
        public ClientAppUpdateState? State { get; set; }

        /// <summary>The modules reported by the till.</summary>
        public IReadOnlyList<ModuleState> ReportedModules { get; set; } = Array.Empty<ModuleState>();

        /// <summary>The modules the till gets through its template.</summary>
        public string[] TemplateModules { get; set; } = Array.Empty<string>();

        /// <summary>The modules the till has in addition.</summary>
        public string[] OwnModules { get; set; } = Array.Empty<string>();

        /// <summary>All modules that can be assigned.</summary>
        public IReadOnlyList<UpdateProduct> AvailableModules { get; set; } = Array.Empty<UpdateProduct>();
    }

    /// <summary>
    /// A template with its modules.
    /// </summary>
    public sealed class TemplateModulesInfo
    {
        public int ClientAppTemplateId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string[] Modules { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// What the update masks may do, with the permission checked here - not only in the mask.
    /// </summary>
    /// <remarks>
    /// Releases and the modules of templates are platform-wide (<see cref="UpdatePermissions.ReleasesView"/>,
    /// <see cref="UpdatePermissions.ReleasesWrite"/>). The tab of a till works in the active tenant only.
    /// </remarks>
    public interface IUpdateAdminHandler
    {
        bool CanViewReleases();

        bool CanManageReleases();

        bool CanViewClientApps();

        bool CanManageClientApps();

        Task<IReadOnlyList<UpdateProduct>> GetProductsAsync(CancellationToken cancellationToken = default);

        Task<UpdateProduct> SaveProductAsync(string productKey, string kind, string displayName, string? baseProductKey, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<UpdateRelease>> GetReleasesAsync(string productKey, CancellationToken cancellationToken = default);

        /// <summary>The M1 warning before publishing an application version; empty for a module.</summary>
        Task<IReadOnlyList<MissingModuleWarning>> CheckBeforeUploadAsync(string productKey, string channel, string version, CancellationToken cancellationToken = default);

        Task<UpdateRelease> UploadAsync(ReleaseUpload upload, Stream content, CancellationToken cancellationToken = default);

        Task SetWithdrawnAsync(int releaseId, bool withdrawn, CancellationToken cancellationToken = default);

        Task SetUrgentAsync(int releaseId, bool urgent, CancellationToken cancellationToken = default);

        Task<int> CleanupAsync(string productKey, string channel, int keep, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TemplateModulesInfo>> GetTemplatesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<UpdateSyncResult>> SetTemplateModulesAsync(int clientAppTemplateId, string[] modules, CancellationToken cancellationToken = default);

        Task<ClientAppUpdateInfo> GetClientAppAsync(int clientAppId, CancellationToken cancellationToken = default);

        Task<UpdateSyncResult> SetClientAppModulesAsync(int clientAppId, string[] modules, CancellationToken cancellationToken = default);

        Task<UpdateSyncResult> RefreshClientAppAsync(int clientAppId, CancellationToken cancellationToken = default);

        Task<UpdateSyncResult> CheckClientAppNowAsync(int clientAppId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// See <see cref="IUpdateAdminHandler"/>.
    /// </summary>
    public sealed class UpdateAdminHandler<TContext, TClientApp, TClientAppTemplate> : IUpdateAdminHandler
        where TContext : DbContext, IUpdateServiceContext, IClientAppContext<TClientApp, TClientAppTemplate>, ITenantScopeContext
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        private readonly IDbContextFactory<TContext> contextFactory;
        private readonly IUpdateReleaseAdministration releases;
        private readonly IModuleAssignmentAdministration assignments;
        private readonly IClientAppUpdateSync sync;
        private readonly IUpdatePermissionGate gate;
        private readonly ILogger<UpdateAdminHandler<TContext, TClientApp, TClientAppTemplate>> logger;

        public UpdateAdminHandler(IDbContextFactory<TContext> contextFactory, IUpdateReleaseAdministration releases, IModuleAssignmentAdministration assignments,
            IClientAppUpdateSync sync, IUpdatePermissionGate gate, ILogger<UpdateAdminHandler<TContext, TClientApp, TClientAppTemplate>> logger)
        {
            this.contextFactory = contextFactory;
            this.releases = releases;
            this.assignments = assignments;
            this.sync = sync;
            this.gate = gate;
            this.logger = logger;
        }

        /// <inheritdoc />
        public bool CanViewReleases() => gate.Holds(UpdatePermissions.ReleasesView, UpdatePermissions.ReleasesWrite);

        /// <inheritdoc />
        public bool CanManageReleases() => gate.Holds(UpdatePermissions.ReleasesWrite);

        /// <inheritdoc />
        public bool CanViewClientApps() => gate.Holds(UpdatePermissions.ClientAppsView, UpdatePermissions.ClientAppsWrite);

        /// <inheritdoc />
        public bool CanManageClientApps() => gate.Holds(UpdatePermissions.ClientAppsWrite);

        /// <inheritdoc />
        public Task<IReadOnlyList<UpdateProduct>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            Require(CanViewReleases(), "read the update products");
            return releases.GetProductsAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<UpdateProduct> SaveProductAsync(string productKey, string kind, string displayName, string? baseProductKey, CancellationToken cancellationToken = default)
        {
            Require(CanManageReleases(), "maintain update products");
            return Guard(() => releases.EnsureProductAsync(productKey?.Trim() ?? string.Empty, kind, displayName, string.IsNullOrWhiteSpace(baseProductKey) ? null : baseProductKey.Trim(), cancellationToken));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<UpdateRelease>> GetReleasesAsync(string productKey, CancellationToken cancellationToken = default)
        {
            Require(CanViewReleases(), "read the releases");
            return releases.GetReleasesAsync(productKey, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MissingModuleWarning>> CheckBeforeUploadAsync(string productKey, string channel, string version, CancellationToken cancellationToken = default)
        {
            Require(CanManageReleases(), "publish releases");
            var product = (await releases.GetProductsAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(p => p.ProductKey == productKey);
            if (product == null || product.Kind != PackageKinds.Base)
            {
                return Array.Empty<MissingModuleWarning>();
            }

            return await Guard(() => releases.CheckModulesForAsync(productKey, channel, version, cancellationToken)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<UpdateRelease> UploadAsync(ReleaseUpload upload, Stream content, CancellationToken cancellationToken = default)
        {
            Require(CanManageReleases(), "publish releases");
            return Guard(() => releases.UploadReleaseAsync(upload, content, cancellationToken));
        }

        /// <inheritdoc />
        public Task SetWithdrawnAsync(int releaseId, bool withdrawn, CancellationToken cancellationToken = default)
        {
            Require(CanManageReleases(), "withdraw releases");
            return Guard(async () =>
            {
                await releases.SetWithdrawnAsync(releaseId, withdrawn, cancellationToken).ConfigureAwait(false);
                return true;
            });
        }

        /// <inheritdoc />
        public Task SetUrgentAsync(int releaseId, bool urgent, CancellationToken cancellationToken = default)
        {
            Require(CanManageReleases(), "mark releases as urgent");
            return Guard(async () =>
            {
                await releases.SetUrgentAsync(releaseId, urgent, cancellationToken).ConfigureAwait(false);
                return true;
            });
        }

        /// <inheritdoc />
        public Task<int> CleanupAsync(string productKey, string channel, int keep, CancellationToken cancellationToken = default)
        {
            Require(CanManageReleases(), "clean up releases");
            if (keep < 1)
            {
                throw new UpdateAdministrationException("At least one release must be kept.");
            }

            return Guard(() => releases.CleanupAsync(productKey, channel, keep, cancellationToken));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TemplateModulesInfo>> GetTemplatesAsync(CancellationToken cancellationToken = default)
        {
            Require(CanViewReleases(), "read the modules of templates");
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var templates = await db.ClientAppTemplates.AsNoTracking()
                .Select(t => new TemplateModulesInfo
                {
                    ClientAppTemplateId = EF.Property<int>(t, nameof(IClientAppTemplateCore.ClientAppTemplateId)),
                    Name = EF.Property<string>(t, nameof(IClientAppTemplateCore.Name))
                })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var modules = await db.ClientAppTemplateModuleAssignments.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var template in templates)
            {
                template.Modules = modules.Where(m => m.ClientAppTemplateId == template.ClientAppTemplateId).Select(m => m.ModuleProductKey)
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
            }

            return templates.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<UpdateSyncResult>> SetTemplateModulesAsync(int clientAppTemplateId, string[] modules, CancellationToken cancellationToken = default)
        {
            Require(CanManageReleases(), "assign modules to templates");
            return Guard(() => assignments.SetTemplateModulesAsync(clientAppTemplateId, modules, cancellationToken));
        }

        /// <inheritdoc />
        public async Task<ClientAppUpdateInfo> GetClientAppAsync(int clientAppId, CancellationToken cancellationToken = default)
        {
            var (tenantId, templateId) = await ClientAppAsync(clientAppId, CanViewClientApps(), "read the update state", cancellationToken).ConfigureAwait(false);
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var state = await db.ClientAppUpdateStates.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClientAppId == clientAppId && s.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
            var available = (await releases.GetProductsAsync(cancellationToken).ConfigureAwait(false)).Where(p => p.Kind == PackageKinds.Module).ToList();
            return new ClientAppUpdateInfo
            {
                State = state,
                ReportedModules = state?.ReadModules() ?? Array.Empty<ModuleState>(),
                TemplateModules = await assignments.GetTemplateModulesAsync(templateId, cancellationToken).ConfigureAwait(false),
                OwnModules = await assignments.GetClientAppModulesAsync(tenantId, clientAppId, cancellationToken).ConfigureAwait(false),
                AvailableModules = available
            };
        }

        /// <inheritdoc />
        public async Task<UpdateSyncResult> SetClientAppModulesAsync(int clientAppId, string[] modules, CancellationToken cancellationToken = default)
        {
            var (tenantId, _) = await ClientAppAsync(clientAppId, CanManageClientApps(), "assign modules", cancellationToken).ConfigureAwait(false);
            return await Guard(() => assignments.SetClientAppModulesAsync(tenantId, clientAppId, modules, cancellationToken)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<UpdateSyncResult> RefreshClientAppAsync(int clientAppId, CancellationToken cancellationToken = default)
        {
            var (tenantId, _) = await ClientAppAsync(clientAppId, CanViewClientApps(), "read the update state", cancellationToken).ConfigureAwait(false);
            return await sync.RefreshStateAsync(tenantId, clientAppId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<UpdateSyncResult> CheckClientAppNowAsync(int clientAppId, CancellationToken cancellationToken = default)
        {
            var (tenantId, _) = await ClientAppAsync(clientAppId, CanManageClientApps(), "ask a till to check for updates", cancellationToken).ConfigureAwait(false);
            return await sync.CheckNowAsync(tenantId, clientAppId, cancellationToken).ConfigureAwait(false);
        }

        private void Require(bool permitted, string what)
        {
            if (!permitted)
            {
                logger.LogWarning("The acting user may not {What}: missing permission.", what);
                throw new UpdateAdministrationException($"You may not {what}.");
            }
        }

        /// <summary>
        /// Invalid input from the mask (unknown module, version published twice, unreadable range) becomes a message.
        /// </summary>
        private async Task<T> Guard<T>(Func<Task<T>> call)
        {
            try
            {
                return await call().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                logger.LogWarning(ex, "An update administration request was refused.");
                throw new UpdateAdministrationException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Permission, tenant and the till - one of the active tenant only.
        /// </summary>
        private async Task<(int TenantId, int TemplateId)> ClientAppAsync(int clientAppId, bool permitted, string what, CancellationToken cancellationToken)
        {
            Require(permitted, what);
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            if (db.CurrentTenantId is not { } tenantId)
            {
                logger.LogWarning("The update state of client app {ClientAppId} was requested outside a tenant.", clientAppId);
                throw new UpdateAdministrationException("There is no tenant in scope.");
            }

            var templateId = await db.ClientApps.IgnoreQueryFilters().AsNoTracking()
                .Where(a => EF.Property<int>(a, nameof(IClientAppCore.ClientAppId)) == clientAppId
                            && EF.Property<int>(a, nameof(IClientAppCore.TenantId)) == tenantId)
                .Select(a => (int?)EF.Property<int>(a, nameof(IClientAppCore.ClientAppTemplateId)))
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (templateId == null)
            {
                logger.LogWarning("Client app {ClientAppId} does not exist in tenant {TenantId}.", clientAppId, tenantId);
                throw new UpdateAdministrationException("This till does not exist.");
            }

            return (tenantId, templateId.Value);
        }
    }
}
