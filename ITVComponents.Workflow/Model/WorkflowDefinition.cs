using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Die statische Beschreibung eines Workflows: ein Graph aus Knoten und Kanten. Reine Daten,
    /// unabhaengig von einer laufenden Instanz - damit versions- und serialisierbar und Grundlage
    /// des spaeteren visuellen Modelers.
    /// </summary>
    public class WorkflowDefinition
    {
        private Dictionary<string, WorkflowNode> nodeIndex;

        /// <summary>Fachliche Kennung des Workflows (ueber Versionen hinweg stabil).</summary>
        public string Id { get; set; }

        /// <summary>Version dieser Definition. Laufende Instanzen bleiben an ihrer Version.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Anzeigename des Workflows.</summary>
        public string Name { get; set; }

        /// <summary>
        /// Name des Tenants, dem die Definition gehoert, oder null fuer eine oeffentliche Definition.
        /// Oeffentliche Definitionen sind fuer alle Tenants sichtbar und im Kontext eines beliebigen
        /// Tenants startbar; tenant-eigene nur im eigenen Tenant. Der Kern wertet den Wert nicht aus.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>Die Knoten des Graphen.</summary>
        public List<WorkflowNode> Nodes { get; set; } = new List<WorkflowNode>();

        /// <summary>Die gerichteten Kanten des Graphen.</summary>
        public List<SequenceFlow> Flows { get; set; } = new List<SequenceFlow>();

        /// <summary>
        /// Liefert den Knoten mit der angegebenen Id, oder null.
        /// </summary>
        public WorkflowNode GetNode(string id)
        {
            if (id == null)
            {
                return null;
            }

            // Der Index wird beim ersten Zugriff aufgebaut. Nach Aenderungen an Nodes bitte
            // RebuildIndex() rufen - fuer laufende Instanzen ist die Definition ohnehin unveraendert.
            nodeIndex ??= BuildIndex();
            return nodeIndex.TryGetValue(id, out WorkflowNode node) ? node : null;
        }

        /// <summary>Verwirft den zwischengespeicherten Knotenindex nach Aenderungen an <see cref="Nodes"/>.</summary>
        public void RebuildIndex()
        {
            nodeIndex = BuildIndex();
        }

        /// <summary>Alle vom angegebenen Knoten ausgehenden Kanten, in Definitionsreihenfolge.</summary>
        public IReadOnlyList<SequenceFlow> OutgoingFlows(string nodeId)
        {
            return Flows.Where(f => f.SourceId == nodeId).ToList();
        }

        /// <summary>Alle in den angegebenen Knoten eingehenden Kanten, in Definitionsreihenfolge.</summary>
        public IReadOnlyList<SequenceFlow> IncomingFlows(string nodeId)
        {
            return Flows.Where(f => f.TargetId == nodeId).ToList();
        }

        /// <summary>Alle Start-Knoten der Definition.</summary>
        public IEnumerable<StartNode> StartNodes()
        {
            return Nodes.OfType<StartNode>();
        }

        private Dictionary<string, WorkflowNode> BuildIndex()
        {
            var index = new Dictionary<string, WorkflowNode>();
            foreach (WorkflowNode node in Nodes)
            {
                if (node?.Id != null)
                {
                    index[node.Id] = node;
                }
            }

            return index;
        }
    }
}
