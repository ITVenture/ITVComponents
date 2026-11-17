using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.UpdateService.Installation
{
    /// <summary>
    /// What the service has to remember across its own restart - above all, that an installation it started is running.
    /// </summary>
    public sealed class InstallState
    {
        /// <summary>Gets or sets the installation that was started and not yet evaluated.</summary>
        public PendingInstall? Pending { get; set; }

        /// <summary>Gets or sets the version the deferral bookkeeping is about.</summary>
        public string? DeferralVersion { get; set; }

        /// <summary>Gets or sets until when the application asked to wait.</summary>
        public DateTimeOffset? DeferredUntil { get; set; }

        /// <summary>
        /// Gets or sets the version that was refused or failed. It is not tried again until a newer version appears or
        /// someone asks to check now - otherwise a broken release would be installed and rolled back over and over.
        /// </summary>
        public string? FailedVersion { get; set; }

        /// <summary>Gets or sets the outcome of the last installation, for the state shown.</summary>
        public string? LastResult { get; set; }

        /// <summary>Gets or sets a value indicating whether Windows must restart to complete the last installation.</summary>
        public bool RestartRequired { get; set; }

        /// <summary>Gets or sets since when Windows must restart; a start of Windows after it clears the flag.</summary>
        public DateTimeOffset? RestartRequiredSince { get; set; }

        /// <summary>Gets or sets the modules assigned to this machine (ids).</summary>
        public List<string> AssignedModules { get; set; } = new List<string>();

        /// <summary>Gets or sets the installed modules, by id.</summary>
        public Dictionary<string, InstalledModule> Modules { get; set; } = new Dictionary<string, InstalledModule>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets or sets the module versions that failed (id to version); they are not tried again.</summary>
        public Dictionary<string, ModuleFailure> FailedModules { get; set; } = new Dictionary<string, ModuleFailure>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets or sets the modules switched to a new version and not yet confirmed by the application.</summary>
        public ModuleActivation? ModuleActivation { get; set; }
    }

    /// <summary>
    /// A module as installed.
    /// </summary>
    public sealed class InstalledModule
    {
        public string Version { get; set; } = string.Empty;

        /// <summary>Gets or sets the versions of the base package the installed version works with (NuGet range), or null for any.</summary>
        public string? Requires { get; set; }
    }

    /// <summary>
    /// A module version that failed, and why.
    /// </summary>
    public sealed class ModuleFailure
    {
        public string Version { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// A module that was unpacked next to the active one and waits to be switched to.
    /// </summary>
    public sealed class StagedModule
    {
        public string Id { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string? Requires { get; set; }
    }

    /// <summary>
    /// Modules switched to new versions; their former versions are kept until the application confirms that it loaded them.
    /// </summary>
    public sealed class ModuleActivation
    {
        public List<ActivatedModule> Modules { get; set; } = new List<ActivatedModule>();

        public DateTimeOffset ActivatedUtc { get; set; }

        /// <summary>Gets or sets a value indicating whether the application has connected since the switch.</summary>
        public bool ApplicationSeen { get; set; }

        /// <summary>Gets or sets a value indicating whether the switch is to be undone (load failure or crash).</summary>
        public bool RollbackRequested { get; set; }

        /// <summary>Gets or sets why.</summary>
        public string? RollbackReason { get; set; }

        /// <summary>Gets or sets the sessions the application ran in before the switch, to start it again after an undo.</summary>
        public List<int> Sessions { get; set; } = new List<int>();
    }

    /// <summary>
    /// One switched module.
    /// </summary>
    public sealed class ActivatedModule
    {
        public string Id { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string? Requires { get; set; }

        /// <summary>Gets or sets the version before the switch, or null if the module was new.</summary>
        public InstalledModule? Previous { get; set; }
    }

    /// <summary>
    /// An installation that was started.
    /// </summary>
    public sealed class PendingInstall
    {
        public string PackageId { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string LogPath { get; set; } = string.Empty;

        public int InstallerProcessId { get; set; }

        public DateTimeOffset StartedUtc { get; set; }

        /// <summary>Gets or sets the sessions in which the application ran and is to be started again.</summary>
        public List<int> SessionsToRestart { get; set; } = new List<int>();

        /// <summary>Gets or sets the modules prepared for this installation; they are switched to only if it succeeds.</summary>
        public List<StagedModule> StagedModules { get; set; } = new List<StagedModule>();
    }

    /// <summary>
    /// Keeps the <see cref="InstallState"/> in the service's protected folder. Written before every step that may end the
    /// service, so that the restarted service finds the same state.
    /// </summary>
    public sealed class InstallStateStore
    {
        private readonly string path;
        private readonly object sync = new object();

        public InstallStateStore(string directory)
        {
            path = System.IO.Path.Combine(directory, "state.json");
        }

        /// <summary>
        /// Reads the state; a missing file is an empty state, an unreadable one too - with an error in the log.
        /// </summary>
        public InstallState Load()
        {
            lock (sync)
            {
                if (!File.Exists(path))
                {
                    return new InstallState();
                }

                try
                {
                    return JsonSerializer.Deserialize<InstallState>(File.ReadAllText(path)) ?? new InstallState();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"The state of the update service ({path}) could not be read; it starts over: {ex.OutlineException()}", LogSeverity.Error);
                    return new InstallState();
                }
            }
        }

        /// <summary>
        /// Writes the state through a temporary file, so that an interrupted write never leaves half a state.
        /// </summary>
        public void Save(InstallState state)
        {
            lock (sync)
            {
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporary, path, true);
            }
        }
    }
}
