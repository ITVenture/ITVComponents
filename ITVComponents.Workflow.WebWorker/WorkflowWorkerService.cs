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
using ITVComponents.Workflow.EntityFramework.Abstractions;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Retention;
using ITVComponents.Workflow.Runtime;
using ITVComponents.Workflow.Stores;
using ITVComponents.Workflow.WebWorker.Runtime;
using Microsoft.EntityFrameworkCore;
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
    /// <para>
    /// Dazu ein vierter, sehr langsamer Zyklus: der <b>Aufbewahrungslauf</b>. Er faehrt je UMGEBUNG
    /// (nicht je Deskriptor - er raeumt mandantenuebergreifend), immer filterfrei, und ist per Vorgabe
    /// <b>aus</b>: <c>WorkflowWorkerOptions.RetentionInterval</c> schaltet ihn ein.
    /// </para>
    /// </summary>
    public sealed class WorkflowWorkerService : BackgroundService, IWorkflowWorkerWake
    {
        // Der Name, unter dem der WorkflowContext als scope-owned Dependency haengt. Nur fuer den
        // Plugin-Weg gebraucht: nennt eine Umgebung keinen eigenen Store, aber es gibt auch keine
        // Kontext-Fabrik, bleibt dieser Standardname der letzte Versuch. Dieselbe Aufloesung wie in
        // WorkflowOperation - die DEFAULT-Namensaufloesung der Lease faende die Dependency NICHT.
        private static readonly string ContextPluginName =
            (Attribute.GetCustomAttribute(typeof(WorkflowContext), typeof(ScopedDependencyAttribute))
                as ScopedDependencyAttribute)?.FriendlyName ?? typeof(WorkflowContext).Name;

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
            Task retention = RetentionLoopAsync(stop);

            await Task.WhenAll(new[] { refresh, schedule, retention }.Concat(consumers))
                .ConfigureAwait(false);
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
                        log.LogError(ex, "Workflow worker: environment discovery failed; keeping the last known state.");
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
                        log.LogError(ex, "Workflow worker: drive for {Key} failed.", d.Key);
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

        /// <summary>
        /// Woher der Store dieses Deskriptors seine Kontexte bekommt - oder null, wenn es dafuer nichts
        /// gibt (dann ist die Ursache bereits protokolliert).
        /// </summary>
        /// <remarks>
        /// Aus <c>Drive</c> herausgezogen, als der Aufbewahrungslauf denselben Weg brauchte. Die
        /// Reihenfolge ist keine Geschmacksfrage und steht deshalb an EINER Stelle: <b>zuerst</b> die
        /// Kontext-Fabrik (der options-only-Weg, der einzige wirklich filterfreie), und nur wenn eine
        /// Umgebung ausdruecklich ein Store-Plugin nennt, dessen Name.
        /// </remarks>
        private Func<WorkflowContext>? ResolveContextSource(IServiceProvider sp, DescriptorSpec spec,
            List<IDisposable> leases)
        {
            IDbContextFactory<WorkflowContext>? contextFactory =
                string.IsNullOrEmpty(spec.StorePluginName) ? sp.GetService<IDbContextFactory<WorkflowContext>>() : null;
            IFreshInjectablePlugin<WorkflowContext>? fresh = contextFactory == null
                ? sp.GetService<IFreshInjectablePlugin<WorkflowContext>>()
                : null;

            if (contextFactory == null && fresh == null)
            {
                log.LogError(
                    "Workflow worker: descriptor {Key} has neither an IDbContextFactory<WorkflowContext> nor an "
                    + "IFreshInjectablePlugin<WorkflowContext> to build its store from - this descriptor cannot "
                    + "run. Register the context factory (single-environment web setup) or name a store plugin "
                    + "on the environment.", spec.Key);
                return null;
            }

            return () =>
            {
                if (contextFactory != null)
                {
                    // Der Store disposed den Kontext je Aufruf selbst - hier nichts zu sammeln.
                    return contextFactory.CreateDbContext();
                }

                IPluginLease<WorkflowContext> lease = fresh!.Lease(spec.StorePluginName ?? ContextPluginName);
                leases.Add(lease);
                return lease.Value;
            };
        }

        // ---- Der Aufbewahrungslauf: langsam, je UMGEBUNG, und per Vorgabe aus -------------------------
        private async Task RetentionLoopAsync(CancellationToken stop)
        {
            if (opt.RetentionInterval <= TimeSpan.Zero)
            {
                // Kein stilles Nichtstun: wer die Fristen einstellt und sich wundert, dass nichts
                // passiert, soll den Grund im Log finden.
                log.LogInformation(
                    "Workflow worker: retention is off (RetentionInterval is not set). Nothing will be "
                    + "archived or purged, whatever deadlines are configured.");
                return;
            }

            try
            {
                while (!stop.IsCancellationRequested)
                {
                    // Erst warten, dann raeumen: ein Prozessstart ist der schlechteste Moment fuer einen
                    // Lauf, der loescht - und der erste Refresh muss die Umgebungen ohnehin erst finden.
                    await Task.Delay(opt.RetentionInterval, stop).ConfigureAwait(false);

                    // Je UMGEBUNG einmal, nicht je Deskriptor: der Lauf raeumt mandantenuebergreifend,
                    // und bei mandantengebundenen Deskriptoren taete sonst jeder dieselbe Arbeit.
                    foreach (DescriptorSpec spec in live.Values.Select(d => d.Spec)
                                 .GroupBy(s => s.EnvironmentName ?? string.Empty)
                                 .Select(g => g.First())
                                 .ToList())
                    {
                        try
                        {
                            await RunRetentionAsync(spec, stop).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (stop.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            // Eine Umgebung darf die anderen nicht mitnehmen - aber sie verschwindet auch
                            // nicht stillschweigend.
                            log.LogError(ex,
                                "Workflow worker: retention run for environment {Environment} failed.",
                                spec.EnvironmentName ?? "(default)");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normaler Stop
            }
        }

        /// <summary>Ein Aufbewahrungs- und ein Anhang-Lauf ueber die Ablage dieser Umgebung.</summary>
        /// <remarks>
        /// <b>Immer im filterfreien Regime</b>, auch wenn der Deskriptor an einen Mandanten gebunden war:
        /// aufgeraeumt wird ueber alle Mandanten, und die Grenze zieht die Frist, nicht der Kontext. Mit
        /// Filter saehe der Lauf „TenantId IS NULL" - er liefe leer, ohne eine einzige Meldung.
        /// </remarks>
        private async Task RunRetentionAsync(DescriptorSpec spec, CancellationToken stop)
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;
            sp.PrepareEmptyContext(out _);

            var leases = new List<IDisposable>();
            try
            {
                Func<WorkflowContext>? leaseCtx = ResolveContextSource(sp, spec, leases);
                if (leaseCtx == null)
                {
                    return;
                }

                var store = new EfWorkflowStore(leaseCtx);
                DateTime nowUtc = DateTime.UtcNow;

                new WorkflowRetentionRunner(store, opt.RetentionDefaults)
                    .Run(nowUtc, opt.MaxRetentionBatch);

                // Die Anhang-Inhalte haben ihre eigene Frist - und der Lauf greift auch bei Vorgaengen,
                // die noch aktiv liegen. Die Ablage der Bytes ist austauschbar; ist keine registriert,
                // gilt die eingebaute.
                IWorkflowAttachmentStore attachments =
                    sp.GetService<IWorkflowAttachmentStore>() ?? new EfWorkflowAttachmentStore(leaseCtx);
                await new WorkflowAttachmentRetentionRunner(store, attachments, opt.RetentionDefaults)
                    .RunAsync(nowUtc, opt.MaxRetentionBatch, stop).ConfigureAwait(false);
            }
            finally
            {
                foreach (IDisposable lease in leases)
                {
                    try
                    {
                        lease.Dispose();
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex,
                            "Workflow worker: releasing a store lease after the retention run failed.");
                    }
                }
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
            var leases = new List<IDisposable>();

            // ►► Woher der Kontext des Runners kommt - und warum das keine Geschmacksfrage ist.
            //
            // Der Suchlauf des Runners MUSS mandantenuebergreifend sein: er laeuft VOR jedem
            // WorkflowExecutionScope, und die Mandantengrenze zieht der Store danach an genau einer Stelle
            // (EfWorkflowStore.LoadInstances). Ein Kontext MIT Filter wertet den Mandanten hier als null aus
            // - das heisst nicht "kein Filter", sondern "TenantId IS NULL", und LoadInstances wirft dann
            // JEDE Zeile weg, die einem Mandanten gehoert. Nicht nur die lauffaehigen: auch faellige Timer,
            // Zeitplaene und Nachrichten, denn alle Aufgriffs-Wege sammeln nur Ids und laden ueber dieselbe
            // Stelle. Der Worker liefe dann vollstaendig leer, ohne eine einzige Fehlermeldung.
            //
            // Deshalb ZUERST die Kontext-Fabrik (der options-only-Weg, auf dem gar keine Model-Optionen
            // gesetzt werden - der einzige wirklich filterfreie). Nennt eine Umgebung ausdruecklich ein
            // Store-Plugin, gilt dessen Name: im Mehr-Umgebungen-Betrieb zeigt jede Umgebung auf ihre
            // eigene Ablage, und dann liegt es beim Host, dieses Plugin filterfrei zu bauen.
            Func<WorkflowContext>? leaseCtx = ResolveContextSource(sp, spec, leases);
            if (leaseCtx == null)
            {
                return (false, null);
            }

            try
            {
                // EfWorkflowStore leaset je Aufruf einen frischen Kontext (Unit of Work je Aufruf) und disposed
                // ihn selbst; die Operation sammelt die Scopes und schliesst sie am Ende (Doppel-Dispose idempotent).
                IWorkflowStore store = new EfWorkflowStore(leaseCtx);
                // Ist ein Protokoll-Filter registriert, gilt er fuer die hier angetriebenen Instanzen;
                // sonst der prozessweite Standard (WorkflowHistoryFilter.Default). Das Feature-Gate
                // entscheidet bei jedem zeitgesteuerten und jedem nachrichten-getriebenen Start, ob der
                // Mandant den Ablauf ueberhaupt (noch) haben darf - ohne Durchreichen bliebe es wirkungslos,
                // und genau diese beiden Wege sind die einzigen, die es fragen.
                var engine = new WorkflowEngine(store, activityHost, null, spec.HostTargets,
                    sp.GetService<IWorkflowHistoryFilter>(), sp.GetService<IWorkflowTenantFeatureGate>());

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
                        "Workflow worker: released orphaned branch locks of {Owner} on the first drive.", lockOwner);
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

                // Faellige Zeitplaene: hier entsteht eine Instanz. Sie kann nicht in die Arbeitsschlange
                // gehen wie die uebrigen Punkte - die verweisen auf eine bestehende Instanz, und die gibt
                // es hier erst nach dem Start. Der Start selbst laeuft deshalb gleich hier, mit demselben
                // Owner wie die Zweig-Sperren.
                int startedBySchedule = engine.TriggerDueStarts(DateTime.UtcNow, lockOwner, opt.TimerLease,
                    opt.MaxTimerBatch);

                // Liegen gebliebene Nachrichten nachholen - derselbe Punkt, den der WorkflowRunner im
                // ParallelProcessing-Betrieb faehrt. Im Regelfall ist hier nichts: der Sender stellt selbst
                // zu, sobald er festgeschrieben ist. Was hier auftaucht, hat einen Absturz zwischen Commit
                // und Zustellung ueberlebt - und wo NUR dieser Worker laeuft, gibt es sonst niemanden, der
                // die Vormerkung je wieder anfasst. Sie bliebe fuer immer liegen, ohne dass etwas fehlt,
                // das man suchen wuerde.
                int deliveredMessages = engine.DeliverPendingMessages(lockOwner, opt.TimerLease);

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

                // Ein zeitgesteuerter Start ist Arbeit, auch wenn er nichts in die Schlange gelegt hat:
                // sonst legte sich der Antrieb gleich wieder schlafen, obwohl gerade eine Instanz
                // angelaufen ist, deren erste Zweige noch aufzunehmen sind. Fuer eine nachgeholte
                // Nachricht gilt dasselbe: sie weckt einen Empfaenger, der erst im naechsten Suchlauf
                // als lauffaehig auftaucht.
                bool any = work.Count > 0 || startedBySchedule > 0 || deliveredMessages > 0;
                const int maxSteps = 100000;
                int steps = 0;
                while (work.Count > 0 && !ct.IsCancellationRequested)
                {
                    if (++steps > maxSteps)
                    {
                        log.LogError("Workflow worker: drive for {Key} exceeded {Max} steps - aborted " +
                            "(possible endless loop).", spec.Key, maxSteps);
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

                // Der naechste Weckzeitpunkt ist der FRUEHERE von beiden - ein Timer und ein Zeitplan sind
                // gleichermassen Grund aufzuwachen. Nur den Timer zu betrachten hiesse, ueber einen
                // faelligen Zeitplan hinwegzuschlafen, bis zufaellig etwas anderes den Antrieb weckt.
                DateTime? nextWake = null;
                if (!any)
                {
                    DateTime nowUtc = DateTime.UtcNow;
                    DateTime? nextTimer = store.PeekNextTimerDueUtc(nowUtc);
                    DateTime? nextSchedule = store.PeekNextScheduleDueUtc(nowUtc);
                    nextWake = nextTimer == null || nextSchedule == null
                        ? nextTimer ?? nextSchedule
                        : (nextTimer < nextSchedule ? nextTimer : nextSchedule);
                }

                return (any, nextWake);
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
                        log.LogError(ex, "Workflow worker: could not release an operation scope ({Key}).", spec.Key);
                    }
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Ueber den Namen der Umgebung, nicht ueber den Schluessel: die Discovery fasst gleich
        /// konfigurierte Mandanten zu EINEM Deskriptor zusammen, und bei widerspruechlicher Konfiguration
        /// traegt derselbe Name mehrere Varianten mit je eigenem Schluessel. Ein Weckruf, der den
        /// Schluessel raten muesste, ginge in beiden Faellen ins Leere - und ein nicht geweckter Vorgang
        /// laeuft erst beim naechsten regulaeren Poll an, was aussieht wie "der Workflow startet nicht".
        /// <para>
        /// Der Mandant bleibt im Aufruf, weil ein tenant-gepinnter Deskriptor (Host ohne Zusammenfassung,
        /// oder eine Umgebung, die nur ein Mandant traegt) weiterhin gezielt geweckt werden soll; er ist
        /// aber kein Ausschluss-Kriterium - der gemeinsame Deskriptor faehrt diesen Mandanten mit.
        /// </para>
        /// </remarks>
        public void Poke(string? environmentName, string? tenantId)
        {
            string env = environmentName ?? string.Empty;
            bool anyWoken = false;
            foreach (WorkflowExecutionDescriptor d in live.Values)
            {
                string? candidate = d.Spec.EnvironmentName ?? string.Empty;
                if (!string.Equals(candidate, env, StringComparison.Ordinal))
                {
                    continue;
                }

                // Ein gemeinsamer (tenant-freier) Deskriptor faehrt jeden Mandanten; ein gepinnter nur
                // seinen eigenen.
                if (d.Spec.TenantId == null || tenantId == null
                    || string.Equals(d.Spec.TenantId, tenantId, StringComparison.Ordinal))
                {
                    d.Poke();
                    anyWoken = true;
                }
            }

            if (!anyWoken)
            {
                // Kein Deskriptor fuer diese Umgebung: sonst wartete der Aufrufer auf einen Vortrieb, den
                // niemand macht. Kein Fehler (die Umgebung kann bewusst ohne Worker laufen), aber nichts,
                // was stillschweigend verschwinden darf.
                log.LogDebug(
                    "Workflow worker: poke for environment {Environment} (tenant {Tenant}) matched no "
                    + "descriptor - nothing is polling it in this process.",
                    env.Length == 0 ? "(default)" : env, tenantId ?? "(none)");
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
