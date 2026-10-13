using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Impl
{
    /// <summary>
    /// Die Pflege der Rollen einer Kasse über den Kontext des Hosts.
    /// </summary>
    public class AgentServiceAdministration<TContext, TClientApp, TClientAppTemplate> : IAgentServiceAdministration
        where TContext : DbContext, IAgentServiceBindingContext, IClientAppContext<TClientApp, TClientAppTemplate>, ITenantScopeContext
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        /// <summary>Lesen — dasselbe Recht wie die ClientApp-Liste.</summary>
        public const string ViewPermission = "Apps.View";

        /// <summary>Schreiben — dasselbe Recht wie das Bearbeiten einer ClientApp.</summary>
        public const string WritePermission = "Apps.Write";

        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IAgentConnector connector;
        private readonly IServiceProvider services;
        private readonly IReadOnlyList<AgentServiceSlot> slots;
        private readonly ILogger<AgentServiceAdministration<TContext, TClientApp, TClientAppTemplate>> logger;

        /// <summary>Initializes a new instance of the <see cref="AgentServiceAdministration{TContext, TClientApp, TClientAppTemplate}"/> class.</summary>
        public AgentServiceAdministration(IDbContextFactory<TContext> dbFactory, IAgentConnector connector,
            IServiceProvider services, IEnumerable<AgentServiceSlot> slots,
            ILogger<AgentServiceAdministration<TContext, TClientApp, TClientAppTemplate>> logger)
        {
            this.dbFactory = dbFactory;
            this.connector = connector;
            this.services = services;
            this.slots = slots.ToArray();
            this.logger = logger;
        }

        /// <inheritdoc />
        public bool CanView() => services.VerifyUserPermissions([ViewPermission, WritePermission]);

        /// <inheritdoc />
        public bool CanManage() => services.VerifyUserPermissions([WritePermission]);

        /// <inheritdoc />
        public async Task<IReadOnlyList<AgentServiceBindingInfo>> GetBindingsAsync(int clientAppId,
            CancellationToken cancellationToken = default)
        {
            var (tenantId, _, _) = await ClientAppAsync(clientAppId, CanView(), cancellationToken);
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var bound = await db.ClientAppServiceBindings.AsNoTracking()
                .Where(b => b.ClientAppId == clientAppId && b.TenantId == tenantId)
                .ToDictionaryAsync(b => b.Slot, StringComparer.Ordinal, cancellationToken);
            return
            [
                .. slots.Select(s => bound.TryGetValue(s.Name, out var b)
                    ? new AgentServiceBindingInfo(s.Name, s.DisplayName, b.ObjectName, b.Enabled)
                    : new AgentServiceBindingInfo(s.Name, s.DisplayName, null, false))
            ];
        }

        /// <inheritdoc />
        public async Task<AgentStatusInfo> GetStatusAsync(int clientAppId, CancellationToken cancellationToken = default)
        {
            var (tenantId, clientKey, lastSeen) = await ClientAppAsync(clientAppId, CanView(), cancellationToken);
            try
            {
                await connector.GetCatalogAsync(tenantId, clientKey, cancellationToken);
                return new AgentStatusInfo(true, lastSeen, null);
            }
            catch (AgentConnectionException ex)
            {
                // Kein Fehler der Maske: ein Agent, der gerade nicht laeuft, ist ein normaler Zustand. Der
                // Connector hat den Grund protokolliert.
                return new AgentStatusInfo(false, lastSeen, ex.Message);
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AgentObjectInfo>> GetOffersAsync(int clientAppId, string slot,
            CancellationToken cancellationToken = default)
        {
            var declared = Slot(slot);
            var catalog = await CatalogAsync(clientAppId, CanView(), cancellationToken);
            return await catalog.GetObjectsAsync(declared.Contract.FullName!);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SettingDescriptor>> DescribeSettingsAsync(int clientAppId, string objectName,
            CancellationToken cancellationToken = default)
            => await (await CatalogAsync(clientAppId, CanView(), cancellationToken)).DescribeSettingsAsync(objectName);

        /// <inheritdoc />
        /// <remarks>Die Einstellungen eines Geräts können Adressen und Kennungen enthalten — Schreibrecht.</remarks>
        public async Task<string?> GetSettingsAsync(int clientAppId, string objectName,
            CancellationToken cancellationToken = default)
            => await (await CatalogAsync(clientAppId, CanManage(), cancellationToken)).GetSettingsAsync(objectName);

        /// <inheritdoc />
        public async Task<IReadOnlyList<SettingChoice>> GetChoicesAsync(int clientAppId, string objectName,
            string settingName, string? draftJson, CancellationToken cancellationToken = default)
            => await (await CatalogAsync(clientAppId, CanManage(), cancellationToken))
                .GetChoicesAsync(objectName, settingName, draftJson);

        /// <inheritdoc />
        /// <remarks><b>Schreibt auf dem Kassen-PC.</b></remarks>
        public async Task<ApplySettingsResult> ApplySettingsAsync(int clientAppId, string objectName, string settingsJson,
            CancellationToken cancellationToken = default)
        {
            var result = await (await CatalogAsync(clientAppId, CanManage(), cancellationToken))
                .ApplySettingsAsync(objectName, settingsJson);
            if (!result.Success)
            {
                logger.LogWarning("The agent of client app {ClientAppId} refused the settings of {ObjectName}: {Message}",
                    clientAppId, objectName, result.Message ?? string.Join("; ", result.Errors.Select(e => e.Message)));
            }

            return result;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Ist der Agent erreichbar, muss er das Gerät für den Vertrag der Rolle anbieten — sonst stünde eine
        /// Bindung auf ein Objekt da, das es nicht gibt oder das etwas anderes kann. Ist er es nicht, wird
        /// trotzdem gespeichert (und protokolliert): eine Kasse soll sich auch einrichten lassen, wenn der
        /// Laden-PC gerade aus ist.
        /// </remarks>
        public async Task SaveBindingAsync(int clientAppId, string slot, string objectName, bool enabled,
            CancellationToken cancellationToken = default)
        {
            var declared = Slot(slot);
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new AgentServiceAdministrationException("No device was chosen.");
            }

            var (tenantId, clientKey, _) = await ClientAppAsync(clientAppId, CanManage(), cancellationToken);
            try
            {
                var catalog = await connector.GetCatalogAsync(tenantId, clientKey, cancellationToken);
                var offers = await catalog.GetObjectsAsync(declared.Contract.FullName!);
                if (offers.All(o => o.Name != objectName))
                {
                    logger.LogWarning("Client app {ClientAppId} does not offer {ObjectName} as {Contract}; slot {Slot} was not saved.",
                        clientAppId, objectName, declared.Contract, slot);
                    throw new AgentServiceAdministrationException(
                        $"The till does not offer '{objectName}' as {declared.DisplayName}.");
                }
            }
            catch (AgentConnectionException ex)
            {
                logger.LogWarning(ex, "Slot {Slot} of client app {ClientAppId} is saved without checking {ObjectName} - the agent cannot be reached.",
                    slot, clientAppId, objectName);
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var binding = await db.ClientAppServiceBindings
                .FirstOrDefaultAsync(b => b.ClientAppId == clientAppId && b.TenantId == tenantId && b.Slot == slot,
                    cancellationToken);
            if (binding == null)
            {
                binding = new ClientAppServiceBinding { ClientAppId = clientAppId, TenantId = tenantId, Slot = slot };
                db.ClientAppServiceBindings.Add(binding);
            }

            binding.ObjectName = objectName.Trim();
            binding.Enabled = enabled;
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task RemoveBindingAsync(int clientAppId, string slot, CancellationToken cancellationToken = default)
        {
            var (tenantId, _, _) = await ClientAppAsync(clientAppId, CanManage(), cancellationToken);
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var binding = await db.ClientAppServiceBindings
                .FirstOrDefaultAsync(b => b.ClientAppId == clientAppId && b.TenantId == tenantId && b.Slot == slot,
                    cancellationToken);
            if (binding == null)
            {
                logger.LogInformation("Slot {Slot} of client app {ClientAppId} was not bound; nothing to remove.", slot, clientAppId);
                return;
            }

            db.ClientAppServiceBindings.Remove(binding);
            await db.SaveChangesAsync(cancellationToken);
        }

        private AgentServiceSlot Slot(string slot)
            => slots.FirstOrDefault(s => s.Name == slot)
               ?? throw new AgentServiceAdministrationException($"Slot '{slot}' is not declared.");

        private async Task<IAgentObjectCatalog> CatalogAsync(int clientAppId, bool permitted,
            CancellationToken cancellationToken)
        {
            var (tenantId, clientKey, _) = await ClientAppAsync(clientAppId, permitted, cancellationToken);
            return await connector.GetCatalogAsync(tenantId, clientKey, cancellationToken);
        }

        /// <summary>
        /// Recht, Mandant und die Kasse — und zwar nur eine des aktiven Mandanten.
        /// </summary>
        private async Task<(int TenantId, string ClientKey, DateTime? LastSeenUtc)> ClientAppAsync(int clientAppId,
            bool permitted, CancellationToken cancellationToken)
        {
            if (!permitted)
            {
                logger.LogWarning("Access to the service slots of client app {ClientAppId} was refused: missing permission.", clientAppId);
                throw new AgentServiceAdministrationException("The acting user may not manage the devices of this till.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            if (db.CurrentTenantId is not { } tenantId)
            {
                logger.LogWarning("The service slots of client app {ClientAppId} were requested outside a tenant.", clientAppId);
                throw new AgentServiceAdministrationException("There is no tenant in scope.");
            }

            var app = await db.ClientApps.AsNoTracking()
                .Where(a => EF.Property<int>(a, nameof(IClientAppCore.ClientAppId)) == clientAppId
                            && EF.Property<int>(a, nameof(IClientAppCore.TenantId)) == tenantId)
                .Select(a => new
                {
                    Key = EF.Property<string>(a, nameof(IClientAppCore.ClientKey)),
                    LastSeen = EF.Property<DateTime?>(a, nameof(IClientAppCore.LastSeenUtc))
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (app == null)
            {
                logger.LogWarning("Client app {ClientAppId} does not exist in tenant {TenantId}.", clientAppId, tenantId);
                throw new AgentServiceAdministrationException("This till does not exist.");
            }

            return (tenantId, app.Key, app.LastSeen);
        }
    }
}
