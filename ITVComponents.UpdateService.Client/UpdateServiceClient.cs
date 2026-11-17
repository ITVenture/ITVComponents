using System;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.NamedPipe.Client;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Contracts;

namespace ITVComponents.UpdateService.Client
{
    /// <summary>
    /// How an application reaches its update service.
    /// </summary>
    public sealed class UpdateServiceClientOptions
    {
        /// <summary>Gets or sets the name of the service's pipe.</summary>
        public string PipeName { get; set; } = string.Empty;

        /// <summary>Gets or sets the name of the service at the hub. Default: <c>UpdateService</c>.</summary>
        public string HubServiceName { get; set; } = "UpdateService";

        /// <summary>Gets or sets the product of the application.</summary>
        public string ProductId { get; set; } = string.Empty;

        /// <summary>Gets or sets the version of the application.</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the account that must own the pipe. Default: LocalSystem - the service. Null skips the check
        /// (development).
        /// </summary>
        public SecurityIdentifier? ExpectedServiceAccount { get; set; } = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        /// <summary>Gets or sets how often to try again while the service is not reachable. Default: 30 seconds.</summary>
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// The application's side of the update service. The application does not need to know the protocol: it subscribes
    /// to the events, defers while it is busy, and ends when asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without a service (development, <c>dotnet run</c>) the client keeps trying in the background and says so once in
    /// the log; the application works on - and counts as active.
    /// </para>
    /// <para>
    /// With several sessions, only one instance is active (<see cref="Role"/>). When <see cref="RoleChanged"/> says
    /// <see cref="AgentRole.Waiting"/>, the application disconnects from the hub, releases its devices and then calls
    /// <see cref="ReportReleasedAsync"/>; when it says <see cref="AgentRole.Active"/>, it takes over. If the registration is
    /// refused with <see cref="AlreadyRunning"/>, another instance runs in the same session and this one should end.
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class UpdateServiceClient : IDisposable
    {
        private readonly UpdateServiceClientOptions options;
        private readonly object sync = new object();
        private readonly Timer reconnect;
        private NamedPipeClient? client;
        private IUpdateServiceFacade? facade;
        private bool unavailableReported;
        private bool refused;
        private bool disposed;

        static UpdateServiceClient()
        {
            UpdateContractTypes.RegisterAll((t, alias) => JsonHelper.RegisterManualType(t, alias));
        }

        public UpdateServiceClient(UpdateServiceClientOptions options)
        {
            this.options = options;
            reconnect = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Is raised when the service wants the application to end.
        /// </summary>
        public event Action<ShutdownRequest>? ShutdownRequested;

        /// <summary>
        /// Is raised when the state of the updates changes.
        /// </summary>
        public event Action<UpdateStatus>? StatusChanged;

        /// <summary>
        /// Is raised when this instance is to take over (<see cref="AgentRole.Active"/>) or to release
        /// (<see cref="AgentRole.Waiting"/>).
        /// </summary>
        public event Action<AgentRole>? RoleChanged;

        /// <summary>
        /// Gets the role of this instance. Without a service it is <see cref="AgentRole.Active"/>: nobody else decides.
        /// </summary>
        public AgentRole Role { get; private set; } = AgentRole.Active;

        /// <summary>
        /// Gets a value indicating whether the registration was refused because the application already runs in this
        /// session (see <see cref="Registration"/> for who). The instance should end.
        /// </summary>
        public bool AlreadyRunning { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the service is connected and the application registered.
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// Gets how the service saw the application at its registration.
        /// </summary>
        public AgentRegistration? Registration { get; private set; }

        /// <summary>
        /// Gets why the service refused the application, if it did. The client does not try again then: the refusal
        /// depends on the installation (product, folder), not on timing.
        /// </summary>
        public string? RefusalReason { get; private set; }

        /// <summary>
        /// Connects to the service; if it is not there, keeps trying in the background.
        /// </summary>
        public void Start()
        {
            TryConnect();
            lock (sync)
            {
                if (!disposed)
                {
                    reconnect.Change(options.ReconnectInterval, options.ReconnectInterval);
                }
            }
        }

        /// <summary>
        /// Gets the state of the updates, or null without service.
        /// </summary>
        public async Task<UpdateStatus?> GetStatusAsync()
        {
            var current = Facade();
            return current == null ? null : await Call(() => current.GetStatusAsync()).ConfigureAwait(false);
        }

        /// <summary>
        /// Asks the service to look for a new version now. Returns false without service.
        /// </summary>
        public async Task<bool> CheckNowAsync()
        {
            var current = Facade();
            if (current == null)
            {
                return false;
            }

            await Call(async () =>
            {
                await current.CheckNowAsync().ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Asks the service to wait before it ends the application. Returns false without service or when refused.
        /// </summary>
        public async Task<bool> DeferAsync(TimeSpan duration, string reason)
        {
            var current = Facade();
            return current != null && await Call(() => current.DeferAsync((int)Math.Ceiling(duration.TotalMinutes), reason)).ConfigureAwait(false);
        }

        /// <summary>
        /// Confirms that this instance has released the hub connection and the devices after being asked to.
        /// </summary>
        public async Task ReportReleasedAsync()
        {
            var current = Facade();
            if (current == null)
            {
                return;
            }

            await Call(async () =>
            {
                await current.ReportReleasedAsync().ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                reconnect.Dispose();
                Disconnect();
            }
        }

        private IUpdateServiceFacade? Facade()
        {
            lock (sync)
            {
                return facade;
            }
        }

        private async Task<T> Call<T>(Func<Task<T>> call)
        {
            try
            {
                return await call().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The update service did not answer; reconnecting: {ex.OutlineException()}", LogSeverity.Warning);
                lock (sync)
                {
                    Disconnect();
                }

                return default!;
            }
        }

        private void TryConnect()
        {
            lock (sync)
            {
                if (disposed || facade != null)
                {
                    return;
                }

                try
                {
                    client = new NamedPipeClient(new NamedPipeHubClientOptions
                    {
                        PipeName = options.PipeName,
                        ExpectedServerOwner = options.ExpectedServiceAccount
                    }, options.HubServiceName, true);
                    client.Initialize();
                    var proxy = client.CreateProxy<IUpdateServiceFacade>(UpdateContractTypes.FacadeObjectName);
                    proxy.ShutdownRequested += OnShutdownRequested;
                    proxy.StatusChanged += OnStatusChanged;
                    proxy.RoleChanged += OnRoleChanged;
                    var registration = proxy.RegisterAsync(options.ProductId, options.Version, Role == AgentRole.Active).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (!registration.Accepted)
                    {
                        LogEnvironment.LogEvent($"The update service refused the registration: {registration.Reason}",
                            registration.AlreadyRunning ? LogSeverity.Warning : LogSeverity.Error);
                        RefusalReason = registration.Reason;
                        AlreadyRunning = registration.AlreadyRunning;
                        Registration = registration;
                        Disconnect();
                        refused = true;
                        reconnect.Change(Timeout.Infinite, Timeout.Infinite);
                        return;
                    }

                    facade = proxy;
                    Registration = registration;
                    Connected = true;
                    unavailableReported = false;
                    LogEnvironment.LogEvent($"Connected to the update service (session {registration.SessionId}, process {registration.ProcessId}).", LogSeverity.Report);
                }
                catch (Exception ex)
                {
                    Disconnect();
                    if (!unavailableReported)
                    {
                        LogEnvironment.LogEvent($"No update service reachable on pipe {options.PipeName}; the application works on without it and keeps trying: {ex.Message}", LogSeverity.Warning);
                        unavailableReported = true;
                    }
                    else
                    {
                        LogEnvironment.LogDebugEvent($"Update service still not reachable: {ex.Message}", LogSeverity.Report);
                    }
                }
            }

            var accepted = Registration;
            if (accepted is { Accepted: true })
            {
                SetRole(accepted.Role);
                if (accepted.Status != null)
                {
                    OnStatusChanged(accepted.Status);
                }
            }
        }

        private void OnRoleChanged(RoleChange change)
        {
            // Every instance receives every change; only the one for this process counts.
            if (change.ProcessId != Registration?.ProcessId)
            {
                return;
            }

            SetRole(change.Role);
        }

        private void SetRole(AgentRole role)
        {
            if (Role == role)
            {
                return;
            }

            Role = role;
            LogEnvironment.LogEvent(role == AgentRole.Active ? "This instance takes over." : "This instance is asked to release and wait.", LogSeverity.Report);
            Forward(RoleChanged, role);
        }

        /// <summary>
        /// Every interval: connect if not connected, else announce the application again. The announcement is idempotent;
        /// it notices a lost connection, and a restarted service learns about the application again.
        /// </summary>
        private void Tick()
        {
            if (refused)
            {
                return;
            }

            var current = Facade();
            if (current == null)
            {
                TryConnect();
                return;
            }

            try
            {
                var registration = current.RegisterAsync(options.ProductId, options.Version, Role == AgentRole.Active).ConfigureAwait(false).GetAwaiter().GetResult();
                if (!registration.Accepted)
                {
                    LogEnvironment.LogEvent($"The update service no longer accepts the application: {registration.Reason}", LogSeverity.Error);
                }
                else
                {
                    SetRole(registration.Role);
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The connection to the update service is lost; reconnecting: {ex.Message}", LogSeverity.Warning);
                lock (sync)
                {
                    Disconnect();
                }

                TryConnect();
            }
        }

        private void Disconnect()
        {
            Connected = false;
            facade = null;
            var current = client;
            client = null;
            if (current == null)
            {
                return;
            }

            try
            {
                current.Dispose();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Closing the connection to the update service failed: {ex.OutlineException()}", LogSeverity.Warning);
            }
        }

        private void OnShutdownRequested(ShutdownRequest request)
        {
            LogEnvironment.LogEvent($"The update service asks the application to end ({request.Reason}, version {request.Version}{(request.Deadline.HasValue ? $", by {request.Deadline:u}" : string.Empty)}).", LogSeverity.Report);
            Forward(ShutdownRequested, request);
        }

        private void OnStatusChanged(UpdateStatus status)
        {
            Forward(StatusChanged, status);
        }

        private static void Forward<T>(Action<T>? handler, T value)
        {
            if (handler == null)
            {
                return;
            }

            foreach (Action<T> single in handler.GetInvocationList())
            {
                try
                {
                    single(value);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"A listener of the update client failed: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }
    }
}
