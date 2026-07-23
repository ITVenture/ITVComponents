using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow.Stores
{
    /// <summary>
    /// Persistiert Workflow-Definitionen und -Instanzen und beantwortet die Abfragen, die die
    /// Engine zum Wiederaufnehmen braucht.
    /// </summary>
    /// <remarks>
    /// Bewusst abstrahiert: die Engine haengt nur an diesem Vertrag. Standard-Implementierung wird
    /// ein DB-Store (EFRepo); die In-Memory-Variante dient Tests und einfachen Szenarien. Die
    /// Abfragen <see cref="FindWaitingForSignal"/> und <see cref="FindDueTimers"/> sind der Grund,
    /// warum der Store mehr koennen muss als blosses Ablegen von Bytes.
    /// </remarks>
    public interface IWorkflowStore
    {
        /// <summary>Legt eine Definition ab (Upsert nach Id+Version).</summary>
        void SaveDefinition(WorkflowDefinition definition);

        /// <summary>
        /// Laedt eine Definition. Ist <paramref name="version"/> null, wird die hoechste Version
        /// geliefert. Liefert null, wenn nichts gefunden wird.
        /// </summary>
        WorkflowDefinition GetDefinition(string definitionId, int? version = null);

        /// <summary>Legt eine Instanz ab (Upsert nach Id). Setzt den Aenderungszeitpunkt.</summary>
        void SaveInstance(WorkflowInstance instance);

        /// <summary>Laedt eine Instanz, oder null.</summary>
        WorkflowInstance GetInstance(string instanceId);

        /// <summary>
        /// Findet Instanzen mit einem wartenden Token auf das angegebene Signal. Ist
        /// <paramref name="correlationKey"/> gesetzt, werden nur Instanzen mit passendem
        /// Korrelationsschluessel (oder passender Id) geliefert.
        /// </summary>
        IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null);

        /// <summary>Findet Instanzen mit einem faelligen Timer-Token (DueUtc &lt;= nowUtc).</summary>
        IEnumerable<WorkflowInstance> FindDueTimers(DateTime nowUtc);
    }
}
