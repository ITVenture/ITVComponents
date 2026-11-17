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
        /// <returns>how the service sees the application</returns>
        Task<AgentRegistration> RegisterAsync(string productId, string version);

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
