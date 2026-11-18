using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.UpdateService.Abstractions.Contracts;

namespace ITVComponents.UpdateService.Client
{
    /// <summary>
    /// The application's answer to the backend about its updates: forwards to the <see cref="UpdateServiceClient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The application offers it on the service hub under <see cref="UpdateContractTypes.BridgeObjectName"/>, e.g. as
    /// plugin <c>[ITVComponents.UpdateService.Client.dll]&lt;ITVComponents.UpdateService.Client.AgentUpdateBridge&gt;($updates)</c>,
    /// with the <see cref="UpdateServiceClient"/> registered as <c>updates</c>.
    /// </para>
    /// <para>
    /// The bridge does not own the client: disposing it leaves the client running.
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class AgentUpdateBridge : IAgentUpdateBridge, IPlugin
    {
        private readonly UpdateServiceClient updates;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentUpdateBridge"/> class.
        /// </summary>
        /// <param name="updates">the connection of the application to its update service</param>
        public AgentUpdateBridge(UpdateServiceClient updates)
        {
            this.updates = updates ?? throw new ArgumentNullException(nameof(updates));
        }

        /// <summary>
        /// Is raised when the backend changed the assignment of modules. The modules are installed while the application is
        /// ended; the event lets the application tell the user.
        /// </summary>
        public event Action<string[]>? ModulesAssigned;

        /// <inheritdoc />
        public event EventHandler? Disposed;

        /// <inheritdoc />
        public string UniqueName { get; set; } = UpdateContractTypes.BridgeObjectName;

        /// <inheritdoc />
        public async Task<AgentUpdateReport> GetReportAsync()
        {
            return await Report().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<AgentUpdateReport> AssignModulesAsync(string[] moduleIds)
        {
            moduleIds ??= Array.Empty<string>();
            LogEnvironment.LogEvent($"The backend assigns the modules [{string.Join(", ", moduleIds)}].", LogSeverity.Report);
            if (!await updates.AssignModulesAsync(moduleIds).ConfigureAwait(false))
            {
                LogEnvironment.LogEvent("The module assignment did not reach the update service; it is delivered once the service is reachable.", LogSeverity.Warning);
            }

            var handler = ModulesAssigned;
            if (handler != null)
            {
                try
                {
                    handler(moduleIds);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"A handler of ModulesAssigned failed: {ex.OutlineException()}", LogSeverity.Error);
                }
            }

            return await Report().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> CheckNowAsync()
        {
            var reached = await updates.CheckNowAsync().ConfigureAwait(false);
            if (!reached)
            {
                LogEnvironment.LogEvent("The backend asked to look for updates, but no update service is connected.", LogSeverity.Warning);
            }

            return reached;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        private async Task<AgentUpdateReport> Report()
        {
            var status = updates.Connected ? await updates.GetStatusAsync().ConfigureAwait(false) : null;
            return new AgentUpdateReport
            {
                ProductId = updates.ProductId,
                ApplicationVersion = updates.Version,
                ServiceConnected = status != null,
                RefusalReason = updates.RefusalReason,
                AssignmentPending = updates.AssignmentPending,
                Status = status
            };
        }
    }
}
