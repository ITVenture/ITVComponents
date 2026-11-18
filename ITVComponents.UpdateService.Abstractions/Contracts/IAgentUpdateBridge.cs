using System.Threading.Tasks;

namespace ITVComponents.UpdateService.Abstractions.Contracts
{
    /// <summary>
    /// What the backend may ask an application about its updates. The application offers it on the service hub under
    /// <see cref="UpdateContractTypes.BridgeObjectName"/> and forwards it to its local update service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The backend calls the application, never the update service: the service is only reachable on the machine (named
    /// pipe), and the application is the instance the backend knows (its client app).
    /// </para>
    /// <para>
    /// Only ids travel; what a module is, decides the feed of the update service alone.
    /// </para>
    /// </remarks>
    public interface IAgentUpdateBridge
    {
        /// <summary>
        /// Gets the state of the updates on this machine.
        /// </summary>
        /// <returns>the report; never null - without an update service, <see cref="AgentUpdateReport.ServiceConnected"/> is false</returns>
        Task<AgentUpdateReport> GetReportAsync();

        /// <summary>
        /// Sets the modules this machine is to have. Without a connected update service the assignment is kept and delivered
        /// as soon as the service is reachable.
        /// </summary>
        /// <param name="moduleIds">the product keys of the modules; empty removes all</param>
        /// <returns>the report after the assignment</returns>
        Task<AgentUpdateReport> AssignModulesAsync(string[] moduleIds);

        /// <summary>
        /// Asks the update service to look for a new version now (e.g. after an urgent release).
        /// </summary>
        /// <returns>true if the service was reached</returns>
        Task<bool> CheckNowAsync();
    }

    /// <summary>
    /// The state of the updates on one machine, as the application reports it to the backend.
    /// </summary>
    public sealed class AgentUpdateReport
    {
        /// <summary>Gets or sets the product id of the application.</summary>
        public string? ProductId { get; set; }

        /// <summary>Gets or sets the version of the running application.</summary>
        public string? ApplicationVersion { get; set; }

        /// <summary>Gets or sets a value indicating whether the application is connected to its update service.</summary>
        public bool ServiceConnected { get; set; }

        /// <summary>Gets or sets why the update service refused the application, if it did.</summary>
        public string? RefusalReason { get; set; }

        /// <summary>Gets or sets a value indicating whether an assignment waits for the service to become reachable.</summary>
        public bool AssignmentPending { get; set; }

        /// <summary>Gets or sets the state reported by the update service; null without service.</summary>
        public UpdateStatus? Status { get; set; }
    }
}
