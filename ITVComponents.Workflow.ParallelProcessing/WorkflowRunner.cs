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

        /// <summary>Anzahl paralleler Worker.</summary>
        public int WorkerCount { get; set; } = 4;

        /// <summary>
        /// Poll-Intervall des Moderators in Millisekunden. Bestimmt zugleich die Aufloesung, mit
        /// der faellige Timer aufgenommen werden.
        /// </summary>
        public int PollTimeMs { get; set; } = 1000;
    }

    /// <summary>
    /// Macht aus der synchron getriebenen Engine einen laufenden Dienst: treibt Instanzen
    /// nebenlaeufig voran, nimmt faellige Timer periodisch auf und liefert Signale ein - pro Instanz
    /// stets nur ein Worker gleichzeitig.
    /// </summary>
    /// <remarks>
    /// Setzt auf <see cref="ParallelTaskProcessor{TTask}"/> auf: dessen Worker-Pool traegt die
    /// Nebenlaeufigkeit, sein Moderator-Timer treibt ueber das GetMoreTasks-Ereignis den Timer-Poll.
    /// Neue Instanzen werden weiterhin ueber die Engine gestartet (<c>StartWorkflow</c> laeuft bis
    /// zum ersten Wartepunkt synchron durch); danach uebernimmt der Runner.
    /// </remarks>
    public sealed class WorkflowRunner : IDisposable
    {
        private readonly IWorkflowStore store;
        private readonly ParallelTaskProcessor<WorkflowTask> processor;
        private bool disposed;

        /// <summary>Initialisiert den Runner.</summary>
        public WorkflowRunner(WorkflowEngine engine, IWorkflowStore store, WorkflowRunnerOptions options = null)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            this.store = store ?? throw new ArgumentNullException(nameof(store));
            options ??= new WorkflowRunnerOptions();

            // Das InstanceGate gehoert zur geteilten Laufzeit-Umgebung der Engine und wird jedem Worker
            // als Property "reingedrueckt" (nicht als Konstruktor-Argument) - siehe IWorkflowRuntimeAware.
            processor = new ParallelTaskProcessor<WorkflowTask>(
                options.Identifier,
                () => new WorkflowTaskWorker(engine, store) { Runtime = engine.Runtime },
                highestPriority: 0,
                lowestPriority: 0,
                workerCount: options.WorkerCount,
                workerPollTime: options.PollTimeMs,
                lowTaskThreshold: 1,
                highTaskThreshold: 64,
                useAffineThreads: false,
                runWithoutSchedulers: true,
                useTasks: true);

            // Der Moderator meldet ueber GetMoreTasks, dass eine Queue leerlaeuft - der passende
            // Moment, faellige Timer nachzuschieben.
            processor.GetMoreTasks += (_, _) => DispatchDueTimers();
        }

        /// <summary>
        /// Startet den Dienst: nimmt liegengebliebene laufende Instanzen und faellige Timer auf und
        /// beginnt die periodische Verarbeitung.
        /// </summary>
        public void Start()
        {
            foreach (WorkflowInstance instance in store.FindRunnable().ToList())
            {
                processor.EnqueueTask(new WorkflowTask(instance.Id, WorkflowTrigger.Advance));
            }

            DispatchDueTimers();

            // Initialize startet den Moderator-Timer (treibt GetMoreTasks); die Worker starten
            // beim Konstruieren zwar, bleiben aber suspendiert - erst Resume aktiviert sie.
            processor.Initialize();
            processor.Resume();
        }

        /// <summary>Haelt die Verarbeitung an.</summary>
        public void Stop()
        {
            processor.Stop();
        }

        /// <summary>Reiht das Vorantreiben einer Instanz ein.</summary>
        public void EnqueueAdvance(string instanceId)
        {
            processor.EnqueueTask(new WorkflowTask(instanceId, WorkflowTrigger.Advance));
        }

        /// <summary>
        /// Reiht die Zustellung eines Signals ein. Die Verarbeitung laeuft nebenlaeufig; die Methode
        /// kehrt sofort zurueck.
        /// </summary>
        public void Signal(string instanceId, string signalName, IDictionary<string, object> payload = null)
        {
            processor.EnqueueTask(new WorkflowTask(instanceId, WorkflowTrigger.Signal, signalName, payload));
        }

        private void DispatchDueTimers()
        {
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
