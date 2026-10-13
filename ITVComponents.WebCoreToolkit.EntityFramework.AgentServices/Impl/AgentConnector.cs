using ITVComponents.AgentServices.Abstractions;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Impl
{
    /// <summary>
    /// Der Weg zu einem Agenten: ClientApp des Mandanten → <c>ClientKey</c> → Client des Hosts → Proxy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die ClientApp wird über Mandant <b>und</b> Schlüssel gesucht. Der Dienst-Verteiler hat einen flachen
    /// Namensraum ohne Mandanten; hier ist die Stelle, an der ein fremder Agent „nicht vorhanden" wird.
    /// </para>
    /// <para>
    /// Gelesen wird über <c>EF.Property</c> und nicht über die Core-Schnittstelle: ein Zugriff über ein
    /// Interface ist in einem EF-Ausdruck nicht übersetzbar.
    /// </para>
    /// </remarks>
    public class AgentConnector<TContext, TClientApp, TClientAppTemplate> : IAgentConnector
        where TContext : DbContext, IClientAppContext<TClientApp, TClientAppTemplate>
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IAgentClientFactory? clients;
        private readonly ILogger<AgentConnector<TContext, TClientApp, TClientAppTemplate>> logger;

        /// <summary>Initializes a new instance of the <see cref="AgentConnector{TContext, TClientApp, TClientAppTemplate}"/> class.</summary>
        public AgentConnector(IDbContextFactory<TContext> dbFactory,
            ILogger<AgentConnector<TContext, TClientApp, TClientAppTemplate>> logger, IAgentClientFactory? clients = null)
        {
            this.dbFactory = dbFactory;
            this.clients = clients;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task<IAgentObjectCatalog> GetCatalogAsync(int tenantId, string clientKey,
            CancellationToken cancellationToken = default)
        {
            var agent = await FindAsync(tenantId, clientKey, cancellationToken);
            return Connect<IAgentObjectCatalog>(clientKey, agent.CatalogObjectName);
        }

        /// <inheritdoc />
        public async Task<T> GetObjectAsync<T>(int tenantId, string clientKey, string objectName,
            CancellationToken cancellationToken = default) where T : class
        {
            await FindAsync(tenantId, clientKey, cancellationToken);
            return Connect<T>(clientKey, objectName);
        }

        /// <summary>Die ClientApp dieses Mandanten mit diesem Schlüssel, und der Name ihres Katalogs.</summary>
        private async Task<(bool Enabled, string CatalogObjectName)> FindAsync(int tenantId, string clientKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(clientKey))
            {
                throw new AgentConnectionException(AgentConnectionFailure.UnknownAgent, "No agent was named.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            // Der Mandant steht ausdruecklich in der Abfrage; ein Mandantenfilter des Kontexts (der hier keinen
            // aktiven Mandanten haben muss, etwa beim Nachfragen im Hintergrund) waere dafuer keine Garantie.
            var app = await db.ClientApps.IgnoreQueryFilters().AsNoTracking()
                .Where(a => EF.Property<int>(a, nameof(IClientAppCore.TenantId)) == tenantId
                            && EF.Property<string>(a, nameof(IClientAppCore.ClientKey)) == clientKey)
                .Select(a => new
                {
                    Enabled = EF.Property<bool>(a, nameof(IClientAppCore.Enabled)),
                    TemplateId = EF.Property<int>(a, nameof(IClientAppCore.ClientAppTemplateId))
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (app == null)
            {
                logger.LogWarning("No client app with key {ClientKey} exists in tenant {TenantId}.", clientKey, tenantId);
                throw new AgentConnectionException(AgentConnectionFailure.UnknownAgent,
                    $"There is no till '{clientKey}' in this tenant.");
            }

            if (!app.Enabled)
            {
                logger.LogWarning("Client app {ClientKey} of tenant {TenantId} is disabled.", clientKey, tenantId);
                throw new AgentConnectionException(AgentConnectionFailure.Disabled, $"Till '{clientKey}' is disabled.");
            }

            var catalogName = await db.ClientAppTemplates.AsNoTracking()
                .Where(t => EF.Property<int>(t, nameof(IClientAppTemplateCore.ClientAppTemplateId)) == app.TemplateId)
                .Select(t => EF.Property<string?>(t, nameof(IClientAppTemplateCore.CatalogObjectName)))
                .FirstOrDefaultAsync(cancellationToken);
            return (true, string.IsNullOrWhiteSpace(catalogName) ? AgentCatalog.DefaultObjectName : catalogName);
        }

        /// <summary>Der Proxy — erst nachdem feststeht, dass der Agent das Objekt anbietet.</summary>
        private T Connect<T>(string clientKey, string objectName) where T : class
        {
            if (clients == null)
            {
                logger.LogError("Agent {ClientKey} cannot be reached: no IAgentClientFactory is registered.", clientKey);
                throw new AgentConnectionException(AgentConnectionFailure.NotConfigured,
                    "This application has no connection to agents configured (IAgentClientFactory is missing).");
            }

            IBaseClient client;
            try
            {
                client = clients.GetClient(clientKey);
                var availability = client.CheckRemoteObjectAvailability(objectName);
                if (!availability.Available)
                {
                    logger.LogWarning("Agent {ClientKey} does not offer {ObjectName}: {Message}", clientKey, objectName,
                        availability.Message);
                    throw new AgentConnectionException(AgentConnectionFailure.Unreachable,
                        $"Till '{clientKey}' does not offer '{objectName}': {availability.Message}");
                }
            }
            catch (AgentConnectionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Agent {ClientKey} could not be reached.", clientKey);
                throw new AgentConnectionException(AgentConnectionFailure.Unreachable,
                    $"Till '{clientKey}' cannot be reached: {ex.Message}", ex);
            }

            return client.CreateProxy<T>(objectName);
        }
    }
}
