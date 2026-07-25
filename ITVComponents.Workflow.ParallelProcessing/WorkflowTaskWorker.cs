using System;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.ParallelProcessing;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Runtime;
using ITVComponents.Workflow.Stores;

namespace ITVComponents.Workflow.ParallelProcessing
{
    /// <summary>
    /// Arbeitet <see cref="WorkflowTask"/>s ab: laedt die Instanz aus dem Store und treibt sie ueber
    /// die Engine voran, jeweils unter der Pro-Instanz-Sperre.
    /// </summary>
    /// <remarks>
    /// Das <see cref="InstanceGate"/> kommt NICHT ueber den Konstruktor, sondern als garantiert
    /// gesetztes Property (<see cref="IWorkflowRuntimeAware"/>) aus der geteilten Laufzeit-Umgebung der
    /// Engine - so laesst sich der Worker auch als Plugin laden, wo ein zur Kompositionszeit erzeugtes,
    /// geteiltes Gate nicht als Konstruktor-Argument aufloesbar waere.
    /// </remarks>
    public sealed class WorkflowTaskWorker : TaskWorkerBase<WorkflowTask>, IWorkflowRuntimeAware
    {
        private readonly WorkflowEngine engine;
        private readonly IWorkflowStore store;
        private WorkflowRuntimeContext runtime;

        /// <summary>Initialisiert einen Worker.</summary>
        public WorkflowTaskWorker(WorkflowEngine engine, IWorkflowStore store)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <inheritdoc/>
        public WorkflowRuntimeContext Runtime
        {
            set => runtime = value;
        }

        /// <inheritdoc/>
        public override void Process(WorkflowTask task)
        {
            InstanceGate gate = runtime?.Gate;
            if (gate == null)
            {
                // Verdrahtungsfehler: die Laufzeit-Umgebung wurde nie gesetzt. Nicht still ueberspringen.
                LogEnvironment.LogEvent(
                    $"Workflow-Worker ohne Laufzeit-Umgebung (InstanceGate nicht gesetzt). Auftrag " +
                    $"'{task.Trigger}' fuer Instanz '{task.InstanceId}' wird uebersprungen.",
                    LogSeverity.Error);
                return;
            }

            try
            {
                // Pro Instanz nur ein Worker gleichzeitig. Auftraege fuer verschiedene Instanzen
                // laufen parallel; Doppel-Auftraege fuer dieselbe Instanz werden serialisiert und
                // sind harmlos (ein zweiter Vortrieb/Signal findet nichts mehr zu tun).
                lock (gate.For(task.InstanceId))
                {
                    // Einmal laden - fuer Existenzpruefung UND um den Tenant der Instanz zu kennen.
                    WorkflowInstance instance = store.GetInstance(task.InstanceId);
                    if (instance == null)
                    {
                        // Kein stilles Verschlucken: die Instanz ist weg (geloescht) oder fuer diesen
                        // Store nicht sichtbar. Der Auftrag laeuft ins Leere - das wird protokolliert.
                        LogEnvironment.LogEvent(
                            $"Workflow task '{task.Trigger}' for instance '{task.InstanceId}': instance not " +
                            "found (already gone or not visible) - skipped.", LogSeverity.Warning);
                        return;
                    }

                    // Der Runner arbeitet tenant-uebergreifend, aber jede Instanz wird unter IHREM Tenant
                    // vorangetrieben: so sehen tenant-abhaengige Aktivitaeten/DB-Kontexte die richtigen
                    // Daten (siehe WorkflowExecutionScope). Ausserhalb des Scopes bleibt der Store
                    // filterfrei, damit der Poll die Instanzen aller Tenants findet.
                    using (WorkflowExecutionScope.UseTenant(instance.TenantId))
                    {
                        switch (task.Trigger)
                        {
                            case WorkflowTrigger.Advance:
                                engine.Advance(instance);
                                break;
                            case WorkflowTrigger.Timer:
                                engine.TriggerTimers(instance, DateTime.UtcNow);
                                break;
                            case WorkflowTrigger.Signal:
                                engine.SignalWorkflow(task.InstanceId, task.SignalName, task.Payload);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ein fehlgeschlagener Auftrag darf den Worker nicht mitreissen. Der Fehler wird
                // protokolliert (mit Stacktrace); die Engine hat instanz-interne Fehler ohnehin
                // bereits auf Faulted abgebildet.
                LogEnvironment.LogEvent(
                    $"Workflow task '{task.Trigger}' for instance '{task.InstanceId}' failed: {ex.OutlineException()}",
                    LogSeverity.Error);
            }
        }
    }
}
