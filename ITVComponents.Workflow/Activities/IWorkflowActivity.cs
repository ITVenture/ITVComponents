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
        /// <param name="instance">die laufende Instanz</param>
        /// <param name="node">der ausloesende Knoten</param>
        /// <param name="inputs">
        /// die von der Engine bereits aufgeloesten Eingabewerte (aus den Bindungen des Knotens), oder
        /// null fuer einen leeren Satz
        /// </param>
        /// <param name="outputs">
        /// das Ziel-Dictionary fuer die deklarierten Ausgaben (die Engine bildet es nach der
        /// Ausfuehrung auf die Variablen ab), oder null fuer einen frischen Satz
        /// </param>
        public WorkflowActivityContext(WorkflowInstance instance, AutomatedActivityNode node,
            IDictionary<string, object> inputs = null, IDictionary<string, object> outputs = null)
        {
            Instance = instance;
            Node = node;
            Inputs = inputs ?? new Dictionary<string, object>();
            Outputs = outputs ?? new Dictionary<string, object>();
        }

        /// <summary>Die laufende Instanz.</summary>
        public WorkflowInstance Instance { get; }

        /// <summary>Der Knoten, der diese Aktivitaet ausloest.</summary>
        public AutomatedActivityNode Node { get; }

        /// <summary>Die Variablen der Instanz; Aktivitaeten schreiben hier ihre Ergebnisse hin.</summary>
        public IDictionary<string, object> Variables => Instance.Variables;

        /// <summary>Die statische Konfiguration des Knotens (generische Aktivitaeten).</summary>
        public IDictionary<string, object> Configuration => Node.Configuration;

        /// <summary>
        /// Die von der Engine aufgeloesten Eingabewerte, je deklariertem Eingabeparameter. Eine
        /// Aktivitaet liest ihre Parameter hier, statt Variablen selbst aufzuloesen.
        /// </summary>
        public IDictionary<string, object> Inputs { get; }

        /// <summary>
        /// Ziel fuer die deklarierten Ausgaben: die Aktivitaet legt ihre Ergebnisse hier je
        /// Ausgabeparameter ab; die Engine bildet sie anschliessend gemaess der
        /// <see cref="AutomatedActivityNode.Outputs"/>-Bindungen auf Instanz-Variablen ab.
        /// </summary>
        public IDictionary<string, object> Outputs { get; }
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
