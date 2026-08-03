using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.ParallelProcessing;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Stores;

namespace ITVComponents.Workflow.ParallelProcessing
{
    /// <summary>
    /// Einstellungen des <see cref="WorkflowRunner"/>.
    /// </summary>
    public sealed class WorkflowRunnerOptions
    {
        /// <summary>
        /// Eindeutige Kennung des zugrunde liegenden Task-Processors. Muss prozessweit eindeutig
        /// sein; der Standard ist es bereits.
        /// </summary>
        public string Identifier { get; set; } = "workflow-" + Guid.NewGuid().ToString("N");

        /// <summary>
        /// Der <b>stabile</b> Name dieses Runners - Besitzer der Zweig-Sperren. Muss ueber Neustarts
        /// hinweg gleich bleiben (beim Start raeumt der Runner seine eigenen, nach einem Absturz
        /// haengengebliebenen Sperren ueber diesen Namen ab). Standard: der Maschinenname.
        /// </summary>
        public string Owner { get; set; } = Environment.MachineName;

        /// <summary>Anzahl paralleler Worker.</summary>
        public int WorkerCount { get; set; } = 4;

        /// <summary>
        /// Poll-Intervall des Moderators in Millisekunden. Bestimmt zugleich die Aufloesung, mit
        /// der faellige Timer aufgenommen und liegengebliebene Zweige wieder aufgegriffen werden.
        /// </summary>
        public int PollTimeMs { get; set; } = 1000;

        /// <summary>
        /// Wie lange ein aufgegriffener faelliger Timer fuer diesen Runner reserviert bleibt
        /// (Millisekunden). Muss laenger sein als ein normaler Timer-Antrieb dauert, sonst greift ein
        /// zweiter Runner nach - schaedlich ist das nicht (der Commit laesst nur einen durch), es
        /// verschenkt aber den Nutzen. Deutlich zu lang verzoegert nur den Fall, dass dieser Runner
        /// mittendrin abstuerzt.
        /// </summary>
        public int TimerLeaseMs { get; set; } = 60000;

        /// <summary>
        /// Wie viele Instanzen mit faelligen Timern ein Poll hoechstens aufgreift. Der Rest bleibt fuer
        /// den naechsten Poll oder einen anderen Runner liegen - ohne die Grenze risse bei einem Stau
        /// (z.B. nach einem Ausfall) der erste Runner die ganze Nachhol-Arbeit an sich.
        /// </summary>
        public int MaxTimerBatch { get; set; } = 200;

        /// <summary>
        /// Die wichtigste Stufe des Prioritaets-Bandes (<b>kleinere Zahl = wichtiger</b>). Standard
        /// <see cref="WorkflowPriority.Normal"/> - zusammen mit <see cref="LowestPriority"/> also ein
        /// Band aus EINER Stufe. Siehe dort, warum das der Standard ist.
        /// </summary>
        public int HighestPriority { get; set; } = WorkflowPriority.Normal;

        /// <summary>
        /// Die unwichtigste Stufe des Prioritaets-Bandes. Standard <see cref="WorkflowPriority.Normal"/>.
        /// Der Prioritaets-Wert einer Instanz wird auf
        /// [<see cref="HighestPriority"/>..<see cref="LowestPriority"/>] beschnitten - ausserhalb des
        /// Bandes gaebe es keine Warteschlange fuer ihn.
        /// </summary>
        /// <remarks>
        /// <b>Standard ist EINE Stufe - Ueberholen in der Warteschlange ist bewusst abzuschalten, nicht
        /// abgeschaltet zu lassen.</b> Der Grund ist eine Eigenheit des Task-Processors: sein Worker geht
        /// einen Auswahl-Zyklus durch, in dem eine Stufe <c>((niedrigste - stufe) + 1)^3</c> Plaetze hat,
        /// und wartet <b>je Platz 50 ms</b> (<c>TaskProcessor.Work</c>). Die Zahl der Plaetze ist damit
        /// direkt die Aufgriffs-Verzoegerung:
        /// <list type="bullet">
        /// <item><description>1 Stufe: 1 Platz - ca. 50 ms (das bisherige Verhalten)</description></item>
        /// <item><description>2 Stufen (z.B. Normal..Low): 9 Plaetze - ca. 0,5 s</description></item>
        /// <item><description>3 Stufen (High..Low): 36 Plaetze - ca. 1,8 s</description></item>
        /// <item><description>5 Stufen (Highest..Lowest): 225 Plaetze - ca. 11 s</description></item>
        /// </list>
        /// Wer Ueberholen in der Warteschlange will, nimmt darum <b>zwei bis drei benachbarte Stufen</b>
        /// und keine breiteren - und nur, wenn die dadurch erkaufte Verzoegerung im Verhaeltnis zur Dauer
        /// der Workflow-Schritte klein ist. Fuer lange Hintergrund-Laeufe (Minuten) ist das nichts; fuer
        /// Instanzen, die im Sekundentakt Schritte machen, ist es viel.
        /// <para>
        /// Auch mit EINER Stufe ist die Prioritaet nicht wirkungslos: der Poll reiht die dringendsten
        /// Instanzen <b>zuerst</b> ein (der Store liefert sie sortiert), und beim begrenzten Timer-Batch
        /// bekommen sie die Plaetze. Was das Band zusaetzlich bringt, ist das Ueberholen einer bereits
        /// wartenden Schlange. Der Web-Worker-Betrieb
        /// (<c>ITVComponents.Workflow.WebWorker</c>) priorisiert ohne diesen Kompromiss - er hat keine
        /// Stufen-Warteschlangen, sondern eine sortierte Arbeitsliste.
        /// </para></remarks>
        public int LowestPriority { get; set; } = WorkflowPriority.Normal;

        /// <summary>
        /// Mit welcher Stufe Auftraege eingereiht werden, deren Instanz-Prioritaet nicht (mehr) ermittelt
        /// werden konnte - etwa ein Signal an eine bereits geloeschte Instanz. Standard
        /// <see cref="WorkflowPriority.Normal"/>.
        /// </summary>
        public int FallbackPriority { get; set; } = WorkflowPriority.Normal;
    }

    /// <summary>
    /// Macht aus der Engine einen laufenden Dienst, der Workflows <b>zweig-granular und nebenlaeufig</b>
    /// vorantreibt: je aktivem Token ein Zweig-Task, prozessuebergreifend serialisiert ueber die
    /// Zweig-Sperre (Owner = <see cref="WorkflowRunnerOptions.Owner"/>). Zweige derselben Instanz laufen
    /// damit echt parallel - same-host und (ueber eine geteilte DB) verteilt.
    /// </summary>
    /// <remarks>
    /// Setzt auf <see cref="ParallelTaskProcessor{TTask}"/> auf: dessen Worker-Pool traegt die
    /// Nebenlaeufigkeit, sein Moderator-Timer treibt ueber das GetMoreTasks-Ereignis den Poll (faellige
    /// Timer + Wiederaufnahme aktiver Zweige). Neu entstandene aktive Tokens (Split-Kinder /
    /// Join-Fortsetzungen) reihen die Worker direkt als weitere Zweig-Tasks ein.
    /// </remarks>
    public sealed class WorkflowRunner : IDisposable
    {
        private readonly IWorkflowStore store;
        private readonly WorkflowEngine engine;
        private readonly string owner;
        private readonly TimeSpan timerLease;
        private readonly int maxTimerBatch;
        private readonly int highestPriority;
        private readonly int lowestPriority;
        private readonly int fallbackPriority;
        private readonly ParallelTaskProcessor<WorkflowTask> processor;
        private bool disposed;

        /// <summary>Initialisiert den Runner.</summary>
        public WorkflowRunner(WorkflowEngine engine, IWorkflowStore store, WorkflowRunnerOptions options = null)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            options ??= new WorkflowRunnerOptions();
            owner = string.IsNullOrEmpty(options.Owner) ? Environment.MachineName : options.Owner;
            timerLease = TimeSpan.FromMilliseconds(Math.Max(1000, options.TimerLeaseMs));
            maxTimerBatch = Math.Max(1, options.MaxTimerBatch);
            highestPriority = options.HighestPriority;
            lowestPriority = Math.Max(options.HighestPriority, options.LowestPriority);
            fallbackPriority = WorkflowPriority.Clamp(options.FallbackPriority, highestPriority, lowestPriority);

            processor = new ParallelTaskProcessor<WorkflowTask>(
                options.Identifier,
                // Die Worker halten die Zweig-Sperren unter DIESEM Owner und reihen Folge-Zweige ueber den
                // Processor wieder ein - mit der Stufe des Auftrags, aus dem sie entstanden sind (sie
                // gehoeren zur selben Instanz, ein erneutes Nachschlagen waere reine Last).
                () => new WorkflowTaskWorker(engine, store, owner, task => processor.EnqueueTask(task)),
                highestPriority: highestPriority,
                lowestPriority: lowestPriority,
                workerCount: options.WorkerCount,
                workerPollTime: options.PollTimeMs,
                lowTaskThreshold: 1,
                highTaskThreshold: 64,
                useAffineThreads: false,
                runWithoutSchedulers: true,
                useTasks: true);

            // Der Moderator meldet ueber GetMoreTasks, dass eine Queue leerlaeuft - der passende Moment
            // fuer den Poll (faellige Timer + Wiederaufnahme aktiver Zweige).
            processor.GetMoreTasks += (_, _) => Poll();
        }

        /// <summary>
        /// Startet den Dienst: raeumt zunaechst die eigenen, nach einem Absturz haengengebliebenen
        /// Zweig-Sperren ab (Reset ueber den Owner-Namen - keine Wartefrist), nimmt aktive Zweige und
        /// faellige Timer auf und beginnt die periodische Verarbeitung.
        /// </summary>
        public void Start()
        {
            store.ReleaseLocksOfOwner(owner);
            Poll();

            // Initialize startet den Moderator-Timer (treibt GetMoreTasks); die Worker starten beim
            // Konstruieren zwar, bleiben aber suspendiert - erst Resume aktiviert sie.
            processor.Initialize();
            processor.Resume();
        }

        /// <summary>Haelt die Verarbeitung an.</summary>
        public void Stop()
        {
            processor.Stop();
        }

        /// <summary>
        /// Legt eine neue Instanz an und reiht ihre Start-Zweige zur nebenlaeufigen Verarbeitung ein
        /// (kein sequenzielles Vortreiben). Liefert die angelegte Instanz.
        /// </summary>
        /// <param name="definitionId">die Id der Definition</param>
        /// <param name="initialVariables">Startvariablen, oder null</param>
        /// <param name="correlationKey">optionaler Korrelationsschluessel</param>
        /// <param name="priority">
        /// die Dringlichkeit der neuen Instanz (kleinere Zahl = wichtiger), oder null fuer die Vorgabe
        /// der Definition. Sie entscheidet, wie schnell sich die Zweige dieser Instanz gegen die anderen
        /// durchsetzen.
        /// </param>
        public WorkflowInstance StartWorkflow(string definitionId,
            IDictionary<string, object> initialVariables = null, string correlationKey = null,
            int? priority = null)
        {
            WorkflowInstance instance = engine.CreateInstance(definitionId, initialVariables, correlationKey,
                priority);
            EnqueueBranches(instance);
            return instance;
        }

        /// <summary>Reiht die aktiven Zweige einer Instanz als Zweig-Tasks ein.</summary>
        public void EnqueueBranches(string instanceId)
        {
            WorkflowInstance instance = store.GetInstance(instanceId);
            if (instance == null)
            {
                LogEnvironment.LogEvent(
                    $"Branches of workflow instance '{instanceId}' could not be enqueued: no such instance.",
                    LogSeverity.Warning);
                return;
            }

            EnqueueBranches(instance);
        }

        /// <summary>
        /// Reiht die Zustellung eines Signals ein. Die Verarbeitung laeuft nebenlaeufig; die Methode
        /// kehrt sofort zurueck. Der Worker reaktiviert die wartenden Tokens und reiht die dadurch aktiv
        /// gewordenen Zweige ein.
        /// </summary>
        /// <param name="instanceId">die Instanz</param>
        /// <param name="signalName">der Signalname</param>
        /// <param name="payload">optionale Variablen fuer den Weiterlauf</param>
        /// <param name="priority">
        /// die Stufe, mit der die Zustellung eingereiht wird, oder null fuer die Stufe der Instanz. Ein
        /// eigener Wert ist der Griff fuer den Fall "diese eine Antwort ist dringend, der Workflow sonst
        /// nicht" - die Instanz selbst bleibt davon unberuehrt.
        /// </param>
        public void Signal(string instanceId, string signalName, IDictionary<string, object> payload = null,
            int? priority = null)
        {
            processor.EnqueueTask(new WorkflowTask(instanceId, WorkflowTrigger.Signal, null, signalName, payload,
                Band(priority ?? store.GetInstancePriority(instanceId))));
        }

        /// <summary>Reiht die aktiven Zweige einer bereits geladenen Instanz ein (mit ihrer Stufe).</summary>
        private void EnqueueBranches(WorkflowInstance instance)
        {
            int priority = Band(instance.Priority);
            foreach (Token token in instance.Tokens.Where(t => t.Status == TokenStatus.Active))
            {
                processor.EnqueueTask(new WorkflowTask(instance.Id, WorkflowTrigger.Advance, token.Id,
                    priority: priority));
            }
        }

        /// <summary>
        /// Beschneidet eine Stufe auf das Band dieses Runners. Eine Instanz mit einer Stufe ausserhalb des
        /// Bandes (etwa aus einer Umgebung mit anderem Band) soll laufen - der Task-Processor faende fuer
        /// sie sonst keine Warteschlange und der Auftrag ginge als Fehler zurueck.
        /// </summary>
        private int Band(int? priority)
            => WorkflowPriority.Clamp(priority ?? fallbackPriority, highestPriority, lowestPriority);

        /// <summary>
        /// Nimmt liegengebliebene Arbeit auf. Laeuft auf dem Moderator-Timer des Task-Processors - also
        /// auf einem fremden Thread, den niemand bewacht: eine Exception von hier waere <b>unbehandelt</b>
        /// und riesse den ganzen Prozess mit. Genau deshalb die Klammer um den Poll.
        /// </summary>
        /// <remarks>
        /// Ein gescheiterter Poll ist kein Beinbruch - der naechste kommt in
        /// <see cref="WorkflowRunnerOptions.PollTimeMs"/> Millisekunden, und die Arbeit liegt noch da.
        /// Verschwiegen werden darf er trotzdem nicht: ein dauerhaft scheiternder Poll (unerreichbare
        /// Datenbank, kaputte Migration) sieht von aussen aus wie "der Workflow-Dienst tut einfach
        /// nichts".
        /// </remarks>
        private void Poll()
        {
            try
            {
                PollCore();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Workflow runner '{owner}' could not pick up pending work; the next poll will retry: " +
                    $"{ex.OutlineException()}", LogSeverity.Error);
            }
        }

        private void PollCore()
        {
            // Alle laufenden Instanzen: aktive Tokens (wieder) als Zweig-Tasks einreihen. Duplikate sind
            // harmlos - die Zweig-Sperre serialisiert, ein bereits verarbeiteter Zweig ist ein Leerlauf.
            foreach (WorkflowInstance instance in store.FindRunnable().ToList())
            {
                EnqueueBranches(instance);
            }

            // Faellige Timer aufnehmen - und dabei gleich fuer diesen Runner beanspruchen. Ohne den
            // Anspruch laedt in einer Mehr-Instanzen-Umgebung JEDER Runner dieselben faelligen
            // Instanzen, und alle bis auf einen scheitern danach am Commit. Der Ausschluss selbst haengt
            // weiterhin nicht daran (das tut der Versions-Check), nur die verschwendete Arbeit.
            foreach (WorkflowInstance instance in
                     store.ClaimDueTimers(DateTime.UtcNow, owner, timerLease, maxTimerBatch).ToList())
            {
                processor.EnqueueTask(new WorkflowTask(instance.Id, WorkflowTrigger.Timer,
                    priority: Band(instance.Priority)));
            }

            // Verteilter Handoff: Zweige aufnehmen, die auf ein von DIESEM Runner bedientes Ausfuehrungs-Ziel
            // warten (nur wenn dieser Host ueberhaupt Ziele bedient - sonst kein Handoff-Empfang).
            if (engine.HostTargets.Count > 0)
            {
                foreach (WorkflowInstance instance in
                         store.FindBranchesWaitingForTarget(engine.HostTargets).ToList())
                {
                    processor.EnqueueTask(new WorkflowTask(instance.Id, WorkflowTrigger.TargetResume,
                        priority: Band(instance.Priority)));
                }
            }

            // Subworkflows: beendete Kinder, deren Ergebnis-Zustellung an den wartenden Elternprozess (etwa
            // durch einen Absturz) liegen geblieben ist, nachliefern. Im Normalfall liefert schon RunBranch
            // direkt - diese Abfrage liefert dann nichts.
            foreach (WorkflowInstance child in store.FindFinishedChildrenWithWaitingParent().ToList())
            {
                processor.EnqueueTask(new WorkflowTask(child.Id, WorkflowTrigger.DeliverChild,
                    priority: Band(child.Priority)));
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                processor.Dispose();
            }
        }
    }
}
