using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Native;

namespace ITVComponents.UpdateService.Agents
{
    /// <summary>
    /// Starts, ends and identifies the application the service keeps up to date.
    /// </summary>
    public interface IApplicationControl
    {
        /// <summary>
        /// Gets the installed version of the application.
        /// </summary>
        Version InstalledVersion();

        /// <summary>
        /// Starts the application in a Windows session, as the user logged on there.
        /// </summary>
        void Launch(int sessionId);

        /// <summary>
        /// Ends an application that did not end by itself in time.
        /// </summary>
        void Terminate(RegisteredAgent agent);
    }

    /// <summary>
    /// The application as configured: <see cref="UpdateServiceOptions.ApplicationPath"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class ApplicationControl : IApplicationControl
    {
        private readonly UpdateServiceOptions options;

        public ApplicationControl(UpdateServiceOptions options)
        {
            this.options = options;
        }

        /// <inheritdoc />
        public Version InstalledVersion()
        {
            if (!File.Exists(options.ApplicationPath))
            {
                LogEnvironment.LogEvent($"The application {options.ApplicationPath} is not installed; every version counts as newer.", LogSeverity.Warning);
                return new Version(0, 0);
            }

            var info = FileVersionInfo.GetVersionInfo(options.ApplicationPath);
            if (VersionText.TryParse(info.ProductVersion, out var version) || VersionText.TryParse(info.FileVersion, out version))
            {
                return version;
            }

            LogEnvironment.LogEvent($"The application {options.ApplicationPath} has no readable version; every version counts as newer.", LogSeverity.Warning);
            return new Version(0, 0);
        }

        /// <inheritdoc />
        public void Launch(int sessionId)
        {
            var pid = UpdateNativeMethods.StartInSession(sessionId, options.ApplicationPath, options.ApplicationArguments, Path.GetDirectoryName(options.ApplicationPath));
            LogEnvironment.LogEvent($"Application started again in session {sessionId} (process {pid}).", LogSeverity.Report);
        }

        /// <inheritdoc />
        public void Terminate(RegisteredAgent agent)
        {
            try
            {
                using var process = Process.GetProcessById(agent.ProcessId);
                process.Kill();
                LogEnvironment.LogEvent($"Application {agent} did not end in time and was ended.", LogSeverity.Warning);
            }
            catch (ArgumentException ex)
            {
                LogEnvironment.LogDebugEvent($"Application {agent} had ended already ({ex.Message}).", LogSeverity.Report);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Application {agent} could not be ended: {ex.OutlineException()}", LogSeverity.Error);
            }
        }
    }
}
