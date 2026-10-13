using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Impl
{
    /// <summary>
    /// Bindung → ClientApp → Agent → Proxy, im aktiven Mandanten.
    /// </summary>
    public class AgentServiceResolver<TContext, TClientApp, TClientAppTemplate> : IAgentServiceResolver
        where TContext : DbContext, IAgentServiceBindingContext, IClientAppContext<TClientApp, TClientAppTemplate>, ITenantScopeContext
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IAgentConnector connector;
        private readonly IReadOnlyDictionary<string, AgentServiceSlot> slots;
        private readonly ILogger<AgentServiceResolver<TContext, TClientApp, TClientAppTemplate>> logger;

        /// <summary>Initializes a new instance of the <see cref="AgentServiceResolver{TContext, TClientApp, TClientAppTemplate}"/> class.</summary>
        public AgentServiceResolver(IDbContextFactory<TContext> dbFactory, IAgentConnector connector,
            IEnumerable<AgentServiceSlot> slots, ILogger<AgentServiceResolver<TContext, TClientApp, TClientAppTemplate>> logger)
        {
            this.dbFactory = dbFactory;
            this.connector = connector;
            this.slots = slots.ToDictionary(s => s.Name, StringComparer.Ordinal);
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task<AgentServiceHandle<T>> GetAsync<T>(int clientAppId, string slot,
            CancellationToken cancellationToken = default) where T : class
        {
            if (!slots.TryGetValue(slot, out var declared))
            {
                logger.LogError("Service slot {Slot} was requested, but no such slot is declared (AddAgentServiceSlot).", slot);
                return Fail<T>(AgentServiceStatus.UnknownSlot, $"Slot '{slot}' is not declared.");
            }

            if (!typeof(T).IsAssignableFrom(declared.Contract))
            {
                logger.LogError("Service slot {Slot} carries {Contract}, but {Requested} was requested.", slot,
                    declared.Contract, typeof(T));
                return Fail<T>(AgentServiceStatus.ContractMismatch,
                    $"Slot '{slot}' provides {declared.Contract.Name}, not {typeof(T).Name}.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            if (db.CurrentTenantId is not { } tenantId)
            {
                logger.LogWarning("Service slot {Slot} of client app {ClientAppId} was requested outside a tenant.", slot, clientAppId);
                return Fail<T>(AgentServiceStatus.NoTenant, "There is no tenant in scope.");
            }

            var clientKey = await db.ClientApps.AsNoTracking()
                .Where(a => EF.Property<int>(a, nameof(IClientAppCore.ClientAppId)) == clientAppId
                            && EF.Property<int>(a, nameof(IClientAppCore.TenantId)) == tenantId)
                .Select(a => EF.Property<string>(a, nameof(IClientAppCore.ClientKey)))
                .FirstOrDefaultAsync(cancellationToken);
            if (clientKey == null)
            {
                logger.LogWarning("Client app {ClientAppId} does not exist in tenant {TenantId}.", clientAppId, tenantId);
                return Fail<T>(AgentServiceStatus.UnknownClientApp, "This till does not exist.");
            }

            var binding = await db.ClientAppServiceBindings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.ClientAppId == clientAppId && b.TenantId == tenantId && b.Slot == slot,
                    cancellationToken);
            if (binding == null)
            {
                logger.LogInformation("Slot {Slot} of client app {ClientAppId} is not bound to a device.", slot, clientAppId);
                return Fail<T>(AgentServiceStatus.NotBound, $"No device is set up for '{declared.DisplayName}'.");
            }

            if (!binding.Enabled)
            {
                logger.LogInformation("Slot {Slot} of client app {ClientAppId} is switched off.", slot, clientAppId);
                return Fail<T>(AgentServiceStatus.Disabled, $"'{declared.DisplayName}' is switched off.", binding.ObjectName);
            }

            try
            {
                var service = await connector.GetObjectAsync<T>(tenantId, clientKey, binding.ObjectName, cancellationToken);
                return new AgentServiceHandle<T>
                {
                    Status = AgentServiceStatus.Ok,
                    Service = service,
                    ObjectName = binding.ObjectName
                };
            }
            catch (AgentConnectionException ex)
            {
                // Der Connector hat den Grund bereits protokolliert; hier steht, WELCHE Rolle es traf.
                logger.LogWarning(ex, "Slot {Slot} of client app {ClientAppId} cannot be served.", slot, clientAppId);
                return Fail<T>(AgentServiceStatus.Unreachable, ex.Message, binding.ObjectName);
            }
        }

        private static AgentServiceHandle<T> Fail<T>(AgentServiceStatus status, string message, string? objectName = null)
            where T : class
            => new() { Status = status, Message = message, ObjectName = objectName };
    }
}
