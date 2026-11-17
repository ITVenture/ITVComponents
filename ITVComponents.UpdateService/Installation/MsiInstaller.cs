using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Feed;

namespace ITVComponents.UpdateService.Installation
{
    /// <summary>
    /// How an installation ended.
    /// </summary>
    public enum InstallOutcome
    {
        /// <summary>The result is not known (yet).</summary>
        Unknown = 0,

        /// <summary>Installed.</summary>
        Succeeded = 1,

        /// <summary>Installed; Windows must restart to complete it.</summary>
        SucceededRestartRequired = 2,

        /// <summary>Another installation was running; try again later.</summary>
        Busy = 3,

        /// <summary>Cancelled.</summary>
        Cancelled = 4,

        /// <summary>Failed; the installer rolled back.</summary>
        Failed = 5
    }

    /// <summary>
    /// The result of an installation, with the code and the reason for the log.
    /// </summary>
    public sealed class InstallResult
    {
        public InstallResult(InstallOutcome outcome, int? exitCode, string description)
        {
            Outcome = outcome;
            ExitCode = exitCode;
            Description = description;
        }

        public InstallOutcome Outcome { get; }

        public int? ExitCode { get; }

        public string Description { get; }
    }

    /// <summary>
    /// Starts an installation. The service does not wait for it: an MSI that contains the service stops it.
    /// </summary>
    public interface IPackageInstaller
    {
        /// <summary>
        /// Starts the installation of a checked package, detached.
        /// </summary>
        /// <param name="packagePath">the package in the protected folder</param>
        /// <param name="package">the release</param>
        /// <param name="logPath">where the installer writes its log</param>
        /// <returns>the process of the installer</returns>
        Process Start(string packagePath, UpdatePackage package, string logPath);
    }

    /// <summary>
    /// Reads the result of an installation - from the exit code if the service saw the installer end, else from its log
    /// (after its own restart).
    /// </summary>
    public interface IInstallResultEvaluator
    {
        /// <summary>
        /// Evaluates an exit code.
        /// </summary>
        InstallResult FromExitCode(int exitCode);

        /// <summary>
        /// Evaluates the log of an installation that ended while the service was not running. Returns
        /// <see cref="InstallOutcome.Unknown"/> while the log has no result yet.
        /// </summary>
        InstallResult FromLog(string logPath);

        /// <summary>
        /// Waits until an installer started earlier has ended (and its log is complete), then reads its result.
        /// </summary>
        Task<InstallResult> AwaitAsync(PendingInstall pending, TimeSpan timeout, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Installs an MSI quietly with a verbose log: <c>msiexec /i "…" /qn /norestart /l*v "…"</c>. A failed installation is
    /// rolled back by Windows Installer itself; the service copies nothing back.
    /// </summary>
    public sealed class MsiInstaller : IPackageInstaller
    {
        /// <inheritdoc />
        public Process Start(string packagePath, UpdatePackage package, string logPath)
        {
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var start = new ProcessStartInfo(Path.Combine(system, "msiexec.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("/i");
            start.ArgumentList.Add(packagePath);
            start.ArgumentList.Add("/qn");
            start.ArgumentList.Add("/norestart");
            start.ArgumentList.Add("/l*v");
            start.ArgumentList.Add(logPath);
            return Process.Start(start) ?? throw new InvalidOperationException("msiexec could not be started.");
        }
    }

    /// <summary>
    /// The exit codes of Windows Installer.
    /// </summary>
    public sealed class MsiInstallResultEvaluator : IInstallResultEvaluator
    {
        private static readonly Regex ResultLine = new Regex(@"MainEngineThread is returning (\d+)", RegexOptions.Compiled);

        /// <inheritdoc />
        public InstallResult FromExitCode(int exitCode)
        {
            return exitCode switch
            {
                0 => new InstallResult(InstallOutcome.Succeeded, exitCode, "installed"),
                1641 => new InstallResult(InstallOutcome.SucceededRestartRequired, exitCode, "installed, Windows restart initiated"),
                3010 => new InstallResult(InstallOutcome.SucceededRestartRequired, exitCode, "installed, Windows must restart"),
                1618 => new InstallResult(InstallOutcome.Busy, exitCode, "another installation is running"),
                1602 => new InstallResult(InstallOutcome.Cancelled, exitCode, "cancelled"),
                1603 => new InstallResult(InstallOutcome.Failed, exitCode, "fatal error during installation, rolled back"),
                _ => new InstallResult(InstallOutcome.Failed, exitCode, $"failed with exit code {exitCode}")
            };
        }

        /// <inheritdoc />
        public InstallResult FromLog(string logPath)
        {
            if (!File.Exists(logPath))
            {
                return new InstallResult(InstallOutcome.Unknown, null, $"the installation log {logPath} does not exist");
            }

            string text;
            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true))
            {
                text = reader.ReadToEnd();
            }

            var matches = ResultLine.Matches(text);
            if (matches.Count == 0)
            {
                return new InstallResult(InstallOutcome.Unknown, null, "the installation log has no result yet");
            }

            // The last engine thread is the one of the whole installation (custom actions run nested ones before it).
            return FromExitCode(int.Parse(matches[^1].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Waits until an installer started earlier has ended (and its log is complete), then reads its result.
        /// </summary>
        /// <param name="pending">the installation from the state</param>
        /// <param name="timeout">how long to wait at most</param>
        /// <param name="cancellationToken">ends the wait</param>
        public async Task<InstallResult> AwaitAsync(PendingInstall pending, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var until = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < until)
            {
                if (!InstallerRunning(pending))
                {
                    var result = FromLog(pending.LogPath);
                    if (result.Outcome != InstallOutcome.Unknown)
                    {
                        return result;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }

            var last = FromLog(pending.LogPath);
            return last.Outcome != InstallOutcome.Unknown
                ? last
                : new InstallResult(InstallOutcome.Failed, null, $"no result in {pending.LogPath} after {timeout}");
        }

        private static bool InstallerRunning(PendingInstall pending)
        {
            try
            {
                using var process = Process.GetProcessById(pending.InstallerProcessId);

                // The id may belong to another process by now: only a process started before the installation counts.
                return !process.HasExited && process.StartTime.ToUniversalTime() <= pending.StartedUtc.UtcDateTime.AddSeconds(5)
                                          && string.Equals(process.ProcessName, "msiexec", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException ex)
            {
                // GetProcessById: no such process any more - the installer has ended.
                LogEnvironment.LogDebugEvent($"The installer {pending.InstallerProcessId} has ended ({ex.Message}).", LogSeverity.Report);
                return false;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Whether the installer {pending.InstallerProcessId} still runs could not be determined; its log decides: {ex.OutlineException()}", LogSeverity.Warning);
                return false;
            }
        }
    }
}
