using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Agents
{
    /// <summary>
    /// Settings of the exchange between the backend and the agents.
    /// </summary>
    public sealed class UpdateAgentOptions
    {
        /// <summary>
        /// Gets or sets the name under which the agents offer their <see cref="IAgentUpdateBridge"/>.
        /// </summary>
        public string BridgeObjectName { get; set; } = UpdateContractTypes.BridgeObjectName;

        /// <summary>
        /// Gets or sets how long after an agent connected to the hub the backend pushes the assignment and fetches the
        /// state. The agent needs the time to reach its local update service.
        /// </summary>
        public TimeSpan ConnectDelay { get; set; } = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// How a synchronization with an agent went.
    /// </summary>
    public sealed class UpdateSyncResult
    {
        /// <summary>Gets or sets the client app.</summary>
        public int ClientAppId { get; set; }

        /// <summary>Gets or sets a value indicating whether the agent answered.</summary>
        public bool Reached { get; set; }

        /// <summary>Gets or sets why the agent could not be reached.</summary>
        public AgentConnectionFailure? Failure { get; set; }

        /// <summary>Gets or sets the message for the user, if the agent was not reached.</summary>
        public string? Message { get; set; }

        /// <summary>Gets or sets the state that was stored; null if the agent was not reached.</summary>
        public ClientAppUpdateState? State { get; set; }
    }

    /// <summary>
    /// Brings the assignment of modules to an agent and its update state into the backend. The backend calls the agent
    /// (<see cref="IAgentUpdateBridge"/>), the agent forwards to its local update service.
    /// </summary>
    public interface IClientAppUpdateSync
    {
        /// <summary>
        /// The modules a client app is to have: those of its template and its own.
        /// </summary>
        Task<string[]> GetAssignedModulesAsync(int clientAppId, CancellationToken cancellationToken);

        /// <summary>
        /// Pushes the assignment to the agent and stores the state it reports.
        /// </summary>
        Task<UpdateSyncResult> SynchronizeAsync(int tenantId, int clientAppId, CancellationToken cancellationToken);

        /// <summary>
        /// Fetches the state from the agent and stores it, without touching the assignment.
        /// </summary>
        Task<UpdateSyncResult> RefreshStateAsync(int tenantId, int clientAppId, CancellationToken cancellationToken);

        /// <summary>
        /// Asks the agent to let its update service look for a new version now, then fetches the state.
        /// </summary>
        Task<UpdateSyncResult> CheckNowAsync(int tenantId, int clientAppId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// See <see cref="IClientAppUpdateSync"/>.
    /// </summary>
    /// <remarks>
    /// The tenant is part of every call: the agent is resolved through <see cref="IAgentConnector"/>, which finds only a
    /// client app of that tenant.
    /// </remarks>
    public sealed class ClientAppUpdateSync<TContext, TClientApp, TClientAppTemplate> : IClientAppUpdateSync
        where TContext : DbContext, IUpdateServiceContext, IClientAppContext<TClientApp, TClientAppTemplate>
        where TClientApp : class, IClientAppCore
        where TClientAppTemplate : class, IClientAppTemplateCore
    {
        private readonly IDbContextFactory<TContext> contextFactory;
        private readonly IAgentConnector connector;
        private readonly UpdateAgentOptions options;
        private readonly ILogger<ClientAppUpdateSync<TContext, TClientApp, TClientAppTemplate>> logger;

        public ClientAppUpdateSync(IDbContextFactory<TContext> contextFactory, IAgentConnector connector, IOptions<UpdateAgentOptions> options,
            ILogger<ClientAppUpdateSync<TContext, TClientApp, TClientAppTemplate>> logger)
        {
            this.contextFactory = contextFactory;
            this.connector = connector;
            this.options = options.Value;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task<string[]> GetAssignedModulesAsync(int clientAppId, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var templateId = await db.ClientApps.IgnoreQueryFilters().AsNoTracking()
                .Where(c => EF.Property<int>(c, nameof(IClientAppCore.ClientAppId)) == clientAppId)
                .Select(c => (int?)EF.Property<int>(c, nameof(IClientAppCore.ClientAppTemplateId)))
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (templateId == null)
            {
                logger.LogWarning("Client app {ClientAppId} does not exist; it has no modules.", clientAppId);
                return Array.Empty<string>();
            }

            var fromTemplate = await db.ClientAppTemplateModuleAssignments.AsNoTracking().Where(a => a.ClientAppTemplateId == templateId)
                .Select(a => a.ModuleProductKey).ToListAsync(cancellationToken).ConfigureAwait(false);
            var own = await db.ClientAppModuleAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.ClientAppId == clientAppId)
                .Select(a => a.ModuleProductKey).ToListAsync(cancellationToken).ConfigureAwait(false);
            return fromTemplate.Union(own, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <inheritdoc />
        public async Task<UpdateSyncResult> SynchronizeAsync(int tenantId, int clientAppId, CancellationToken cancellationToken)
        {
            var modules = await GetAssignedModulesAsync(clientAppId, cancellationToken).ConfigureAwait(false);
            return await WithBridge(tenantId, clientAppId, bridge => bridge.AssignModulesAsync(modules), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<UpdateSyncResult> RefreshStateAsync(int tenantId, int clientAppId, CancellationToken cancellationToken)
        {
            return WithBridge(tenantId, clientAppId, bridge => bridge.GetReportAsync(), cancellationToken);
        }

        /// <inheritdoc />
        public Task<UpdateSyncResult> CheckNowAsync(int tenantId, int clientAppId, CancellationToken cancellationToken)
        {
            return WithBridge(tenantId, clientAppId, async bridge =>
            {
                if (!await bridge.CheckNowAsync().ConfigureAwait(false))
                {
                    logger.LogWarning("Client app {ClientAppId} has no update service connected; nothing was checked.", clientAppId);
                }

                return await bridge.GetReportAsync().ConfigureAwait(false);
            }, cancellationToken);
        }

        private async Task<UpdateSyncResult> WithBridge(int tenantId, int clientAppId, Func<IAgentUpdateBridge, Task<AgentUpdateReport>> call, CancellationToken cancellationToken)
        {
            var result = new UpdateSyncResult { ClientAppId = clientAppId };
            string? clientKey;
            await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            {
                clientKey = await db.ClientApps.IgnoreQueryFilters().AsNoTracking()
                    .Where(c => EF.Property<int>(c, nameof(IClientAppCore.ClientAppId)) == clientAppId
                                && EF.Property<int>(c, nameof(IClientAppCore.TenantId)) == tenantId)
                    .Select(c => EF.Property<string>(c, nameof(IClientAppCore.ClientKey)))
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(clientKey))
            {
                logger.LogWarning("Client app {ClientAppId} does not exist in tenant {TenantId}.", clientAppId, tenantId);
                result.Failure = AgentConnectionFailure.UnknownAgent;
                result.Message = "The till does not exist in this tenant.";
                return result;
            }

            AgentUpdateReport report;
            try
            {
                var bridge = await connector.GetObjectAsync<IAgentUpdateBridge>(tenantId, clientKey, options.BridgeObjectName, cancellationToken).ConfigureAwait(false);
                report = await call(bridge).ConfigureAwait(false);
            }
            catch (AgentConnectionException ex)
            {
                // The connector logged the cause; an agent that is off is a normal case.
                logger.LogInformation("Client app {ClientKey} was not reached for the update state: {Failure} {Message}", clientKey, ex.Failure, ex.Message);
                result.Failure = ex.Failure;
                result.Message = ex.Message;
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The update bridge of client app {ClientKey} failed.", clientKey);
                result.Failure = AgentConnectionFailure.Unreachable;
                result.Message = ex.Message;
                return result;
            }

            if (report == null)
            {
                logger.LogError("The update bridge of client app {ClientKey} answered without report.", clientKey);
                result.Failure = AgentConnectionFailure.Unreachable;
                result.Message = "The till answered without report.";
                return result;
            }

            result.Reached = true;
            result.State = await StoreAsync(tenantId, clientAppId, report, cancellationToken).ConfigureAwait(false);
            return result;
        }

        private async Task<ClientAppUpdateState> StoreAsync(int tenantId, int clientAppId, AgentUpdateReport report, CancellationToken cancellationToken)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var state = await db.ClientAppUpdateStates.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ClientAppId == clientAppId, cancellationToken).ConfigureAwait(false);
            if (state == null)
            {
                state = new ClientAppUpdateState { ClientAppId = clientAppId };
                db.ClientAppUpdateStates.Add(state);
            }

            var status = report.Status;
            state.TenantId = tenantId;
            state.InstalledVersion = Cut(status?.InstalledVersion ?? report.ApplicationVersion, 32);
            state.Phase = report.ServiceConnected && status != null ? status.Phase.ToString() : ClientAppUpdateStateExtensions.NoServicePhase;
            state.AvailableVersion = Cut(status?.AvailableVersion, 32);
            state.BlockedBy = Cut(status?.BlockedBy, 512);
            state.Error = Cut(status?.Error ?? report.RefusalReason, 1024);
            state.DeferredUntilUtc = status?.DeferredUntil?.UtcDateTime;
            state.ModulesJson = status?.Modules == null ? null : JsonSerializer.Serialize(status.Modules);
            state.ReportedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return state;
        }

        private static string? Cut(string? text, int length)
        {
            return text == null || text.Length <= length ? text : text.Substring(0, length);
        }
    }

    /// <summary>
    /// Reading a stored <see cref="ClientAppUpdateState"/>.
    /// </summary>
    public static class ClientAppUpdateStateExtensions
    {
        /// <summary>The phase stored when the agent runs without update service; otherwise the phase is an <see cref="UpdatePhase"/>.</summary>
        public const string NoServicePhase = "NoService";

        /// <summary>
        /// Reads <see cref="ClientAppUpdateState.ModulesJson"/>.
        /// </summary>
        public static IReadOnlyList<ModuleState> ReadModules(this ClientAppUpdateState state)
        {
            return string.IsNullOrEmpty(state.ModulesJson)
                ? Array.Empty<ModuleState>()
                : JsonSerializer.Deserialize<ModuleState[]>(state.ModulesJson) ?? Array.Empty<ModuleState>();
        }
    }
}
