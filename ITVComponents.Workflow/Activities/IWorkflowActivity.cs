using System.Collections.Generic;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow.Activities
{
    /// <summary>
    /// Der Kontext, in dem eine Aktivitaet laeuft. Gibt Lese-/Schreibzugriff auf die Variablen der
    /// Instanz sowie den ausloesenden Knoten.
    /// </summary>
    public class WorkflowActivityContext
    {
        /// <summary>
        /// Initialisiert den Kontext.
        /// </summary>
        public WorkflowActivityContext(WorkflowInstance instance, AutomatedActivityNode node)
        {
            Instance = instance;
            Node = node;
        }

        /// <summary>Die laufende Instanz.</summary>
        public WorkflowInstance Instance { get; }

        /// <summary>Der Knoten, der diese Aktivitaet ausloest.</summary>
        public AutomatedActivityNode Node { get; }

        /// <summary>Die Variablen der Instanz; Aktivitaeten schreiben hier ihre Ergebnisse hin.</summary>
        public IDictionary<string, object> Variables => Instance.Variables;

        /// <summary>Die statische Konfiguration des Knotens.</summary>
        public IDictionary<string, object> Configuration => Node.Configuration;
    }

    /// <summary>
    /// Ein automatischer Arbeitsschritt eines Workflows. Implementierungen lesen und schreiben die
    /// Variablen der Instanz ueber den Kontext.
    /// </summary>
    /// <remarks>
    /// Vorerst synchron. Die asynchrone Variante und die Ausfuehrung ueber Factory-Plugins folgen in
    /// spaeteren Phasen; der Vertrag ist bewusst schmal gehalten.
    /// </remarks>
    public interface IWorkflowActivity
    {
        /// <summary>Fuehrt den Schritt aus.</summary>
        /// <param name="context">der Ausfuehrungskontext</param>
        void Execute(WorkflowActivityContext context);
    }
}
