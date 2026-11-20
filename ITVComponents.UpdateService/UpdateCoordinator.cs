using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Modules;
using ITVComponents.UpdateService.Packages;

namespace ITVComponents.UpdateService
{
    /// <summary>
    /// The course of an update: look for a new version, fetch and check it, let the application end, install, start the
    /// application again. One step at a time, on one worker; every decision goes to the log.
    /// </summary>
    /// <remarks>
    /// When the update is installed:
    /// <list type="bullet">
    /// <item>no application connected (nobody logged on, the last one ended): right away;</item>
    /// <item>application connected, normal update: it is asked to end when idle; its connection ending is the signal;</item>
    /// <item>urgent (flag in the feed, version below the minimum, or the longest deferral is used up): it is asked to end
    /// within a deadline, after which the service ends it.</item>
    /// </list>
    /// </remarks>
    public sealed class UpdateCoordinator : IDisposable
    {
        private static readonly TimeSpan ResumeTimeout = TimeSpan.FromMinutes(30);

        private readonly UpdateServiceOptions options;
        private readonly IUpdateFeedClient feedClient;
        private readonly IPackageDownloader downloader;
        private readonly IPackageVerifier verifier;
        private readonly IPackageInstaller installer;
        private readonly IInstallResultEvaluator evaluator;
        private readonly InstallStateStore stateStore;
        private readonly AgentRegistry registry;
        private readonly IApplicationControl application;
        private readonly TimeProvider clock;
        private readonly string logDirectory;
        private readonly ModuleManager? modules;
        private readonly SemaphoreSlim? installGate;
        private readonly string tag;
        private readonly SemaphoreSlim wake = new SemaphoreSlim(0);
        private readonly object sync = new object();
        private CancellationTokenSource? stopping;
        private Task? worker;

        private FeedResult? feed;
        private Candidate? candidate;
        private ModulePlan modulePlan = ModulePlan.Empty;
        private bool rollbackNeeded;
        private DateTimeOffset nextCheck = DateTimeOffset.MinValue;
        private DateTimeOffset? lastCheck;
        private DateTimeOffset? shutdownRequested;
        private DateTimeOffset? shutdownDeadline;
        private DateTimeOffset? retryAfter;
        private List<int> sessionsToRestart = new List<int>();
        private bool checkRequested;
        private UpdateStatus status = new UpdateStatus { Phase = UpdatePhase.Unknown };
        private InstallGroup? group;
        private Dictionary<UpdateCoordinator, ModulePlan> peerPlans = new Dictionary<UpdateCoordinator, ModulePlan>();

        public UpdateCoordinator(UpdateServiceOptions options, IUpdateFeedClient feedClient, IPackageDownloader downloader, IPackageVerifier verifier,
            IPackageInstaller installer, IInstallResultEvaluator evaluator, InstallStateStore stateStore, AgentRegistry registry,
            IApplicationControl application, string logDirectory, TimeProvider? clock = null, ModuleManager? modules = null,
            SemaphoreSlim? installGate = null)
        {
            this.modules = modules;
            this.installGate = installGate;
            tag = string.Equals(options.TargetId, UpdateServiceOptions.DefaultTargetId, StringComparison.OrdinalIgnoreCase) ? string.Empty : $"[{options.TargetId}] ";
            status.TargetId = options.TargetId;
            this.options = options;
            this.feedClient = feedClient;
            this.downloader = downloader;
            this.verifier = verifier;
            this.installer = installer;
            this.evaluator = evaluator;
            this.stateStore = stateStore;
            this.registry = registry;
            this.application = application;
            this.logDirectory = logDirectory;
            this.clock = clock ?? TimeProvider.System;
            registry.AgentLeft += (_, _) =>
            {
                bool askedToEnd;
                lock (sync)
                {
                    askedToEnd = shutdownRequested != null;
                }

                // An application that ends because the service asked it to has not crashed.
                if (!askedToEnd)
                {
                    modules?.ApplicationLeft();
                }

                Wake();
            };
            registry.AgentRegistered += (_, _) => modules?.ApplicationSeen();
        }

        /// <summary>
        /// Takes over the modules assigned to this machine.
        /// </summary>
        public void AssignModules(IEnumerable<string> moduleIds)
        {
            if (modules == null)
            {
                LogEnvironment.LogEvent($"{tag}Modules were assigned, but this update service runs without modules; ignored.", LogSeverity.Warning);
                return;
            }

            if (modules.Assign(moduleIds))
            {
                Wake();
            }
        }

        /// <summary>
        /// The application reports which modules it loaded.
        /// </summary>
        public void ReportModules(IEnumerable<ModuleLoadReport> reports)
        {
            if (modules == null)
            {
                return;
            }

            modules.Report(reports);
            Wake();
        }

        /// <summary>
        /// Gets the id of the target.
        /// </summary>
        internal string TargetId => options.TargetId;

        /// <summary>
        /// Makes this target a member of the targets that share its MSI. Before <see cref="Start"/>.
        /// </summary>
        internal void JoinGroup(InstallGroup installGroup)
        {
            group = installGroup;
            installGroup.Add(this);
        }

        private bool IsFollower => group != null && group.Lead != this;

        private IEnumerable<UpdateCoordinator> Peers => group?.Members.Where(m => m != this) ?? Enumerable.Empty<UpdateCoordinator>();

        internal void WakeFromGroup() => Wake();

        /// <summary>
        /// The modules of this target for a version of the shared MSI that the lead is about to install.
        /// </summary>
        internal ModulePlan PlanForGroup(FeedResult? currentFeed, Version applicationVersion)
        {
            return modules?.Plan(currentFeed, applicationVersion, stateStore.Load()) ?? ModulePlan.Empty;
        }

        /// <summary>
        /// The lead asks the applications of this target to end, because it installs the shared MSI.
        /// </summary>
        internal void JoinGroupShutdown(ShutdownRequest request)
        {
            var own = registry.Active;
            lock (sync)
            {
                shutdownRequested ??= clock.GetUtcNow();
                shutdownDeadline = request.Deadline;
                sessionsToRestart = sessionsToRestart.Union(own.Select(a => a.SessionId)).ToList();
            }

            SetStatus(s =>
            {
                s.Phase = UpdatePhase.ReadyToInstall;
                s.AvailableVersion = request.Version;
                s.Urgent = request.Reason == ShutdownReason.UrgentUpdate;
                s.DeferredUntil = null;
                s.Error = null;
            });
            if (own.Count == 0)
            {
                LogEnvironment.LogDebugEvent($"{tag}The shared MSI {request.Version} is ready; no application of this target runs.", LogSeverity.Report);
                return;
            }

            LogEnvironment.LogEvent($"{tag}The shared MSI {request.Version} is ready; {own.Count} application(s) asked to end ({request.Reason}).", LogSeverity.Report);
            Raise(ShutdownRequested, new ShutdownRequest { Reason = request.Reason, Deadline = request.Deadline, Version = request.Version, TargetId = options.TargetId });
        }

        /// <summary>
        /// The deadline of an urgent installation of the shared MSI has passed: the applications of this target are ended.
        /// </summary>
        internal void TerminateForGroup(DateTimeOffset deadline, string? version)
        {
            foreach (var agent in registry.Active)
            {
                LogEnvironment.LogEvent($"{tag}{agent} did not end by {deadline:u} for the urgent update of the shared MSI to {version}.", LogSeverity.Warning);
                application.Terminate(agent);
            }
        }

        /// <summary>
        /// Prepares the modules of this target for the shared MSI; see <see cref="ModuleManager.StageAsync"/>.
        /// </summary>
        internal async Task<List<StagedModule>> StageForGroupAsync(FeedResult currentFeed, ModulePlan plan, CancellationToken cancellationToken)
        {
            if (modules == null || !plan.HasWork)
            {
                return new List<StagedModule>();
            }

            return await modules.StageAsync(currentFeed, plan, cancellationToken).ConfigureAwait(false);
        }

        internal void DiscardForGroup(List<StagedModule> staged)
        {
            if (modules != null && staged.Count != 0)
            {
                LogEnvironment.LogEvent($"{tag}The shared MSI is not installed; the {staged.Count} prepared module(s) are thrown away.", LogSeverity.Warning);
                modules.Discard(staged);
            }
        }

        /// <summary>
        /// The lead starts the shared MSI: this target records it as its own pending installation, with its modules and
        /// sessions, so that it can complete it by itself after a restart of the service.
        /// </summary>
        internal void BeginGroupInstall(PendingInstall leadPending, ModulePlan plan, List<StagedModule> staged)
        {
            var state = stateStore.Load();
            if (modules != null && plan.HasWork)
            {
                modules.ApplyRemovals(plan, state);
            }

            List<int> sessions;
            lock (sync)
            {
                sessions = sessionsToRestart.ToList();
            }

            state.Pending = new PendingInstall
            {
                PackageId = leadPending.PackageId,
                Version = leadPending.Version,
                LogPath = leadPending.LogPath,
                StartedUtc = leadPending.StartedUtc,
                SessionsToRestart = sessions,
                StagedModules = staged
            };
            stateStore.Save(state);
            SetStatus(s =>
            {
                s.Phase = UpdatePhase.Installing;
                s.AvailableVersion = leadPending.Version;
                s.Error = null;
            });
        }

        internal void GroupInstallerStarted(int processId)
        {
            var state = stateStore.Load();
            if (state.Pending == null)
            {
                LogEnvironment.LogEvent($"{tag}The shared MSI started (process {processId}), but this target has no pending installation.", LogSeverity.Warning);
                return;
            }

            state.Pending.InstallerProcessId = processId;
            stateStore.Save(state);
        }

        /// <summary>
        /// The shared MSI ended: this target completes its part with the same result.
        /// </summary>
        internal Task CompleteGroupInstallAsync(InstallResult result)
        {
            var state = stateStore.Load();
            if (state.Pending == null)
            {
                LogEnvironment.LogEvent($"{tag}The shared MSI ended ({result.Description}), but this target has no pending installation to complete.", LogSeverity.Warning);
                return Task.CompletedTask;
            }

            return CompleteAsync(state, result);
        }

        /// <summary>
        /// The lead gave up the group without installing (deferred, nothing to install any more, modules only): the
        /// applications of this target run again.
        /// </summary>
        internal void EndGroupPass()
        {
            List<int> sessions;
            lock (sync)
            {
                sessions = sessionsToRestart.ToList();
            }

            EndPass(sessions);
        }

        /// <summary>
        /// Gives the group free when this target holds it; the other members end their part of the pass.
        /// </summary>
        private void ReleaseGroup()
        {
            if (group == null || !group.Release(this))
            {
                return;
            }

            foreach (var peer in Peers)
            {
                try
                {
                    peer.EndGroupPass();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"{tag}Target {peer.TargetId} could not end its part of the pass: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        /// <summary>
        /// No request to end is open any more.
        /// </summary>
        private void ClearShutdown()
        {
            lock (sync)
            {
                shutdownRequested = null;
                shutdownDeadline = null;
            }

            ReleaseGroup();
        }

        /// <summary>
        /// Is raised when the state changes.
        /// </summary>
        public event Action<UpdateStatus>? StatusChanged;

        /// <summary>
        /// Is raised when the applications are to end.
        /// </summary>
        public event Action<ShutdownRequest>? ShutdownRequested;

        /// <summary>
        /// Gets the current state.
        /// </summary>
        public UpdateStatus Status
        {
            get
            {
                lock (sync)
                {
                    return Copy(status);
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the service keeps the application down right now: it asked it to end for an
        /// update, or the installation runs. No instance is started in a new session meanwhile.
        /// </summary>
        public bool HoldsApplication
        {
            get
            {
                lock (sync)
                {
                    if (shutdownRequested != null || status.Phase == UpdatePhase.Installing)
                    {
                        return true;
                    }
                }

                // The lead installs the MSI this target shares: its application stays down as well.
                return group?.WholeGroupHeldByOther(this) == true;
            }
        }

        /// <summary>
        /// Starts the worker.
        /// </summary>
        public void Start()
        {
            stopping = new CancellationTokenSource();
            var token = stopping.Token;
            worker = Task.Run(() => RunAsync(token));
        }

        /// <summary>
        /// Looks for a new version now; a version that failed before is tried again.
        /// </summary>
        public void CheckNow()
        {
            lock (sync)
            {
                checkRequested = true;
            }

            var state = stateStore.Load();
            if (state.FailedVersion != null)
            {
                state.FailedVersion = null;
                stateStore.Save(state);
            }

            if (IsFollower)
            {
                // The MSI of this target is installed by the lead.
                group!.Lead.CheckNow();
            }

            Wake();
        }

        /// <summary>
        /// Defers ending the application.
        /// </summary>
        /// <returns>whether the deferral was granted</returns>
        public bool Defer(int minutes, string reason, RegisteredAgent agent)
        {
            var holder = group?.HeldByOther(this);
            if (holder != null)
            {
                // Another target of the shared MSI keeps the applications down; it decides.
                LogEnvironment.LogDebugEvent($"{tag}{agent} asks to defer; target {holder.TargetId} holds the shared MSI and decides.", LogSeverity.Report);
                return holder.Defer(minutes, reason, agent);
            }

            Candidate? current;
            bool moduleWork;
            lock (sync)
            {
                current = candidate;
                moduleWork = modulePlan.HasWork || rollbackNeeded;
            }

            if (current == null && !moduleWork)
            {
                LogEnvironment.LogDebugEvent($"{tag}{agent} asked to defer, but there is nothing to install.", LogSeverity.Report);
                return true;
            }

            var now = clock.GetUtcNow();
            var workKey = WorkKey(current?.Package);
            if (current != null && IsUrgent(current.Package, application.InstalledVersion(), now))
            {
                LogEnvironment.LogEvent($"{tag}{agent} asked to defer the urgent update to {current.Package.Version} ({reason}); refused.", LogSeverity.Warning);
                return false;
            }

            var limit = current != null ? DeferralLimit(current.Package) : null;
            var until = now + TimeSpan.FromMinutes(Math.Clamp(minutes, 1, options.MaximumDeferralMinutes));
            if (limit.HasValue && until > limit.Value)
            {
                until = limit.Value;
            }

            if (until <= now)
            {
                LogEnvironment.LogEvent($"{tag}{agent} asked to defer the update ({workKey}, {reason}); the longest deferral is used up.", LogSeverity.Warning);
                return false;
            }

            var state = stateStore.Load();
            state.DeferralVersion = workKey;
            state.DeferredUntil = until;
            stateStore.Save(state);
            ClearShutdown();

            LogEnvironment.LogEvent($"{tag}{agent} defers the update ({workKey}) until {until:u}: {reason}", LogSeverity.Report);
            SetStatus(s =>
            {
                s.Phase = UpdatePhase.Deferred;
                s.DeferredUntil = until;
            });
            Wake();
            return true;
        }

        /// <summary>
        /// Stops the worker.
        /// </summary>
        public void Dispose()
        {
            stopping?.Cancel();
            try
            {
                worker?.Wait(TimeSpan.FromSeconds(10));
            }
            catch (AggregateException ex)
            {
                LogEnvironment.LogDebugEvent($"{tag}The update worker ended with: {ex.InnerException?.Message}", LogSeverity.Report);
            }

            stopping?.Dispose();
            wake.Dispose();
        }

        /// <summary>
        /// One pass of the course. Public for tests; the worker calls it.
        /// </summary>
        public async Task<TimeSpan> RunOnceAsync(CancellationToken cancellationToken)
        {
            var holder = group?.HeldByOther(this);
            if (holder != null)
            {
                // Target sharing the MSI is in a pass; this one waits until it gives the group free (it wakes us).
                LogEnvironment.LogDebugEvent($"{tag}Waits: target {holder.TargetId} holds the applications of the shared MSI.", LogSeverity.Report);
                return TimeSpan.FromMinutes(1);
            }

            var state = stateStore.Load();
            if (state.Pending != null)
            {
                await ResumeAsync(state, cancellationToken).ConfigureAwait(false);
                state = stateStore.Load();
            }

            if (state.RestartRequired && state.RestartRequiredSince.HasValue
                                      && state.RestartRequiredSince.Value < clock.GetUtcNow() - TimeSpan.FromMilliseconds(Environment.TickCount64))
            {
                LogEnvironment.LogEvent($"{tag}Windows has restarted since the last installation; it is complete.", LogSeverity.Report);
                state.RestartRequired = false;
                state.RestartRequiredSince = null;
                stateStore.Save(state);
            }

            if (modules != null && modules.RollbackPending(state))
            {
                if (registry.Active.Count == 0)
                {
                    var sessions = modules.Rollback(state);
                    stateStore.Save(state);
                    lock (sync)
                    {
                        rollbackNeeded = false;
                    }

                    ClearShutdown();
                    Relaunch(sessions);
                }
                else
                {
                    lock (sync)
                    {
                        rollbackNeeded = true;
                    }
                }
            }

            var now = clock.GetUtcNow();
            if (retryAfter.HasValue && now < retryAfter.Value)
            {
                return retryAfter.Value - now;
            }

            await CheckFeedAsync(now, cancellationToken).ConfigureAwait(false);
            var installed = application.InstalledVersion();
            if (state.FailedVersion != null && VersionText.TryParse(state.FailedVersion, out var failed) && VersionText.Compare(installed, failed) >= 0)
            {
                LogEnvironment.LogEvent($"{tag}Version {state.FailedVersion}, which failed before, is installed by now.", LogSeverity.Report);
                state.FailedVersion = null;
                stateStore.Save(state);
            }

            var package = SelectBase(installed, state.FailedVersion);
            FeedResult? currentFeed;
            lock (sync)
            {
                currentFeed = feed;
            }

            // M1: a newer application only together with a fitting version of every assigned module.
            string? blockedBy = null;
            var plan = ModulePlan.Empty;
            if (modules != null)
            {
                var target = package != null && VersionText.TryParse(package.Version, out var targetVersion) ? targetVersion : installed;
                plan = modules.Plan(currentFeed, target, state);
                if (package != null && plan.Missing.Count > 0)
                {
                    blockedBy = $"waits for {string.Join(", ", plan.Missing)} for {package.Version}";
                    LogEnvironment.LogEvent($"{tag}Version {package.Version} {blockedBy}; it is not installed before.", LogSeverity.Warning);
                    package = null;
                    plan = modules.Plan(currentFeed, installed, state);
                }
            }

            // The MSI is shared: the modules of every other target must fit the new version as well.
            var plans = new Dictionary<UpdateCoordinator, ModulePlan>();
            if (package != null && group != null && VersionText.TryParse(package.Version, out var groupVersion))
            {
                foreach (var peer in Peers)
                {
                    var peerPlan = peer.PlanForGroup(currentFeed, groupVersion);
                    if (peerPlan.Missing.Count > 0)
                    {
                        blockedBy = $"waits for {string.Join(", ", peerPlan.Missing)} of target {peer.TargetId} for {package.Version}";
                        LogEnvironment.LogEvent($"{tag}Version {package.Version} {blockedBy}; it is not installed before.", LogSeverity.Warning);
                        package = null;
                        plans.Clear();
                        if (modules != null)
                        {
                            plan = modules.Plan(currentFeed, installed, state);
                        }

                        break;
                    }

                    plans[peer] = peerPlan;
                }
            }

            bool pendingRollback;
            lock (sync)
            {
                modulePlan = plan;
                peerPlans = plans;
                pendingRollback = rollbackNeeded;
            }

            SetStatus(s =>
            {
                s.BlockedBy = blockedBy;
                s.Modules = modules?.States(state);
            });

            if (package == null && (plan.HasWork || pendingRollback))
            {
                lock (sync)
                {
                    candidate = null;
                }

                return await DecideAsync(null, installed, state, cancellationToken).ConfigureAwait(false);
            }

            if (package == null)
            {
                lock (sync)
                {
                    candidate = null;
                }

                ClearShutdown();

                // A version that failed and is not tried again stays visible as failed - the application shows it, and
                // the backend sees which shop is left behind.
                var failedBefore = state.FailedVersion;

                // The lead installs the shared MSI: this target shows what is coming.
                var lead = IsFollower && failedBefore == null ? group!.Lead.Status : null;
                if (lead != null && lead.AvailableVersion != null
                                 && lead.Phase is UpdatePhase.Downloading or UpdatePhase.ReadyToInstall or UpdatePhase.Deferred or UpdatePhase.Installing)
                {
                    SetStatus(s =>
                    {
                        s.Phase = lead.Phase;
                        s.InstalledVersion = installed.ToString();
                        s.AvailableVersion = lead.AvailableVersion;
                        s.Urgent = lead.Urgent;
                        s.DeferredUntil = lead.DeferredUntil;
                        s.DeferralLimit = lead.DeferralLimit;
                        s.Error = null;
                    });
                    return NextWake(now, state);
                }

                SetStatus(s =>
                {
                    s.Phase = failedBefore != null ? UpdatePhase.Failed : state.RestartRequired ? UpdatePhase.RestartRequired : UpdatePhase.UpToDate;
                    s.InstalledVersion = installed.ToString();
                    s.AvailableVersion = failedBefore;
                    s.Urgent = false;
                    s.DeferredUntil = null;
                    s.DeferralLimit = null;
                    s.Error = failedBefore != null ? s.Error ?? state.LastResult ?? $"Version {failedBefore} failed." : null;
                });
                return NextWake(now, state);
            }

            if (!await PrepareAsync(package, installed, cancellationToken).ConfigureAwait(false))
            {
                return NextWake(now, stateStore.Load());
            }

            return await DecideAsync(package, installed, state, cancellationToken).ConfigureAwait(false);
        }

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TimeSpan delay;
                try
                {
                    delay = await RunOnceAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"{tag}The update course failed and is tried again: {ex.OutlineException()}", LogSeverity.Error);
                    SetStatus(s =>
                    {
                        s.Phase = UpdatePhase.Failed;
                        s.Error = ex.Message;
                    });
                    bool idle;
                    lock (sync)
                    {
                        idle = shutdownRequested == null;
                    }

                    if (idle)
                    {
                        // A group held for a pass that never asked anybody to end is given free.
                        ReleaseGroup();
                    }
                    delay = TimeSpan.FromMinutes(Math.Max(1, options.RetryMinutes));
                }

                try
                {
                    await wake.WaitAsync(Clamp(delay), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            LogEnvironment.LogDebugEvent($"{tag}The update worker stopped.", LogSeverity.Report);
        }

        private async Task CheckFeedAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            bool due;
            lock (sync)
            {
                due = checkRequested || now >= nextCheck;
                checkRequested = false;
            }

            if (!due)
            {
                return;
            }

            try
            {
                var result = await feedClient.GetFeedAsync(cancellationToken).ConfigureAwait(false);
                lock (sync)
                {
                    lastCheck = now;
                    nextCheck = now + TimeSpan.FromMinutes(Math.Max(1, options.CheckIntervalMinutes));
                    if (result != null)
                    {
                        feed = result;
                    }
                }

                LogEnvironment.LogDebugEvent(result == null ? "Feed checked: nothing new." : $"Feed checked: {result.Feed.Packages.Count} package(s).", LogSeverity.Report);
                SetStatus(s => s.LastCheck = now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogEnvironment.LogEvent($"{tag}The feed could not be read: {ex.OutlineException()}", LogSeverity.Error);
                lock (sync)
                {
                    nextCheck = now + TimeSpan.FromMinutes(Math.Max(1, options.RetryMinutes));
                }

                SetStatus(s => s.Error = $"Feed: {ex.Message}");
            }
        }

        private UpdatePackage? SelectBase(Version installed, string? failedVersion)
        {
            if (IsFollower)
            {
                // The MSI is shared; the lead installs it for every target.
                return null;
            }

            FeedResult? current;
            lock (sync)
            {
                current = feed;
            }

            var release = current?.Feed.Packages
                .Where(p => string.Equals(p.Id, options.ProductId, StringComparison.OrdinalIgnoreCase)
                            && (p.Kind == null || string.Equals(p.Kind, PackageKinds.Base, StringComparison.OrdinalIgnoreCase)))
                .Select(p => new { Package = p, Version = VersionText.TryParse(p.Version, out var v) ? v : null })
                .Where(p => p.Version != null)
                .OrderByDescending(p => p.Version)
                .FirstOrDefault();
            if (release == null)
            {
                return null;
            }

            var comparison = VersionText.Compare(release.Version!, installed);
            if (comparison == 0 || (comparison < 0 && !options.AllowDowngrade))
            {
                if (comparison < 0)
                {
                    LogEnvironment.LogDebugEvent($"{tag}The feed offers {release.Package.Version}, older than the installed {installed}; no downgrade.", LogSeverity.Report);
                }

                return null;
            }

            if (string.Equals(failedVersion, release.Package.Version, StringComparison.OrdinalIgnoreCase))
            {
                LogEnvironment.LogDebugEvent($"{tag}Version {failedVersion} failed before; it is tried again with the next version or on request.", LogSeverity.Report);
                return null;
            }

            return release.Package;
        }

        private async Task<bool> PrepareAsync(UpdatePackage package, Version installed, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                if (candidate != null && string.Equals(candidate.Package.Version, package.Version, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate.Path))
                {
                    return true;
                }

                candidate = null;
            }

            ClearShutdown();
            SetStatus(s =>
            {
                s.Phase = UpdatePhase.Downloading;
                s.InstalledVersion = installed.ToString();
                s.AvailableVersion = package.Version;
                s.Error = null;
            });

            FeedResult current;
            lock (sync)
            {
                current = feed!;
            }

            string path;
            try
            {
                path = await downloader.DownloadAsync(package, current.ResolvePackageUri(package, options.AllowInsecureFeed), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogEnvironment.LogEvent($"{tag}Version {package.Version} could not be downloaded: {ex.OutlineException()}", LogSeverity.Error);
                SetStatus(s =>
                {
                    s.Phase = UpdatePhase.Failed;
                    s.Error = $"Download: {ex.Message}";
                });
                retryAfter = clock.GetUtcNow() + TimeSpan.FromMinutes(Math.Max(1, options.RetryMinutes));
                return false;
            }

            var refusal = verifier.Verify(path, package);
            if (refusal != null)
            {
                LogEnvironment.LogEvent($"{tag}Version {package.Version} is refused: {refusal}", LogSeverity.Error);
                var state = stateStore.Load();
                state.FailedVersion = package.Version;
                state.LastResult = $"{package.Version}: {refusal}";
                stateStore.Save(state);
                TryDelete(path);
                SetStatus(s =>
                {
                    s.Phase = UpdatePhase.Failed;
                    s.Error = refusal;
                });
                return false;
            }

            LogEnvironment.LogEvent($"{tag}Version {package.Version} is checked and ready to install.", LogSeverity.Report);
            lock (sync)
            {
                candidate = new Candidate(package, path);
            }

            return true;
        }

        private async Task<TimeSpan> DecideAsync(UpdatePackage? package, Version installed, InstallState state, CancellationToken cancellationToken)
        {
            var now = clock.GetUtcNow();
            var urgent = package != null && IsUrgent(package, installed, now);
            // The shared MSI: the applications of every target that shares it must end before it runs.
            var wholeGroup = package != null && group != null;
            var own = registry.Active;
            var agents = wholeGroup ? group!.Members.SelectMany(m => m.registry.Active).ToList() : own;
            var workKey = WorkKey(package);
            SetStatus(s =>
            {
                s.InstalledVersion = installed.ToString();
                s.AvailableVersion = package?.Version;
                s.Urgent = urgent;
                s.DeferralLimit = package != null ? DeferralLimit(package) : null;
                s.Error = null;
            });

            var deferred = !urgent && string.Equals(state.DeferralVersion, workKey, StringComparison.OrdinalIgnoreCase)
                                   && state.DeferredUntil.HasValue && state.DeferredUntil.Value > now;
            if (agents.Count != 0 && deferred)
            {
                ReleaseGroup();
                SetStatus(s =>
                {
                    s.Phase = UpdatePhase.Deferred;
                    s.DeferredUntil = state.DeferredUntil;
                });
                return NextWake(now, state);
            }

            if (group != null && !group.TryHold(this, wholeGroup))
            {
                LogEnvironment.LogDebugEvent($"{tag}{workKey} waits: target {group.HeldByOther(this)?.TargetId} holds the applications of the shared MSI.", LogSeverity.Report);
                return NextWake(now, state);
            }

            if (agents.Count == 0)
            {
                await InstallAsync(state, cancellationToken).ConfigureAwait(false);
                return NextWake(clock.GetUtcNow(), stateStore.Load());
            }

            ShutdownRequest? request = null;
            lock (sync)
            {
                if (shutdownRequested == null || (urgent && shutdownDeadline == null))
                {
                    shutdownRequested = now;
                    shutdownDeadline = urgent ? now + TimeSpan.FromMinutes(Math.Max(1, options.UrgentDeadlineMinutes)) : null;
                    sessionsToRestart = own.Select(a => a.SessionId).Distinct().ToList();
                    request = new ShutdownRequest
                    {
                        Reason = urgent ? ShutdownReason.UrgentUpdate : ShutdownReason.Update,
                        Deadline = shutdownDeadline,
                        Version = package?.Version,
                        TargetId = options.TargetId
                    };
                }
            }

            SetStatus(s =>
            {
                s.Phase = UpdatePhase.ReadyToInstall;
                s.DeferredUntil = null;
            });
            if (request != null)
            {
                LogEnvironment.LogEvent($"{tag}{workKey} is ready; {agents.Count} application(s) asked to end ({request.Reason}{(request.Deadline.HasValue ? $", by {request.Deadline:u}" : ", when idle")}).", LogSeverity.Report);
                Raise(ShutdownRequested, request);
                if (wholeGroup)
                {
                    foreach (var peer in Peers)
                    {
                        peer.JoinGroupShutdown(request);
                    }
                }
            }

            DateTimeOffset? deadline;
            lock (sync)
            {
                deadline = shutdownDeadline;
            }

            if (deadline.HasValue && now >= deadline.Value)
            {
                foreach (var agent in own)
                {
                    LogEnvironment.LogEvent($"{tag}{agent} did not end by {deadline:u} for the urgent update to {package?.Version}.", LogSeverity.Warning);
                    application.Terminate(agent);
                }

                if (wholeGroup)
                {
                    foreach (var peer in Peers)
                    {
                        peer.TerminateForGroup(deadline.Value, package?.Version);
                    }
                }
            }

            return NextWake(now, state);
        }

        private async Task InstallAsync(InstallState state, CancellationToken cancellationToken)
        {
            Candidate? current;
            List<int> sessions;
            ModulePlan plan;
            FeedResult? currentFeed;
            bool pendingRollback;
            Dictionary<UpdateCoordinator, ModulePlan> plans;
            lock (sync)
            {
                current = candidate;
                sessions = sessionsToRestart.ToList();
                plan = modulePlan;
                currentFeed = feed;
                pendingRollback = rollbackNeeded;
                plans = peerPlans;
            }

            if (pendingRollback)
            {
                // The rollback runs at the start of the next pass, now that the application is down.
                return;
            }

            // M1: every module of the pass is fetched, checked and unpacked BEFORE the base package is installed. After that
            // only the file system can fail.
            var staged = new List<StagedModule>();
            if (modules != null && plan.HasWork && currentFeed != null)
            {
                try
                {
                    staged = await modules.StageAsync(currentFeed, plan, cancellationToken).ConfigureAwait(false);
                }
                catch (ModuleStagingException ex)
                {
                    SetStatus(s =>
                    {
                        s.Phase = UpdatePhase.Failed;
                        s.Error = ex.Message;
                        s.Modules = modules.States(stateStore.Load());
                    });
                    EndPass(sessions);
                    return;
                }
            }

            // The shared MSI: the modules of the other targets are prepared as well, before anything changes.
            var peerStaged = new List<(UpdateCoordinator Peer, ModulePlan Plan, List<StagedModule> Staged)>();
            if (current != null && group != null)
            {
                foreach (var pair in plans)
                {
                    try
                    {
                        var prepared = currentFeed != null
                            ? await pair.Key.StageForGroupAsync(currentFeed, pair.Value, cancellationToken).ConfigureAwait(false)
                            : new List<StagedModule>();
                        peerStaged.Add((pair.Key, pair.Value, prepared));
                    }
                    catch (ModuleStagingException ex)
                    {
                        LogEnvironment.LogEvent($"{tag}The modules of target {pair.Key.TargetId} could not be prepared for {current.Package.Version}; the shared MSI is not installed: {ex.OutlineException()}", LogSeverity.Error);
                        modules?.Discard(staged);
                        foreach (var done in peerStaged)
                        {
                            done.Peer.DiscardForGroup(done.Staged);
                        }

                        SetStatus(s =>
                        {
                            s.Phase = UpdatePhase.Failed;
                            s.Error = ex.Message;
                        });
                        EndPass(sessions);
                        return;
                    }
                }
            }

            if (modules != null && plan.HasWork && currentFeed != null)
            {
                modules.ApplyRemovals(plan, state);
                stateStore.Save(state);
            }

            if (current == null)
            {
                modules?.Activate(staged, state, sessions);
                state.DeferralVersion = null;
                state.DeferredUntil = null;
                stateStore.Save(state);
                LogEnvironment.LogEvent($"{tag}Modules updated ({staged.Count} switched, {plan.Removals.Count} removed).", LogSeverity.Report);
                SetStatus(s =>
                {
                    s.Phase = UpdatePhase.UpToDate;
                    s.Error = null;
                    s.Modules = modules?.States(state);
                });
                EndPass(sessions);
                return;
            }

            var logPath = Path.Combine(logDirectory, $"install-{current.Package.Id}-{current.Package.Version}-{clock.GetUtcNow():yyyyMMddHHmmss}.log");
            var pending = new PendingInstall
            {
                PackageId = current.Package.Id!,
                Version = current.Package.Version!,
                LogPath = logPath,
                StartedUtc = clock.GetUtcNow(),
                SessionsToRestart = sessions,
                StagedModules = staged
            };

            // The state goes to disk BEFORE the installer starts: an MSI that contains the service stops it, and the
            // restarted service must find out what it was doing.
            state.Pending = pending;
            stateStore.Save(state);
            foreach (var part in peerStaged)
            {
                part.Peer.BeginGroupInstall(pending, part.Plan, part.Staged);
            }

            SetStatus(s =>
            {
                s.Phase = UpdatePhase.Installing;
                s.Error = null;
            });
            LogEnvironment.LogEvent($"{tag}Installing {current.Package.Id} {current.Package.Version} (log {logPath}).", LogSeverity.Report);

            // Only one msiexec at a time on the machine: the targets of the service take turns (issue
            // ModulePlugins-Instances, D). The application of this target is down already; it waits for the other one.
            if (installGate != null && !await installGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                LogEnvironment.LogEvent($"{tag}Another target of the update service is installing; {current.Package.Id} {current.Package.Version} waits for it.", LogSeverity.Report);
                await installGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                Process process;
                try
                {
                    process = installer.Start(current.Path, current.Package, logPath);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"{tag}The installer for {current.Package.Version} could not be started: {ex.OutlineException()}", LogSeverity.Error);
                    var notStarted = new InstallResult(InstallOutcome.Failed, null, $"installer not started: {ex.Message}");
                    await CompletePeersAsync(peerStaged.Select(p => p.Peer), notStarted).ConfigureAwait(false);
                    await CompleteAsync(stateStore.Load(), notStarted).ConfigureAwait(false);
                    return;
                }

                using (process)
                {
                    pending.InstallerProcessId = process.Id;
                    state.Pending = pending;
                    stateStore.Save(state);
                    foreach (var part in peerStaged)
                    {
                        part.Peer.GroupInstallerStarted(process.Id);
                    }

                    // If the service survives the installation (the MSI does not contain it), it sees the end itself.
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    var result = evaluator.FromExitCode(process.ExitCode);
                    await CompletePeersAsync(peerStaged.Select(p => p.Peer), result).ConfigureAwait(false);
                    await CompleteAsync(stateStore.Load(), result).ConfigureAwait(false);
                }
            }
            finally
            {
                installGate?.Release();
            }
        }

        /// <summary>
        /// The other targets of the shared MSI complete their part first, so that they run again before the group is free.
        /// </summary>
        private async Task CompletePeersAsync(IEnumerable<UpdateCoordinator> peers, InstallResult result)
        {
            foreach (var peer in peers)
            {
                try
                {
                    await peer.CompleteGroupInstallAsync(result).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"{tag}Target {peer.TargetId} could not complete its part of the shared MSI: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        private async Task ResumeAsync(InstallState state, CancellationToken cancellationToken)
        {
            var pending = state.Pending!;
            LogEnvironment.LogEvent($"{tag}The installation of {pending.PackageId} {pending.Version} started at {pending.StartedUtc:u} is evaluated.", LogSeverity.Report);
            SetStatus(s =>
            {
                s.Phase = UpdatePhase.Installing;
                s.AvailableVersion = pending.Version;
            });
            var result = await evaluator.AwaitAsync(pending, ResumeTimeout, cancellationToken).ConfigureAwait(false);
            await CompleteAsync(state, result).ConfigureAwait(false);
        }

        private Task CompleteAsync(InstallState state, InstallResult result)
        {
            var pending = state.Pending;
            state.Pending = null;
            state.LastResult = $"{pending?.Version}: {result.Description}";
            var installed = application.InstalledVersion();
            var staged = pending?.StagedModules ?? new List<StagedModule>();
            if (modules != null && staged.Count > 0)
            {
                if (result.Outcome is InstallOutcome.Succeeded or InstallOutcome.SucceededRestartRequired)
                {
                    modules.Activate(staged, state, pending!.SessionsToRestart);
                }
                else
                {
                    LogEnvironment.LogEvent($"{tag}The application was not updated; the {staged.Count} prepared module(s) are thrown away.", LogSeverity.Warning);
                    modules.Discard(staged);
                }
            }

            switch (result.Outcome)
            {
                case InstallOutcome.Succeeded:
                case InstallOutcome.SucceededRestartRequired:
                    LogEnvironment.LogEvent($"{tag}{pending?.PackageId} {pending?.Version} installed ({result.Description}, exit code {result.ExitCode}).", LogSeverity.Report);
                    state.RestartRequired = result.Outcome == InstallOutcome.SucceededRestartRequired;
                    state.RestartRequiredSince = state.RestartRequired ? clock.GetUtcNow() : null;
                    state.DeferralVersion = null;
                    state.DeferredUntil = null;
                    state.FailedVersion = null;
                    lock (sync)
                    {
                        candidate = null;
                    }

                    SetStatus(s =>
                    {
                        s.Phase = state.RestartRequired ? UpdatePhase.RestartRequired : UpdatePhase.UpToDate;
                        s.InstalledVersion = installed.ToString();
                        s.AvailableVersion = null;
                        s.Urgent = false;
                        s.Error = null;
                    });
                    break;
                case InstallOutcome.Busy:
                    LogEnvironment.LogEvent($"{tag}{pending?.Version} could not be installed now ({result.Description}); tried again in {options.RetryMinutes} minutes.", LogSeverity.Warning);
                    retryAfter = clock.GetUtcNow() + TimeSpan.FromMinutes(Math.Max(1, options.RetryMinutes));
                    SetStatus(s =>
                    {
                        s.Phase = UpdatePhase.ReadyToInstall;
                        s.Error = result.Description;
                    });
                    break;
                default:
                    LogEnvironment.LogEvent($"{tag}{pending?.PackageId} {pending?.Version} was not installed: {result.Description} (exit code {result.ExitCode?.ToString() ?? "unknown"}). It is tried again with the next version or on request.", LogSeverity.Error);
                    state.FailedVersion = pending?.Version;
                    lock (sync)
                    {
                        candidate = null;
                    }

                    SetStatus(s =>
                    {
                        s.Phase = UpdatePhase.Failed;
                        s.InstalledVersion = installed.ToString();
                        s.Error = result.Description;
                    });
                    break;
            }

            stateStore.Save(state);
            EndPass(pending?.SessionsToRestart ?? new List<int>());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Ends a pass: no request to end is open any more, and the application runs again where it ran before.
        /// </summary>
        private void EndPass(List<int> sessions)
        {
            lock (sync)
            {
                shutdownRequested = null;
                shutdownDeadline = null;
                sessionsToRestart = new List<int>();
            }

            Relaunch(sessions);
            ReleaseGroup();
        }

        /// <summary>
        /// Starts the application in the given sessions, unless it runs there already.
        /// </summary>
        private void Relaunch(IEnumerable<int> sessions)
        {
            // Whatever the outcome: the application runs again where it ran before.
            foreach (var session in sessions)
            {
                if (registry.Active.Any(a => a.SessionId == session))
                {
                    LogEnvironment.LogDebugEvent($"{tag}The application already runs again in session {session}.", LogSeverity.Report);
                    continue;
                }

                try
                {
                    application.Launch(session);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"{tag}The application could not be started again in session {session}; it starts with the next logon: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        /// <summary>
        /// The key a deferral is recorded under: the version of the application, or "modules" for a pass of modules only.
        /// </summary>
        private static string WorkKey(UpdatePackage? package) => package?.Version ?? "modules";

        private bool IsUrgent(UpdatePackage package, Version installed, DateTimeOffset now)
        {
            if (package.Urgent)
            {
                return true;
            }

            if (VersionText.TryParse(package.MinimumVersion, out var minimum) && VersionText.Compare(installed, minimum) < 0)
            {
                return true;
            }

            var limit = DeferralLimit(package);
            return limit.HasValue && now >= limit.Value;
        }

        private DateTimeOffset? DeferralLimit(UpdatePackage package)
        {
            return package.Published?.AddDays(Math.Max(0, options.MaximumDeferralDays));
        }

        private TimeSpan NextWake(DateTimeOffset now, InstallState state)
        {
            var candidates = new List<DateTimeOffset>();
            lock (sync)
            {
                candidates.Add(nextCheck);
                if (shutdownDeadline.HasValue)
                {
                    candidates.Add(shutdownDeadline.Value);
                }
            }

            if (state.DeferredUntil.HasValue && state.DeferredUntil.Value > now)
            {
                candidates.Add(state.DeferredUntil.Value);
            }

            if (retryAfter.HasValue && retryAfter.Value > now)
            {
                candidates.Add(retryAfter.Value);
            }

            var next = candidates.Where(c => c > now).DefaultIfEmpty(now + TimeSpan.FromMinutes(1)).Min();
            return next - now;
        }

        private void Wake()
        {
            try
            {
                wake.Release();
            }
            catch (ObjectDisposedException ex)
            {
                LogEnvironment.LogDebugEvent($"{tag}The update worker is stopped; nothing to wake ({ex.Message}).", LogSeverity.Report);
            }
        }

        private void SetStatus(Action<UpdateStatus> change)
        {
            UpdateStatus snapshot;
            lock (sync)
            {
                change(status);
                status.LastCheck = lastCheck ?? status.LastCheck;
                snapshot = Copy(status);
            }

            Raise(StatusChanged, snapshot);
        }

        private void Raise<T>(Action<T>? handler, T value)
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
                    LogEnvironment.LogEvent($"{tag}A listener of the update service failed: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        private static UpdateStatus Copy(UpdateStatus s) => new UpdateStatus
        {
            Phase = s.Phase,
            InstalledVersion = s.InstalledVersion,
            AvailableVersion = s.AvailableVersion,
            Urgent = s.Urgent,
            DeferredUntil = s.DeferredUntil,
            DeferralLimit = s.DeferralLimit,
            LastCheck = s.LastCheck,
            Error = s.Error,
            BlockedBy = s.BlockedBy,
            Modules = s.Modules,
            TargetId = s.TargetId
        };

        private static TimeSpan Clamp(TimeSpan delay)
        {
            if (delay < TimeSpan.FromSeconds(1))
            {
                return TimeSpan.FromSeconds(1);
            }

            return delay > TimeSpan.FromHours(6) ? TimeSpan.FromHours(6) : delay;
        }

        private void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"{tag}The refused package {path} could not be deleted: {ex.OutlineException()}", LogSeverity.Warning);
            }
        }

        private sealed class Candidate
        {
            public Candidate(UpdatePackage package, string path)
            {
                Package = package;
                Path = path;
            }

            public UpdatePackage Package { get; }

            public string Path { get; }
        }
    }
}
