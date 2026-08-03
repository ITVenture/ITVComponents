using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Stores;
using ITVComponents.Workflow.WebWorker.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITVComponents.Workflow.WebWorker
{
    /// <summary>
    /// Der eine Hosted-Service: haelt einen langsamen Refresh (Tenant/Umgebungs-Discovery), einen schnellen
    /// Scheduler (Faelligkeit pruefen) und einen geteilten, gedeckelten Pool (die Antriebe). Die Deskriptoren
    /// sind passiv; dieser Service treibt sie. Beide Betriebs-Regimes teilen sich denselben Antriebs-Pfad und
    /// unterscheiden sich nur darin, welcher <c>Prepare*Context</c> den Store-Kontext baut.
    /// </summary>
    public sealed class WorkflowWorkerService : BackgroundService, IWorkflowWorkerWake
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly WorkflowEnvironmentDiscovery discovery;
        private readonly WorkflowWorkerOptions opt;
        private readonly ILogger<WorkflowWorkerService> log;

        // Lebende Deskriptoren, ueber Refreshes hinweg stabil (Schluessel = DescriptorSpec.Key).
        private readonly ConcurrentDictionary<string, WorkflowExecutionDescriptor> live = new();

        private Channel<WorkflowExecutionDescriptor> queue = null!;

        public WorkflowWorkerService(IServiceScopeFactory scopeFactory, WorkflowEnvironmentDiscovery discovery,
            WorkflowWorkerOptions opt, ILogger<WorkflowWorkerService> log)
        {
            this.scopeFactory = scopeFactory;
            this.discovery = discovery;
            this.opt = opt;
            this.log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            queue = Channel.CreateBounded<WorkflowExecutionDescriptor>(
                new BoundedChannelOptions(Math.Max(1, opt.MaxConcurrency) * 4) { SingleReader = false, SingleWriter = true });

            // N feste Consumer = der geteilte Pool (NICHT Threads je Deskriptor).
            Task[] consumers = Enumerable.Range(0, Math.Max(1, opt.MaxConcurrency))
                .Select(_ => Task.Run(() => ConsumeAsync(stop), stop)).ToArray();

            Task refresh = RefreshLoopAsync(stop);
            Task schedule = ScheduleLoopAsync(stop);

            await Task.WhenAll(new[] { refresh, schedule }.Concat(consumers)).ConfigureAwait(false);
        }

        // ---- langsamer Refresh: teure Discovery, selten -----------------------------------------------
        private async Task RefreshLoopAsync(CancellationToken stop)
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        IReadOnlyList<DescriptorSpec> desired = discovery.Discover(stop);
                        Reconcile(desired);
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Kein stiller catch: der Refresh soll den Prozess nicht killen, aber sichtbar bleiben.
                        log.LogError(ex, "Workflow-Worker: Umgebungs-Discovery fehlgeschlagen; behalte den letzten Stand.");
                    }

                    await Task.Delay(opt.RefreshInterval, stop).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // normaler Stop
            }
        }

        private void Reconcile(IReadOnlyList<DescriptorSpec> desired)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (DescriptorSpec spec in desired)
            {
                keep.Add(spec.Key);
                live.AddOrUpdate(spec.Key,
                    _ => WorkflowExecutionDescriptor.FromSpec(spec),
                    (_, existing) => existing.WithRefreshedSpec(spec));
            }

            foreach (string goneKey in live.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                if (live.TryRemove(goneKey, out WorkflowExecutionDescriptor? gone))
                {
                    gone.MarkRetired(); // ein etwaiger laufender Antrieb darf auslaufen
                }
            }
        }

        // ---- schneller Scheduler: nur Faelligkeit pruefen, faellige Deskriptoren einreihen -------------
        private async Task ScheduleLoopAsync(CancellationToken stop)
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    DateTime now = DateTime.UtcNow;
                    foreach (WorkflowExecutionDescriptor d in live.Values)
                    {
                        if (d.TryClaimForRun(now))
                        {
                            await queue.Writer.WriteAsync(d, stop).ConfigureAwait(false);
                        }
                    }

                    await Task.Delay(opt.SchedulerTick, stop).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // normaler Stop
            }
        }

        // ---- Pool-Consumer: einen Deskriptor antreiben, dann Faelligkeit/Back-off setzen --------------
        private async Task ConsumeAsync(CancellationToken stop)
        {
            try
            {
                await foreach (WorkflowExecutionDescriptor d in queue.Reader.ReadAllAsync(stop).ConfigureAwait(false))
                {
                    bool foundWork = false;
                    DateTime? nextTimer = null;
                    try
                    {
                        (foundWork, nextTimer) = Drive(d, stop);
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested)
                    {
                        // Beim Abschluss unten wird der Deskriptor wieder freigegeben.
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Workflow-Worker: Antrieb fuer {Key} fehlgeschlagen.", d.Key);
                    }
                    finally
                    {
                        d.CompleteRun(foundWork, nextTimer, opt);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normaler Stop
            }
        }

        // ---- Der Antrieb: spiegelt WorkflowRunner.Poll + WorkflowTaskWorker, einzelthreadig je Drive ---
        private (bool foundWork, DateTime? nextTimer) Drive(WorkflowExecutionDescriptor d, CancellationToken ct)
        {
            DescriptorSpec spec = d.Spec;
            using IServiceScope scope = scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            // ►► Der einzige Regime-Unterschied:
            if (spec.TenantId == null)
            {
                sp.PrepareEmptyContext(out _);                                        // global -> Filter AUS
            }
            else
            {
                sp.PrepareBackgroundContext(opt.BackgroundUserName, spec.TenantId, out _); // per-Tenant -> Filter AN
            }

            IActivityHost activityHost = sp.GetRequiredService<IActivityHost>();
            IFreshInjectablePlugin<WorkflowContext> fresh = sp.GetRequiredService<IFreshInjectablePlugin<WorkflowContext>>();
            var leases = new List<IDisposable>();

            WorkflowContext LeaseCtx()
            {
                IPluginLease<WorkflowContext> lease = fresh.Lease(spec.StorePluginName);
                leases.Add(lease);
                return lease.Value;
            }

            try
            {
                // EfWorkflowStore leaset je Aufruf einen frischen Kontext (Unit of Work je Aufruf) und disposed
                // ihn selbst; die Operation sammelt die Scopes und schliesst sie am Ende (Doppel-Dispose idempotent).
                IWorkflowStore store = new EfWorkflowStore(LeaseCtx);
                // Ist ein Protokoll-Filter registriert, gilt er fuer die hier angetriebenen Instanzen;
                // sonst der prozessweite Standard (WorkflowHistoryFilter.Default).
                var engine = new WorkflowEngine(store, activityHost, null, spec.HostTargets,
                    sp.GetService<IWorkflowHistoryFilter>());

                // Deskriptor-spezifischer Lock-Owner: raeumt beim ersten Antrieb NUR die eigenen verwaisten
                // Locks. Die Branch-Lock-Tabelle hat keinen TenantId (ReleaseLocksOfOwner ist global-by-Owner),
                // darum muss der Owner pro Deskriptor eindeutig sein - sonst risse er aktive Locks anderer
                // Deskriptoren/Worker mit. Der prozessuebergreifende Ausschluss laeuft ueber (InstanceId,
                // TokenId), nicht ueber den Owner - der Owner ist nur der Aufraeum-Tag.
                string lockOwner = $"{opt.LockOwnerName}|{spec.Key}";
                if (d.TryBeginInitialCleanup())
                {
                    store.ReleaseLocksOfOwner(lockOwner);
                    log.LogInformation(
                        "Workflow-Worker: verwaiste Branch-Locks fuer {Owner} beim ersten Antrieb freigegeben.", lockOwner);
                }

                // Nach Dringlichkeit geordnet statt streng der Reihe nach: ein Antrieb arbeitet die
                // aufgenommene Arbeit einthreadig ab, also entscheidet allein diese Reihenfolge, was
                // zuerst laeuft. Der Zaehler bricht Gleichstaende in Aufnahme-Reihenfolge auf - ohne ihn
                // waere die Reihenfolge gleich priorisierter Elemente unbestimmt.
                var work = new PriorityQueue<DriveItem, (int Priority, long Seq)>();
                long seq = 0;
                foreach (WorkflowInstance inst in store.FindRunnable().ToList())
                {
                    foreach (Token token in inst.Tokens.Where(t => t.Status == TokenStatus.Active))
                    {
                        work.Enqueue(new DriveItem(DriveTrigger.Advance, inst.Id, token.Id, inst.Priority),
                            (inst.Priority, seq++));
                    }
                }

                // Faellige Timer beanspruchen statt nur lesen: sonst laedt jeder Prozess (und jeder
                // Deskriptor) dieselben faelligen Instanzen und alle bis auf einen scheitern danach am
                // Commit. Derselbe Owner wie bei den Branch-Locks - so raeumt der Neustart oben beides ab.
                foreach (WorkflowInstance inst in
                         store.ClaimDueTimers(DateTime.UtcNow, lockOwner, opt.TimerLease, opt.MaxTimerBatch)
                             .ToList())
                {
                    work.Enqueue(new DriveItem(DriveTrigger.Timer, inst.Id, null, inst.Priority),
                        (inst.Priority, seq++));
                }

                if (spec.HostTargets.Count > 0)
                {
                    foreach (WorkflowInstance inst in store.FindBranchesWaitingForTarget(spec.HostTargets).ToList())
                    {
                        work.Enqueue(new DriveItem(DriveTrigger.TargetResume, inst.Id, null, inst.Priority),
                            (inst.Priority, seq++));
                    }
                }

                foreach (WorkflowInstance child in store.FindFinishedChildrenWithWaitingParent().ToList())
                {
                    work.Enqueue(new DriveItem(DriveTrigger.DeliverChild, child.Id, null, child.Priority),
                        (child.Priority, seq++));
                }

                bool any = work.Count > 0;
                const int maxSteps = 100000;
                int steps = 0;
                while (work.Count > 0 && !ct.IsCancellationRequested)
                {
                    if (++steps > maxSteps)
                    {
                        log.LogError("Workflow-Worker: Antrieb fuer {Key} ueberschritt {Max} Schritte - abgebrochen " +
                            "(moegliche Endlosschleife).", spec.Key, maxSteps);
                        break;
                    }

                    DriveItem item = work.Dequeue();
                    switch (item.Trigger)
                    {
                        case DriveTrigger.Advance:
                            using (IWorkflowBranchLock? branchLock =
                                store.TryAcquireBranchLock(item.InstanceId, item.TokenId!, lockOwner))
                            {
                                if (branchLock == null)
                                {
                                    continue; // ein anderer Runner haelt den Zweig
                                }

                                // Folge-Zweige erben die Stufe ihres Ausloesers - sie gehoeren zur selben
                                // Instanz.
                                foreach (string tid in engine.RunBranch(item.InstanceId, item.TokenId!))
                                {
                                    work.Enqueue(
                                        new DriveItem(DriveTrigger.Advance, item.InstanceId, tid, item.Priority),
                                        (item.Priority, seq++));
                                }
                            }

                            break;
                        case DriveTrigger.Timer:
                            foreach (string tid in engine.ReactivateTimers(item.InstanceId, DateTime.UtcNow))
                            {
                                work.Enqueue(
                                    new DriveItem(DriveTrigger.Advance, item.InstanceId, tid, item.Priority),
                                    (item.Priority, seq++));
                            }

                            break;
                        case DriveTrigger.TargetResume:
                            foreach (string tid in engine.ReactivateForTargets(item.InstanceId, spec.HostTargets))
                            {
                                work.Enqueue(
                                    new DriveItem(DriveTrigger.Advance, item.InstanceId, tid, item.Priority),
                                    (item.Priority, seq++));
                            }

                            break;
                        case DriveTrigger.DeliverChild:
                            engine.DeliverChildCompletion(item.InstanceId);
                            break;
                    }
                }

                DateTime? nextTimer = any ? null : store.PeekNextTimerDueUtc(DateTime.UtcNow);
                return (any, nextTimer);
            }
            finally
            {
                for (int i = leases.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        leases[i].Dispose();
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Workflow-Worker: konnte einen Operations-Scope ({Key}) nicht freigeben.", spec.Key);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Poke(string? environmentName, string? tenantId)
        {
            string key = tenantId == null ? (environmentName ?? string.Empty) : $"{environmentName}|{tenantId}";
            if (live.TryGetValue(key, out WorkflowExecutionDescriptor? d))
            {
                d.Poke();
            }
        }

        private enum DriveTrigger
        {
            Advance,
            Timer,
            TargetResume,
            DeliverChild
        }

        private readonly record struct DriveItem(DriveTrigger Trigger, string InstanceId, string? TokenId,
            int Priority);
    }
}
