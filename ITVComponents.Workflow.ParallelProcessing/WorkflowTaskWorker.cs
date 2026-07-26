using System;
using System.Collections.Generic;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.ParallelProcessing;
using ITVComponents.Workflow.Stores;

namespace ITVComponents.Workflow.ParallelProcessing
{
    /// <summary>
    /// Arbeitet <see cref="WorkflowTask"/>s <b>zweig-granular</b> ab: ein Advance-Task treibt EINEN Zweig
    /// (Token) unter seiner Zweig-Sperre ueber <see cref="WorkflowEngine.RunBranch"/> voran; ein Signal-/
    /// Timer-Task reaktiviert die passenden wartenden Tokens. Die durch einen Vortrieb neu entstandenen
    /// aktiven Tokens (Split-Kinder / Join-Fortsetzungen) werden als eigene Advance-Tasks eingereiht.
    /// </summary>
    /// <remarks>
    /// Der prozessuebergreifende Ausschluss laeuft ueber die Zweig-Sperre des Stores (Owner = stabiler
    /// Runner-Name), nicht mehr ueber ein prozesslokales Gate - Zweige derselben Instanz duerfen darum
    /// echt nebenlaeufig (same-host und verteilt) laufen. Der Tenant-Kontext wird von RunBranch selbst
    /// gesetzt (aus der Instanz).
    /// </remarks>
    public sealed class WorkflowTaskWorker : TaskWorkerBase<WorkflowTask>
    {
        private readonly WorkflowEngine engine;
        private readonly IWorkflowStore store;
        private readonly string owner;
        private readonly Action<WorkflowTask> enqueue;

        /// <summary>Initialisiert einen Worker.</summary>
        /// <param name="engine">die Engine</param>
        /// <param name="store">der Store</param>
        /// <param name="owner">der stabile Runner-Name (Besitzer der Zweig-Sperren)</param>
        /// <param name="enqueue">Callback, um Folge-Zweige (neue aktive Tokens) einzureihen</param>
        public WorkflowTaskWorker(WorkflowEngine engine, IWorkflowStore store, string owner,
            Action<WorkflowTask> enqueue)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.owner = string.IsNullOrEmpty(owner) ? throw new ArgumentNullException(nameof(owner)) : owner;
            this.enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
        }

        /// <inheritdoc/>
        public override void Process(WorkflowTask task)
        {
            try
            {
                switch (task.Trigger)
                {
                    case WorkflowTrigger.Advance:
                        ProcessBranch(task);
                        break;
                    case WorkflowTrigger.Signal:
                        EnqueueBranches(task.InstanceId,
                            engine.ReactivateSignal(task.InstanceId, task.SignalName, task.Payload));
                        break;
                    case WorkflowTrigger.Timer:
                        EnqueueBranches(task.InstanceId,
                            engine.ReactivateTimers(task.InstanceId, DateTime.UtcNow));
                        break;
                    case WorkflowTrigger.TargetResume:
                        // Verteilter Handoff: die auf ein Ziel DIESES Hosts wartenden Zweige aktivieren und
                        // als Zweig-Tasks einreihen (dann fuehrt RunBranch die Aktivitaet hier aus).
                        EnqueueBranches(task.InstanceId,
                            engine.ReactivateForTargets(task.InstanceId, engine.HostTargets));
                        break;
                    case WorkflowTrigger.DeliverChild:
                        // Recovery: ein beendeter Subworkflow liefert sein Ergebnis an den wartenden
                        // Elternprozess nach (idempotent). Der Eltern-Zweig wird dadurch wieder lauffaehig
                        // und beim naechsten Poll aufgenommen.
                        engine.DeliverChildCompletion(task.InstanceId);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Ein fehlgeschlagener Auftrag darf den Worker nicht mitreissen. Der Fehler wird
                // protokolliert (mit Stacktrace); die Engine hat instanz-interne Fehler ohnehin bereits
                // auf Faulted abgebildet.
                LogEnvironment.LogEvent(
                    $"Workflow task '{task.Trigger}' for instance '{task.InstanceId}' token '{task.TokenId}' " +
                    $"failed: {ex.OutlineException()}", LogSeverity.Error);
            }
        }

        private void ProcessBranch(WorkflowTask task)
        {
            if (string.IsNullOrEmpty(task.TokenId))
            {
                LogEnvironment.LogEvent(
                    $"Advance task for instance '{task.InstanceId}' has no token id - skipped.", LogSeverity.Warning);
                return;
            }

            // Prozessuebergreifender Ausschluss pro Zweig. Bekommt ein anderer Worker/Prozess die Sperre
            // nicht, treibt er den Zweig gerade schon voran - dann ueberspringen (kein Fehler).
            using IWorkflowBranchLock branchLock = store.TryAcquireBranchLock(task.InstanceId, task.TokenId, owner);
            if (branchLock == null)
            {
                return;
            }

            EnqueueBranches(task.InstanceId, engine.RunBranch(task.InstanceId, task.TokenId));
        }

        private void EnqueueBranches(string instanceId, IReadOnlyList<string> newTokenIds)
        {
            if (newTokenIds == null)
            {
                return;
            }

            foreach (string tokenId in newTokenIds)
            {
                enqueue(new WorkflowTask(instanceId, WorkflowTrigger.Advance, tokenId));
            }
        }
    }
}
