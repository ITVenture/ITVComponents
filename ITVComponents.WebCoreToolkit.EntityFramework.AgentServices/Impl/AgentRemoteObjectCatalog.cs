using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.InterProcessExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Impl
{
    /// <summary>
    /// Die mitgelieferte Antwort auf „was gibt es auf diesem Dienst": der Katalog des Agenten, gefragt im
    /// aktiven Mandanten.
    /// </summary>
    /// <remarks>
    /// Ein nicht erreichbarer Agent ist hier eine <see cref="AgentConnectionException"/> und keine leere
    /// Liste: leer heisst laut Vertrag „bietet nichts dieser Art an". Wer eine Auswahl füllt, fängt die
    /// Ausnahme und zeichnet ein Textfeld.
    /// </remarks>
    public class AgentRemoteObjectCatalog<TContext> : IRemoteObjectCatalog
        where TContext : DbContext, ITenantScopeContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IAgentConnector connector;
        private readonly ILogger<AgentRemoteObjectCatalog<TContext>> logger;

        /// <summary>Initializes a new instance of the <see cref="AgentRemoteObjectCatalog{TContext}"/> class.</summary>
        public AgentRemoteObjectCatalog(IDbContextFactory<TContext> dbFactory, IAgentConnector connector,
            ILogger<AgentRemoteObjectCatalog<TContext>> logger)
        {
            this.dbFactory = dbFactory;
            this.connector = connector;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<RemoteObjectInfo>> GetObjectsAsync(string serviceName, Type contract,
            CancellationToken cancellationToken = default)
        {
            int tenantId;
            await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            {
                if (db.CurrentTenantId is not { } current)
                {
                    // Ohne Mandant kein Agent - und ganz sicher nicht die Agenten ALLER Mandanten.
                    logger.LogWarning("The objects of agent {ServiceName} were requested outside a tenant.", serviceName);
                    return [];
                }

                tenantId = current;
            }

            var catalog = await connector.GetCatalogAsync(tenantId, serviceName, cancellationToken);
            var objects = await catalog.GetObjectsAsync(contract.FullName!);
            return [.. objects.Select(o => new RemoteObjectInfo(o.Name, o.DisplayName))];
        }
    }
}
