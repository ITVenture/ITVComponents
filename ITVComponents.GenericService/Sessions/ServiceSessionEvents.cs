using System;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.GenericService.Sessions
{
    /// <summary>
    /// Tells the plugins of a service about Windows sessions: logon and logoff, lock and unlock, console and remote
    /// connections.
    /// </summary>
    /// <remarks>
    /// Platform neutral on purpose: only a Windows service run with <c>UseGenericWindowsService()</c> from the package
    /// <c>ITVComponents.GenericService.Windows</c> receives these notifications. Anywhere else (Linux, console, debugging)
    /// <see cref="Active"/> is false and nothing is raised. A plugin gets this object by a constructor parameter named
    /// <see cref="ServiceSessionEvents.ParameterName"/>.
    /// </remarks>
    public interface IServiceSessionEvents
    {
        /// <summary>
        /// Gets a value indicating whether session changes are reported at all (the process runs as a Windows service
        /// with session notifications).
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// Is raised for every session change Windows reports to the service.
        /// </summary>
        event EventHandler<ServiceSessionChangedEventArgs> SessionChanged;
    }

    /// <summary>
    /// What happened to a Windows session. The values match those of the service control manager
    /// (<c>WTS_CONSOLE_CONNECT</c> ... <c>WTS_SESSION_TERMINATE</c>).
    /// </summary>
    public enum ServiceSessionChange
    {
        ConsoleConnect = 1,
        ConsoleDisconnect = 2,
        RemoteConnect = 3,
        RemoteDisconnect = 4,
        SessionLogon = 5,
        SessionLogoff = 6,
        SessionLock = 7,
        SessionUnlock = 8,
        SessionRemoteControl = 9,
        SessionCreate = 10,
        SessionTerminate = 11
    }

    /// <summary>
    /// A session change.
    /// </summary>
    public sealed class ServiceSessionChangedEventArgs : EventArgs
    {
        public ServiceSessionChangedEventArgs(ServiceSessionChange reason, int sessionId)
        {
            Reason = reason;
            SessionId = sessionId;
        }

        /// <summary>
        /// Gets what happened.
        /// </summary>
        public ServiceSessionChange Reason { get; }

        /// <summary>
        /// Gets the Windows session it happened in.
        /// </summary>
        public int SessionId { get; }
    }

    /// <summary>
    /// The session notifications of a service, shared by its host and its plugins.
    /// </summary>
    public sealed class ServiceSessionEvents : IServiceSessionEvents
    {
        /// <summary>
        /// The name of the constructor parameter through which a plugin receives the session notifications.
        /// </summary>
        public const string ParameterName = "serviceSessionEvents";

        /// <inheritdoc />
        public bool Active { get; private set; }

        /// <summary>
        /// Marks the notifications as delivered. Called by the host lifetime that receives them from Windows.
        /// </summary>
        public void Activate()
        {
            Active = true;
        }

        /// <inheritdoc />
        public event EventHandler<ServiceSessionChangedEventArgs> SessionChanged;

        /// <summary>
        /// Passes a session change on to the listeners. A listener that fails does not keep the others from hearing it.
        /// Called by the host lifetime that receives the notifications from Windows.
        /// </summary>
        public void Raise(ServiceSessionChange reason, int sessionId)
        {
            LogEnvironment.LogDebugEvent($"Session {sessionId}: {reason}.", LogSeverity.Report);
            var handlers = SessionChanged;
            if (handlers == null)
            {
                return;
            }

            var args = new ServiceSessionChangedEventArgs(reason, sessionId);
            foreach (EventHandler<ServiceSessionChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"A listener on session change {reason} of session {sessionId} failed: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }
    }
}
