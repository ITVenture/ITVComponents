using System;
using System.Threading.Tasks;

namespace ITVComponents.UpdateService.Abstractions.Contracts
{
    /// <summary>
    /// What an application may ask of the update service, over the local pipe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every call is a wish, never a command with an address, a path or a file: what the service installs is decided by
    /// its own configuration and the check of the package alone. Who calls (user, session, process) the service learns
    /// from the operating system, not from the arguments.
    /// </para>
    /// <para>
    /// The application connects with <c>UpdateServiceClient</c> (package <c>ITVComponents.UpdateService.Client</c>).
    /// </para>
    /// </remarks>
    public interface IUpdateServiceFacade
    {
        /// <summary>
        /// Announces the application. The connection stands for its presence: when it ends, the application is gone.
        /// </summary>
        /// <param name="productId">the product the application belongs to</param>
        /// <param name="version">the version of the application</param>
        /// <param name="currentlyActive">whether the instance is active right now. Only a hint for a service that restarted
        /// and does not know yet which instance is active; the service decides.</param>
        /// <returns>how the service sees the application. Called again regularly; it notices a lost connection and lets a
        /// restarted service know the application again.</returns>
        Task<AgentRegistration> RegisterAsync(string productId, string version, bool currentlyActive);

        /// <summary>
        /// Gets the current state of the updates.
        /// </summary>
        Task<UpdateStatus> GetStatusAsync();

        /// <summary>
        /// Asks the service to look for a new version now (tray menu, or the backend reported one).
        /// </summary>
        Task CheckNowAsync();

        /// <summary>
        /// Asks the service to wait before it ends the application (a payment or a print is running).
        /// </summary>
        /// <param name="minutes">how long to wait</param>
        /// <param name="reason">why, for the log</param>
        /// <returns>whether the deferral was granted; an urgent update or an exhausted deferral is not deferred</returns>
        Task<bool> DeferAsync(int minutes, string reason);

        /// <summary>
        /// Tells the service which modules this machine is to have (from the backend: the assignment of the client app).
        /// Only ids - what they are and where they come from, the feed alone decides. Modules not in the list are removed.
        /// </summary>
        /// <param name="moduleIds">the ids of the assigned modules</param>
        Task AssignModulesAsync(string[] moduleIds);

        /// <summary>
        /// Reports, after a start, which modules the application loaded and which not. A module that failed is switched
        /// back to its former version (or removed), and its new version is not tried again until a newer one appears.
        /// </summary>
        /// <param name="reports">one report per module</param>
        Task ReportModulesLoadedAsync(ModuleLoadReport[] reports);

        /// <summary>
        /// Creates an instance of a template of an assigned and installed module in <c>plugins.json</c>. The load type is
        /// the one of the template's mark; the application keeps the instance's settings itself.
        /// </summary>
        /// <param name="moduleId">the module</param>
        /// <param name="typeName">the type with the mark <c>Template</c></param>
        /// <param name="name">the name of the new instance; it must be valid and free</param>
        /// <returns>whether the instance was created, and if not, why</returns>
        Task<PluginInstanceResult> CreatePluginInstanceAsync(string moduleId, string typeName, string name);

        /// <summary>
        /// Removes an instance of a template from <c>plugins.json</c>. Infrastructure instances follow their module and can
        /// not be removed.
        /// </summary>
        /// <param name="name">the name of the instance</param>
        /// <returns>whether the instance was removed, and if not, why</returns>
        Task<PluginInstanceResult> RemovePluginInstanceAsync(string name);

        /// <summary>
        /// Confirms that the application has released what only the active instance may hold (hub connection, devices),
        /// after the service asked it to (<see cref="RoleChanged"/> to <see cref="AgentRole.Waiting"/>). The service
        /// hands over to the next instance only then - or when the release takes too long.
        /// </summary>
        Task ReportReleasedAsync();

        /// <summary>
        /// Is raised when an instance is to take over or release. Goes to every connected application: each one acts only
        /// on the change addressed to it (<see cref="RoleChange.ProcessId"/>).
        /// </summary>
        event Action<RoleChange> RoleChanged;

        /// <summary>
        /// Is raised when the service wants the application to end. The application finishes what is running and ends; the
        /// end of its connection is the acknowledgement.
        /// </summary>
        event Action<ShutdownRequest> ShutdownRequested;

        /// <summary>
        /// Is raised when the state of the updates changes.
        /// </summary>
        event Action<UpdateStatus> StatusChanged;
    }
}
