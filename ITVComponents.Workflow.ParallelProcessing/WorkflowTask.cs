using System;
using System.Collections.Generic;
using ITVComponents.ParallelProcessing;
using ITVComponents.Threading;

namespace ITVComponents.Workflow.ParallelProcessing
{
    /// <summary>
    /// Die Art eines Workflow-Auftrags, den ein Worker abarbeitet.
    /// </summary>
    public enum WorkflowTrigger
    {
        /// <summary>Die Instanz vorantreiben (aktive Tokens abarbeiten).</summary>
        Advance,

        /// <summary>Faellige Timer der Instanz aufnehmen.</summary>
        Timer,

        /// <summary>Ein Signal an die Instanz liefern.</summary>
        Signal
    }

    /// <summary>
    /// Ein Arbeitsauftrag fuer den <see cref="WorkflowRunner"/>: „treibe Instanz X voran / nimm ihre
    /// Timer auf / liefere ihr ein Signal". Traegt nur Daten - die Ausfuehrung liegt im
    /// <see cref="WorkflowTaskWorker"/>.
    /// </summary>
    public sealed class WorkflowTask : TaskBase
    {
        /// <summary>Initialisiert einen Auftrag.</summary>
        public WorkflowTask(string instanceId, WorkflowTrigger trigger, string signalName = null,
            IDictionary<string, object> payload = null)
        {
            InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            Trigger = trigger;
            SignalName = signalName;
            Payload = payload;
            // Wichtig: Schedules darf nicht null sein - ParallelTaskProcessor.EnqueueTask iteriert
            // darueber. Ohne Schedule laeuft der Task sofort (runWithoutSchedulers).
            Schedules = new List<SchedulerPolicy>();
            // Ebenso wichtig: nur aktive Tasks werden eingereiht - inaktive verwirft TaskScheduled.
            Active = true;
            Description = $"{trigger} {instanceId}";
        }

        /// <summary>Die betroffene Instanz.</summary>
        public string InstanceId { get; }

        /// <summary>Die Art des Auftrags.</summary>
        public WorkflowTrigger Trigger { get; }

        /// <summary>Bei <see cref="WorkflowTrigger.Signal"/>: der Signalname.</summary>
        public string SignalName { get; }

        /// <summary>Bei <see cref="WorkflowTrigger.Signal"/>: optionale Variablen fuer den Weiterlauf.</summary>
        public IDictionary<string, object> Payload { get; }

        /// <inheritdoc/>
        public override IResourceLock DemandExclusive()
        {
            // Kein prozessweiter Ausschluss noetig - die Pro-Instanz-Serialisierung uebernimmt der
            // Worker. Ein frischer Lock je Aufruf genuegt dem Vertrag von EnqueueTask.
            return new ResourceLock(new object());
        }

        /// <inheritdoc/>
        public override bool IsDuplicateOf(ITask other)
        {
            return other is WorkflowTask task
                   && task.InstanceId == InstanceId
                   && task.Trigger == Trigger
                   && task.SignalName == SignalName;
        }

        /// <inheritdoc/>
        public override Dictionary<string, object> BuildMetaData()
        {
            return new Dictionary<string, object>
            {
                { "InstanceId", InstanceId },
                { "Trigger", Trigger.ToString() },
                { "SignalName", SignalName }
            };
        }

        /// <inheritdoc/>
        public override IDisposable Unsafe()
        {
            return new UnsafeLock(null);
        }

        /// <summary>Auftraege werden fuer Phase 4 nicht serialisiert - nichts zu ergaenzen.</summary>
        protected override void CompleteObjectData()
        {
        }
    }
}
