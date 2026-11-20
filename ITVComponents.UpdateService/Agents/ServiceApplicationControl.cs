using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Native;

namespace ITVComponents.UpdateService.Agents
{
    /// <summary>
    /// The state of a Windows service as the service control manager reports it.
    /// </summary>
    public enum WindowsServiceRunState
    {
        /// <summary>A state the service does not normally stay in (paused, continuing, ...).</summary>
        Other = 0,

        /// <summary>SERVICE_STOPPED.</summary>
        Stopped = 1,

        /// <summary>SERVICE_START_PENDING.</summary>
        StartPending = 2,

        /// <summary>SERVICE_STOP_PENDING.</summary>
        StopPending = 3,

        /// <summary>SERVICE_RUNNING.</summary>
        Running = 4
    }

    /// <summary>
    /// A Windows service as the service control manager reports it.
    /// </summary>
    public sealed class WindowsServiceInfo
    {
        /// <summary>Gets the state.</summary>
        public WindowsServiceRunState State { get; init; }

        /// <summary>Gets the process of the service; 0 when it does not run.</summary>
        public int ProcessId { get; init; }

        /// <summary>Gets a value indicating whether the service has a process of its own (not a shared host).</summary>
        public bool OwnProcess { get; init; }
    }

    /// <summary>
    /// What the update service asks of the service control manager for a target of the kind <c>WindowsService</c>.
    /// </summary>
    public interface IWindowsServices
    {
        /// <summary>
        /// Reads a service; null when it is not installed.
        /// </summary>
        WindowsServiceInfo? Query(string serviceName);

        /// <summary>
        /// Starts a service; false when it runs already.
        /// </summary>
        bool Start(string serviceName);

        /// <summary>
        /// Asks a service to stop; false when it does not run. Returns at once.
        /// </summary>
        bool Stop(string serviceName);
    }

    /// <summary>
    /// The service control manager of this machine. The update service runs as LocalSystem; it may query, start and stop
    /// any service.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsServices : IWindowsServices
    {
        /// <inheritdoc />
        public WindowsServiceInfo? Query(string serviceName)
        {
            var status = UpdateNativeMethods.QueryService(serviceName);
            if (status == null)
            {
                return null;
            }

            var (state, processId, serviceType) = status.Value;
            return new WindowsServiceInfo
            {
                State = state is >= 1 and <= 4 ? (WindowsServiceRunState)state : WindowsServiceRunState.Other,
                ProcessId = processId,
                OwnProcess = (serviceType & UpdateNativeMethods.ServiceWin32OwnProcess) != 0
            };
        }

        /// <inheritdoc />
        public bool Start(string serviceName) => UpdateNativeMethods.StartWindowsService(serviceName);

        /// <inheritdoc />
        public bool Stop(string serviceName) => UpdateNativeMethods.StopWindowsService(serviceName);
    }

    /// <summary>
    /// A target of the kind <c>WindowsService</c>: started with the service control manager, ended by stopping it. It has
    /// no sessions; the session given to <see cref="Launch"/> is ignored.
    /// </summary>
    public sealed class ServiceApplicationControl : IApplicationControl
    {
        private readonly UpdateServiceOptions options;
        private readonly IWindowsServices services;
        private readonly IApplicationControl files;
        private readonly TimeSpan stopTimeout;
        private readonly Action<int> kill;

        /// <param name="options">the options of the target; <see cref="UpdateServiceOptions.ServiceName"/> is the service</param>
        /// <param name="services">the service control manager</param>
        /// <param name="files">reads the installed version from <see cref="UpdateServiceOptions.ApplicationPath"/></param>
        /// <param name="stopTimeout">how long a stopped service may take before its process is ended; default 60 seconds</param>
        /// <param name="kill">ends a process by id; default <see cref="Process.Kill()"/></param>
        public ServiceApplicationControl(UpdateServiceOptions options, IWindowsServices services, IApplicationControl files, TimeSpan? stopTimeout = null, Action<int>? kill = null)
        {
            this.options = options;
            this.services = services;
            this.files = files;
            this.stopTimeout = stopTimeout ?? TimeSpan.FromSeconds(60);
            this.kill = kill ?? KillProcess;
        }

        private string ServiceName => options.ServiceName ?? string.Empty;

        /// <inheritdoc />
        public Version InstalledVersion() => files.InstalledVersion();

        /// <inheritdoc />
        public void Launch(int sessionId)
        {
            var info = services.Query(ServiceName);
            if (info == null)
            {
                throw new InvalidOperationException($"The service {ServiceName} of target {options.TargetId} is not installed.");
            }

            if (info.State is WindowsServiceRunState.Running or WindowsServiceRunState.StartPending)
            {
                // The installer often starts the service itself.
                LogEnvironment.LogDebugEvent($"The service {ServiceName} runs already ({info.State}).", LogSeverity.Report);
                return;
            }

            services.Start(ServiceName);
            LogEnvironment.LogEvent($"The service {ServiceName} was started again.", LogSeverity.Report);
        }

        /// <inheritdoc />
        public void Terminate(RegisteredAgent agent)
        {
            try
            {
                if (!services.Stop(ServiceName))
                {
                    LogEnvironment.LogDebugEvent($"The service {ServiceName} ({agent}) did not run any more.", LogSeverity.Report);
                    return;
                }

                LogEnvironment.LogEvent($"The service {ServiceName} ({agent}) did not end in time and is stopped.", LogSeverity.Warning);
                var until = DateTime.UtcNow + stopTimeout;
                while (DateTime.UtcNow < until)
                {
                    if (services.Query(ServiceName)?.State is null or WindowsServiceRunState.Stopped)
                    {
                        return;
                    }

                    Thread.Sleep(500);
                }

                LogEnvironment.LogEvent($"The service {ServiceName} did not stop within {stopTimeout.TotalSeconds:0} seconds; its process {agent.ProcessId} is ended.", LogSeverity.Warning);
                kill(agent.ProcessId);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The service {ServiceName} ({agent}) could not be stopped: {ex.OutlineException()}", LogSeverity.Error);
            }
        }

        private static void KillProcess(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Kill();
            }
            catch (ArgumentException ex)
            {
                LogEnvironment.LogDebugEvent($"Process {processId} had ended already ({ex.Message}).", LogSeverity.Report);
            }
        }
    }
}
