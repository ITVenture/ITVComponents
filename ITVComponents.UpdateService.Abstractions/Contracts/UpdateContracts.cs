using System;

namespace ITVComponents.UpdateService.Abstractions.Contracts
{
    /// <summary>
    /// Where the updates stand.
    /// </summary>
    public enum UpdatePhase
    {
        /// <summary>Nothing checked yet.</summary>
        Unknown = 0,

        /// <summary>The installed version is the newest.</summary>
        UpToDate = 1,

        /// <summary>A newer version is being downloaded and checked.</summary>
        Downloading = 2,

        /// <summary>A newer version is ready; it is installed once the application has ended.</summary>
        ReadyToInstall = 3,

        /// <summary>The application asked to wait.</summary>
        Deferred = 4,

        /// <summary>The installation runs.</summary>
        Installing = 5,

        /// <summary>The installation succeeded, but Windows must restart to complete it.</summary>
        RestartRequired = 6,

        /// <summary>Something failed; see <see cref="UpdateStatus.Error"/>.</summary>
        Failed = 7
    }

    /// <summary>
    /// The state of the updates, as the application shows it and reports it to the backend.
    /// </summary>
    public sealed class UpdateStatus
    {
        /// <summary>Gets or sets the phase.</summary>
        public UpdatePhase Phase { get; set; }

        /// <summary>Gets or sets the installed version.</summary>
        public string? InstalledVersion { get; set; }

        /// <summary>Gets or sets the version that is available, if newer.</summary>
        public string? AvailableVersion { get; set; }

        /// <summary>Gets or sets a value indicating whether the available version is urgent.</summary>
        public bool Urgent { get; set; }

        /// <summary>Gets or sets until when the application asked to wait.</summary>
        public DateTimeOffset? DeferredUntil { get; set; }

        /// <summary>Gets or sets from when on no deferral is granted any more.</summary>
        public DateTimeOffset? DeferralLimit { get; set; }

        /// <summary>Gets or sets when the service last looked for a new version.</summary>
        public DateTimeOffset? LastCheck { get; set; }

        /// <summary>Gets or sets why the last step failed.</summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Why the service wants the application to end.
    /// </summary>
    public enum ShutdownReason
    {
        /// <summary>An update is ready. The application ends as soon as it is idle.</summary>
        Update = 1,

        /// <summary>An urgent update. The application only finishes what is running; after the deadline it is ended.</summary>
        UrgentUpdate = 2,

        /// <summary>An administrator wants it.</summary>
        Administrator = 3
    }

    /// <summary>
    /// The wish of the service that the application ends.
    /// </summary>
    public sealed class ShutdownRequest
    {
        /// <summary>Gets or sets why.</summary>
        public ShutdownReason Reason { get; set; }

        /// <summary>Gets or sets until when the application must have ended; null means: whenever it is idle.</summary>
        public DateTimeOffset? Deadline { get; set; }

        /// <summary>Gets or sets the version that will be installed.</summary>
        public string? Version { get; set; }
    }

    /// <summary>
    /// What an instance of the application may do. Of all instances on a machine, one is active; the others wait.
    /// </summary>
    public enum AgentRole
    {
        /// <summary>The instance waits: no hub connection, no devices.</summary>
        Waiting = 0,

        /// <summary>The instance is active: it connects to the hub and holds the devices.</summary>
        Active = 1
    }

    /// <summary>
    /// The instruction to one instance to take over or release.
    /// </summary>
    public sealed class RoleChange
    {
        /// <summary>Gets or sets the process the instruction is for.</summary>
        public int ProcessId { get; set; }

        /// <summary>Gets or sets the session of that process.</summary>
        public int SessionId { get; set; }

        /// <summary>Gets or sets the new role.</summary>
        public AgentRole Role { get; set; }
    }

    /// <summary>
    /// How the service sees an application that announced itself.
    /// </summary>
    public sealed class AgentRegistration
    {
        /// <summary>Gets or sets a value indicating whether the registration was accepted.</summary>
        public bool Accepted { get; set; }

        /// <summary>Gets or sets why not.</summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the registration was refused because the application already runs in
        /// the same session. The new instance should end.
        /// </summary>
        public bool AlreadyRunning { get; set; }

        /// <summary>Gets or sets the role the instance starts with.</summary>
        public AgentRole Role { get; set; }

        /// <summary>Gets or sets the session of the active instance, if another one is active.</summary>
        public int? ActiveSessionId { get; set; }

        /// <summary>Gets or sets the user of the active instance, if another one is active ("runs already under user X").</summary>
        public string? ActiveUserName { get; set; }

        /// <summary>Gets or sets the Windows session of the application, as the operating system reports it.</summary>
        public int SessionId { get; set; }

        /// <summary>Gets or sets the process id of the application, as the operating system reports it.</summary>
        public int ProcessId { get; set; }

        /// <summary>Gets or sets the current state of the updates.</summary>
        public UpdateStatus? Status { get; set; }
    }

    /// <summary>
    /// The names of the contract types on the wire. Both ends register them (<c>JsonHelper.RegisterManualType</c>), so that
    /// a server that only accepts registered types reads them.
    /// </summary>
    public static class UpdateContractTypes
    {
        /// <summary>
        /// The name under which the service offers its facade.
        /// </summary>
        public const string FacadeObjectName = "updates";

        /// <summary>
        /// Registers every contract type with the given registration (e.g. <c>JsonHelper.RegisterManualType</c>).
        /// </summary>
        public static void RegisterAll(Action<Type, string> register)
        {
            register(typeof(UpdateStatus), "itv.update.status");
            register(typeof(UpdatePhase), "itv.update.phase");
            register(typeof(ShutdownRequest), "itv.update.shutdown");
            register(typeof(ShutdownReason), "itv.update.shutdownreason");
            register(typeof(AgentRegistration), "itv.update.registration");
            register(typeof(AgentRole), "itv.update.role");
            register(typeof(RoleChange), "itv.update.rolechange");
        }
    }
}
