using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Agents;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Administration
{
    /// <summary>
    /// Which client apps get which modules.
    /// </summary>
    /// <remarks>
    /// A change is stored first and then pushed to the agents that are reachable. An agent that is off gets it when it
    /// connects to the hub; the results say which were reached.
    /// </remarks>
    public interface IModuleAssignmentAdministration
    {
        /// <summary>The modules of a template (platform).</summary>
        Task<string[]> GetTemplateModulesAsync(int clientAppTemplateId, CancellationToken cancellationToken);

        /// <summary>Sets the modules of a template (platform) and pushes them to every enabled client app of it, in all tenants.</summary>
        Task<IReadOnlyList<UpdateSyncResult>> SetTemplateModulesAsync(int clientAppTemplateId, string[] moduleProductKeys, CancellationToken cancellationToken);

        /// <summary>The modules a client app has in addition to those of its template.</summary>
        Task<string[]> GetClientAppModulesAsync(int tenantId, int clientAppId, CancellationToken cancellationToken);

        /// <summary>Sets the modules a client app has in addition to those of its template, and pushes the result to it.</summary>
        Task<UpdateSyncResult> SetClientAppModulesAsync(int tenantId, int clientAppId, string[] moduleProductKeys, CancellationToken cancellationToken);
    }

    /// <summary>
    /// See <see cref="IModuleAssignmentAdministration"/>.
    /// </summary>
    public sealed class ModuleAssignmentAdministration<TContext, TClientApp, TClientAppTemplate> : IModuleAssignmentAdministration
        where TContext : DbContext, IUpdateServiceContext, IClientAppContext<TClientApp, TClientAppTemplate>
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        private readonly IDbContextFactory<TContext> contextFactory;
        private readonly IClientAppUpdateSync sync;
        private readonly ILogger<ModuleAssignmentAdministration<TContext, TClientApp, TClientAppTemplate>> logger;

        public ModuleAssignmentAdministration(IDbContextFactory<TContext> contextFactory, IClientAppUpdateSync sync,
            ILogger<ModuleAssignmentAdministration<TContext, TClientApp, TClientAppTemplate>> logger)
        {
            this.contextFactory = contextFactory;
            this.sync = sync;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task<string[]> GetTemplateModulesAsync(int clientAppTemplateId, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await db.ClientAppTemplateModuleAssignments.AsNoTracking().Where(a => a.ClientAppTemplateId == clientAppTemplateId)
                .OrderBy(a => a.ModuleProductKey).Select(a => a.ModuleProductKey).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<UpdateSyncResult>> SetTemplateModulesAsync(int clientAppTemplateId, string[] moduleProductKeys, CancellationToken cancellationToken)
        {
            List<(int TenantId, int ClientAppId)> apps;
            await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            {
                var keys = await CheckModulesAsync(db, moduleProductKeys, cancellationToken).ConfigureAwait(false);
                var current = await db.ClientAppTemplateModuleAssignments.Where(a => a.ClientAppTemplateId == clientAppTemplateId)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                db.ClientAppTemplateModuleAssignments.RemoveRange(current.Where(a => !keys.Contains(a.ModuleProductKey, StringComparer.OrdinalIgnoreCase)));
                foreach (var key in keys.Where(k => !current.Any(a => string.Equals(a.ModuleProductKey, k, StringComparison.OrdinalIgnoreCase))))
                {
                    db.ClientAppTemplateModuleAssignments.Add(new ClientAppTemplateModuleAssignment { ClientAppTemplateId = clientAppTemplateId, ModuleProductKey = key });
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                apps = (await db.ClientApps.IgnoreQueryFilters().AsNoTracking()
                        .Where(c => EF.Property<int>(c, nameof(IClientAppCore.ClientAppTemplateId)) == clientAppTemplateId
                                    && EF.Property<bool>(c, nameof(IClientAppCore.Enabled)))
                        .Select(c => new { TenantId = EF.Property<int>(c, nameof(IClientAppCore.TenantId)), ClientAppId = EF.Property<int>(c, nameof(IClientAppCore.ClientAppId)) })
                        .ToListAsync(cancellationToken).ConfigureAwait(false))
                    .Select(c => (c.TenantId, c.ClientAppId)).ToList();
            }

            logger.LogInformation("The modules of template {TemplateId} are now [{Modules}]; pushing to {Count} client apps.", clientAppTemplateId,
                string.Join(", ", moduleProductKeys), apps.Count);
            var results = new List<UpdateSyncResult>();
            foreach (var app in apps)
            {
                results.Add(await sync.SynchronizeAsync(app.TenantId, app.ClientAppId, cancellationToken).ConfigureAwait(false));
            }

            return results;
        }

        /// <inheritdoc />
        public async Task<string[]> GetClientAppModulesAsync(int tenantId, int clientAppId, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await db.ClientAppModuleAssignments.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.ClientAppId == clientAppId)
                .OrderBy(a => a.ModuleProductKey).Select(a => a.ModuleProductKey).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<UpdateSyncResult> SetClientAppModulesAsync(int tenantId, int clientAppId, string[] moduleProductKeys, CancellationToken cancellationToken)
        {
            await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            {
                var exists = await db.ClientApps.IgnoreQueryFilters().AnyAsync(c => EF.Property<int>(c, nameof(IClientAppCore.ClientAppId)) == clientAppId
                                                                                   && EF.Property<int>(c, nameof(IClientAppCore.TenantId)) == tenantId, cancellationToken).ConfigureAwait(false);
                if (!exists)
                {
                    throw new InvalidOperationException($"Client app {clientAppId} does not exist in tenant {tenantId}.");
                }

                var keys = await CheckModulesAsync(db, moduleProductKeys, cancellationToken).ConfigureAwait(false);
                var current = await db.ClientAppModuleAssignments.IgnoreQueryFilters().Where(a => a.TenantId == tenantId && a.ClientAppId == clientAppId)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                db.ClientAppModuleAssignments.RemoveRange(current.Where(a => !keys.Contains(a.ModuleProductKey, StringComparer.OrdinalIgnoreCase)));
                foreach (var key in keys.Where(k => !current.Any(a => string.Equals(a.ModuleProductKey, k, StringComparison.OrdinalIgnoreCase))))
                {
                    db.ClientAppModuleAssignments.Add(new ClientAppModuleAssignment { TenantId = tenantId, ClientAppId = clientAppId, ModuleProductKey = key });
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return await sync.SynchronizeAsync(tenantId, clientAppId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Only known modules may be assigned: an unknown id would make the update service wait for a module that never comes.
        /// </summary>
        private static async Task<string[]> CheckModulesAsync(TContext db, string[] moduleProductKeys, CancellationToken cancellationToken)
        {
            var keys = (moduleProductKeys ?? Array.Empty<string>()).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            // Compared here, not in the query: whether the database compares without case depends on its collation.
            var modules = await db.UpdateProducts.AsNoTracking().Where(p => p.Kind == PackageKinds.Module)
                .Select(p => p.ProductKey).ToListAsync(cancellationToken).ConfigureAwait(false);
            var unknown = keys.Where(k => !modules.Contains(k, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (unknown.Length != 0)
            {
                throw new ArgumentException($"No module is known as {string.Join(", ", unknown)}.", nameof(moduleProductKeys));
            }

            // The spelling of the product, so that the update service finds it in the feed.
            return modules.Where(m => keys.Contains(m, StringComparer.OrdinalIgnoreCase)).ToArray();
        }
    }
}
