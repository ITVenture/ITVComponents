using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ParallelTaskProcessor<WorkflowTask> processor;
        private bool disposed;

        /// <summary>Initialisiert den Runner.</summary>
        public WorkflowRunner(WorkflowEngine engine, IWorkflowStore store, WorkflowRunnerOptions options = null)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            options ??= new WorkflowRunnerOptions();
            owner = string.IsNullOrEmpty(options.Owner) ? Environment.MachineName : options.Owner;

            processor = new ParallelTaskProcessor<WorkflowTask>(
                options.Identifier,
                // Die Worker halten die Zweig-Sperren unter DIESEM Owner und reihen Folge-Zweige ueber den
                // Processor wieder ein.
                () => new WorkflowTaskWorker(engine, store, owner, task => processor.EnqueueTask(task)),
                highestPriority: 0,
                lowestPriority: 0,
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
        public WorkflowInstance StartWorkflow(string definitionId,
            IDictionary<string, object> initialVariables = null, string correlationKey = null)
        {
            WorkflowInstance instance = engine.CreateInstance(definitionId, initialVariables, correlationKey);
            EnqueueBranches(instance.Id);
            return instance;
        }

        /// <summary>Reiht die aktiven Zweige einer Instanz als Zweig-Tasks ein.</summary>
        public void EnqueueBranches(string instanceId)
        {
            WorkflowInstance instance = store.GetInstance(instanceId);
            if (instance == null)
            {
                return;
            }

            foreach (Token token in instance.Tokens.Where(t => t.Status == TokenStatus.Active))
            {
                processor.EnqueueTask(new WorkflowTask(instanceId, WorkflowTrigger.Advance, token.Id));
            }
        }

        /// <summary>
        /// Reiht die Zustellung eines Signals ein. Die Verarbeitung laeuft nebenlaeufig; die Methode
        /// kehrt sofort zurueck. Der Worker reaktiviert die wartenden Tokens und reiht die dadurch aktiv
        /// gewordenen Zweige ein.
        /// </summary>
        public void Signal(string instanceId, string signalName, IDictionary<string, object> payload = null)
        {
            processor.EnqueueTask(new WorkflowTask(instanceId, WorkflowTrigger.Signal, null, signalName, payload));
        }

        private void Poll()
        {
            // Alle laufenden Instanzen: aktive Tokens (wieder) als Zweig-Tasks einreihen. Duplikate sind
            // harmlos - die Zweig-Sperre serialisiert, ein bereits verarbeiteter Zweig ist ein Leerlauf.
            foreach (WorkflowInstance instance in store.FindRunnable().ToList())
            {
                foreach (Token token in instance.Tokens.Where(t => t.Status == TokenStatus.Active))
                {
                    processor.EnqueueTask(new WorkflowTask(instance.Id, WorkflowTrigger.Advance, token.Id));
                }
            }

            // Faellige Timer aufnehmen.
            foreach (WorkflowInstance instance in store.FindDueTimers(DateTime.UtcNow).ToList())
            {
                processor.EnqueueTask(new WorkflowTask(instance.Id, WorkflowTrigger.Timer));
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
