using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Packages;

namespace ITVComponents.UpdateService.Modules
{
    /// <summary>
    /// What is to happen to the modules in one pass.
    /// </summary>
    public sealed class ModulePlan
    {
        /// <summary>A plan without work.</summary>
        public static ModulePlan Empty => new ModulePlan();

        /// <summary>Gets the module releases to install (new or other version).</summary>
        public List<UpdatePackage> Installs { get; } = new List<UpdatePackage>();

        /// <summary>Gets the installed modules no longer assigned.</summary>
        public List<string> Removals { get; } = new List<string>();

        /// <summary>Gets the assigned modules without a version for the target version of the application.</summary>
        public List<string> Missing { get; } = new List<string>();

        /// <summary>Gets a value indicating whether anything is to be done.</summary>
        public bool HasWork => Installs.Count > 0 || Removals.Count > 0;
    }

    /// <summary>
    /// A module could not be prepared.
    /// </summary>
    public sealed class ModuleStagingException : Exception
    {
        public ModuleStagingException(string moduleId, string message, Exception? inner = null)
            : base(message, inner)
        {
            ModuleId = moduleId;
        }

        /// <summary>Gets the module.</summary>
        public string ModuleId { get; }
    }

    /// <summary>
    /// The modules of the application (issue update service, P4, M1, M3): which are assigned, which version fits the
    /// application, preparing them, switching to them while the application is down, and switching back when the
    /// application cannot load them.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>M1: a newer base package is installed only if every assigned module has a version for it; the modules come along
    /// in the same pass and are switched to before the application starts.</item>
    /// <item>M3: the former version stays as <c>&lt;id&gt;.old</c> until the application reports that it loaded the new one.
    /// A load failure, or the application leaving within <see cref="UpdateServiceOptions.ModuleConfirmMinutes"/> after
    /// its start without a report (a crash), switches back; the failed version is not tried again.</item>
    /// </list>
    /// </remarks>
    public sealed class ModuleManager
    {
        private readonly UpdateServiceOptions options;
        private readonly IPackageDownloader downloader;
        private readonly ModuleFiles files;
        private readonly InstallStateStore store;
        private readonly ModulePluginRegistry? plugins;
        private readonly TimeProvider clock;
        private readonly object sync = new object();

        /// <param name="options">the configuration of the service</param>
        /// <param name="downloader">fetches and checks the module packages</param>
        /// <param name="files">the module folders</param>
        /// <param name="store">the state of the service</param>
        /// <param name="clock">the clock; the system clock if omitted</param>
        /// <param name="plugins">the plugin instances of the modules (<c>plugins.json</c>); without it no instance is kept</param>
        public ModuleManager(UpdateServiceOptions options, IPackageDownloader downloader, ModuleFiles files, InstallStateStore store, TimeProvider? clock = null, ModulePluginRegistry? plugins = null)
        {
            this.options = options;
            this.downloader = downloader;
            this.files = files;
            this.store = store;
            this.plugins = plugins;
            this.clock = clock ?? TimeProvider.System;
        }

        /// <summary>
        /// Brings the plugin instances in line with every installed module - at the start of the service, for modules
        /// installed before there were plugin instances, and after anything that went wrong while switching. Never throws.
        /// </summary>
        public void ReconcilePlugins()
        {
            if (plugins == null)
            {
                return;
            }

            lock (sync)
            {
                var state = store.Load();
                foreach (var id in state.Modules.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    plugins.Reconcile(id);
                }
            }
        }

        /// <summary>
        /// Takes over the assignment of this machine.
        /// </summary>
        /// <returns>whether it changed</returns>
        public bool Assign(IEnumerable<string> moduleIds)
        {
            lock (sync)
            {
                var ids = moduleIds.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(i => i, StringComparer.OrdinalIgnoreCase).ToList();
                var state = store.Load();
                if (state.AssignedModules.SequenceEqual(ids, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }

                LogEnvironment.LogEvent($"Modules assigned to this machine: {(ids.Count == 0 ? "none" : string.Join(", ", ids))}.", LogSeverity.Report);
                state.AssignedModules = ids;
                store.Save(state);
                return true;
            }
        }

        /// <summary>
        /// Plans the modules for a version of the application.
        /// </summary>
        /// <param name="feed">the manifest; without one nothing is planned</param>
        /// <param name="applicationVersion">the version of the application the modules must fit</param>
        /// <param name="state">the state</param>
        public ModulePlan Plan(FeedResult? feed, Version applicationVersion, InstallState state)
        {
            var plan = new ModulePlan();
            if (feed == null)
            {
                return plan;
            }

            foreach (var id in state.AssignedModules)
            {
                state.Modules.TryGetValue(id, out var installed);
                state.FailedModules.TryGetValue(id, out var failed);
                var best = feed.Feed.Packages
                    .Where(p => string.Equals(p.Kind, PackageKinds.Module, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                    .Where(p => failed == null || !string.Equals(failed.Version, p.Version, StringComparison.OrdinalIgnoreCase))
                    .Select(p => new { Package = p, Version = VersionText.TryParse(p.Version, out var v) ? v : null })
                    .Where(p => p.Version != null && Fits(RequiresOf(p.Package), applicationVersion, p.Package))
                    .OrderByDescending(p => p.Version)
                    .FirstOrDefault();
                var installedFits = installed != null && Fits(installed.Requires, applicationVersion, null);
                if (best == null)
                {
                    if (!installedFits)
                    {
                        plan.Missing.Add(id);
                    }

                    continue;
                }

                if (installed == null || !VersionText.TryParse(installed.Version, out var current) || !installedFits)
                {
                    plan.Installs.Add(best.Package);
                    continue;
                }

                if (VersionText.Compare(best.Version!, current) > 0)
                {
                    plan.Installs.Add(best.Package);
                }
            }

            foreach (var id in state.Modules.Keys.Where(k => !state.AssignedModules.Contains(k, StringComparer.OrdinalIgnoreCase)))
            {
                plan.Removals.Add(id);
            }

            return plan;
        }

        /// <summary>
        /// Downloads, checks and unpacks every module to install next to its active version. Fails as a whole: one module
        /// that cannot be prepared discards the others, so that no pass is half done.
        /// </summary>
        /// <exception cref="ModuleStagingException">when a module cannot be prepared; its version is recorded as failed</exception>
        public async Task<List<StagedModule>> StageAsync(FeedResult feed, ModulePlan plan, CancellationToken cancellationToken)
        {
            var staged = new List<StagedModule>();
            var infrastructureNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in plan.Installs)
            {
                try
                {
                    var path = await downloader.DownloadAsync(package, feed.ResolvePackageUri(package, options.AllowInsecureFeed), cancellationToken).ConfigureAwait(false);
                    files.Stage(path, package);
                    try
                    {
                        // C.1: a version that offers its plugins wrongly, or would leave a template instance without its
                        // type, is refused before anything is switched.
                        plugins?.CheckPrepared(package.Id!, package.Version!, infrastructureNames);
                    }
                    catch
                    {
                        files.Discard(package.Id!);
                        throw;
                    }

                    staged.Add(new StagedModule { Id = package.Id!, Version = package.Version!, Requires = RequiresOf(package) });
                    LogEnvironment.LogEvent($"Module {package.Id} {package.Version} is prepared.", LogSeverity.Report);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogEnvironment.LogEvent($"Module {package.Id} {package.Version} could not be prepared; the pass is not installed: {ex.OutlineException()}", LogSeverity.Error);
                    Discard(staged);
                    if (ex is InvalidDataException)
                    {
                        RecordFailure(package.Id!, package.Version!, ex.Message);
                    }

                    throw new ModuleStagingException(package.Id!, $"Module {package.Id} {package.Version}: {ex.Message}", ex);
                }
            }

            return staged;
        }

        /// <summary>
        /// Removes the modules no longer assigned. Only while the application is down.
        /// </summary>
        public void ApplyRemovals(ModulePlan plan, InstallState state)
        {
            foreach (var id in plan.Removals)
            {
                files.Remove(id);
                state.Modules.Remove(id);
                plugins?.RemoveModule(id);
                LogEnvironment.LogEvent($"Module {id} is no longer assigned and was removed.", LogSeverity.Report);
            }
        }

        /// <summary>
        /// Switches to the prepared modules. Only while the application is down. The former versions are kept until the
        /// application confirms.
        /// </summary>
        public void Activate(IReadOnlyCollection<StagedModule> staged, InstallState state, IEnumerable<int> sessions)
        {
            if (staged.Count == 0)
            {
                return;
            }

            var activation = new ModuleActivation { ActivatedUtc = clock.GetUtcNow(), Sessions = sessions.ToList() };
            foreach (var module in staged)
            {
                state.Modules.TryGetValue(module.Id, out var previous);
                files.Activate(module.Id);
                state.Modules[module.Id] = new InstalledModule { Version = module.Version, Requires = module.Requires };
                plugins?.Reconcile(module.Id);
                activation.Modules.Add(new ActivatedModule { Id = module.Id, Version = module.Version, Requires = module.Requires, Previous = previous });
                LogEnvironment.LogEvent($"Module {module.Id} switched to {module.Version}{(previous != null ? $" (from {previous.Version}, kept until the application confirms)" : string.Empty)}.", LogSeverity.Report);
            }

            state.ModuleActivation = activation;
        }

        /// <summary>
        /// Throws prepared modules away (the base package failed, or another module could not be prepared).
        /// </summary>
        public void Discard(IEnumerable<StagedModule> staged)
        {
            foreach (var module in staged)
            {
                try
                {
                    files.Discard(module.Id);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"The prepared module {module.Id} could not be thrown away: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        /// <summary>
        /// The application reports which modules it loaded.
        /// </summary>
        public void Report(IEnumerable<ModuleLoadReport> reports)
        {
            lock (sync)
            {
                var state = store.Load();
                var list = reports.ToList();
                foreach (var failed in list.Where(r => !r.Loaded))
                {
                    LogEnvironment.LogEvent($"The application could not load module {failed.ModuleId}: {failed.Error}", LogSeverity.Error);
                }

                var activation = state.ModuleActivation;
                if (activation == null)
                {
                    return;
                }

                var failedSwitched = activation.Modules.Where(m => list.Any(r => !r.Loaded && string.Equals(r.ModuleId, m.Id, StringComparison.OrdinalIgnoreCase))).ToList();
                if (failedSwitched.Count > 0)
                {
                    activation.RollbackRequested = true;
                    activation.RollbackReason = string.Join("; ", failedSwitched.Select(m => $"{m.Id} {m.Version}: {list.First(r => string.Equals(r.ModuleId, m.Id, StringComparison.OrdinalIgnoreCase)).Error}"));
                    store.Save(state);
                    return;
                }

                var confirmed = activation.Modules.All(m => list.Any(r => r.Loaded && string.Equals(r.ModuleId, m.Id, StringComparison.OrdinalIgnoreCase)));
                if (!confirmed)
                {
                    LogEnvironment.LogDebugEvent("The application reported its modules, but not all switched ones; waiting for the rest.", LogSeverity.Report);
                    return;
                }

                foreach (var module in activation.Modules)
                {
                    try
                    {
                        files.Commit(module.Id);
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"The former version of module {module.Id} could not be deleted: {ex.OutlineException()}", LogSeverity.Warning);
                    }
                }

                LogEnvironment.LogEvent($"The application loaded the new modules ({string.Join(", ", activation.Modules.Select(m => $"{m.Id} {m.Version}"))}); the former versions are deleted.", LogSeverity.Report);
                state.ModuleActivation = null;
                store.Save(state);
            }
        }

        /// <summary>
        /// The application connected.
        /// </summary>
        public void ApplicationSeen()
        {
            lock (sync)
            {
                var state = store.Load();
                if (state.ModuleActivation is { ApplicationSeen: false } activation)
                {
                    activation.ApplicationSeen = true;
                    store.Save(state);
                }
            }
        }

        /// <summary>
        /// The application left. Shortly after a switch and without a report, that counts as a crash.
        /// </summary>
        public void ApplicationLeft()
        {
            lock (sync)
            {
                var state = store.Load();
                var activation = state.ModuleActivation;
                if (activation is not { ApplicationSeen: true, RollbackRequested: false })
                {
                    return;
                }

                if (clock.GetUtcNow() - activation.ActivatedUtc <= TimeSpan.FromMinutes(Math.Max(1, options.ModuleConfirmMinutes)))
                {
                    LogEnvironment.LogEvent("The application ended shortly after switching modules, without reporting them loaded; this counts as a crash.", LogSeverity.Error);
                    activation.RollbackRequested = true;
                    activation.RollbackReason = "the application ended right after the switch without confirming the modules";
                    store.Save(state);
                }
            }
        }

        /// <summary>
        /// Indicates whether a switch is to be undone.
        /// </summary>
        public bool RollbackPending(InstallState state) => state.ModuleActivation?.RollbackRequested == true;

        /// <summary>
        /// Undoes the last switch. Only while the application is down.
        /// </summary>
        /// <returns>the sessions the application ran in, to start it again</returns>
        public List<int> Rollback(InstallState state)
        {
            var activation = state.ModuleActivation!;
            foreach (var module in activation.Modules)
            {
                files.Rollback(module.Id);
                // Back to the instances of the former version - or none, if the module had none before.
                plugins?.Reconcile(module.Id);
                if (module.Previous != null)
                {
                    state.Modules[module.Id] = module.Previous;
                }
                else
                {
                    state.Modules.Remove(module.Id);
                }

                state.FailedModules[module.Id] = new ModuleFailure { Version = module.Version, Reason = activation.RollbackReason ?? "switched back" };
                LogEnvironment.LogEvent($"Module {module.Id} {module.Version} switched back{(module.Previous != null ? $" to {module.Previous.Version}" : " (removed)")}: {activation.RollbackReason}. It is tried again with a newer version.", LogSeverity.Error);
            }

            state.ModuleActivation = null;
            return activation.Sessions;
        }

        /// <summary>
        /// Gets the state of the modules for the application to show.
        /// </summary>
        public ModuleState[] States(InstallState state)
        {
            return state.AssignedModules.Union(state.Modules.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(i => i, StringComparer.OrdinalIgnoreCase)
                .Select(id => new ModuleState
                {
                    Id = id,
                    InstalledVersion = state.Modules.TryGetValue(id, out var m) ? m.Version : null,
                    Assigned = state.AssignedModules.Contains(id, StringComparer.OrdinalIgnoreCase),
                    Error = state.FailedModules.TryGetValue(id, out var f) ? $"{f.Version}: {f.Reason}" : null
                })
                .ToArray();
        }

        private void RecordFailure(string id, string version, string reason)
        {
            lock (sync)
            {
                var state = store.Load();
                state.FailedModules[id] = new ModuleFailure { Version = version, Reason = reason };
                store.Save(state);
            }
        }

        private string? RequiresOf(UpdatePackage package)
        {
            return package.Requires?.FirstOrDefault(r => string.Equals(r.Key, options.ProductId, StringComparison.OrdinalIgnoreCase)).Value;
        }

        private static bool Fits(string? range, Version applicationVersion, UpdatePackage? package)
        {
            if (string.IsNullOrWhiteSpace(range))
            {
                return true;
            }

            try
            {
                return VersionRange.Parse(range).Includes(applicationVersion);
            }
            catch (FormatException ex)
            {
                LogEnvironment.LogEvent($"The version range '{range}'{(package != null ? $" of module {package.Id} {package.Version}" : string.Empty)} is not readable; it counts as not fitting: {ex.Message}", LogSeverity.Error);
                return false;
            }
        }
    }
}
