using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

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

        /// <summary>
        /// Die <b>technische</b> Kennung dieser Definitionszeile - vom Ablageort vergeben, dort der
        /// Primaerschluessel. 0 = noch nicht abgelegt.
        /// </summary>
        /// <remarks>
        /// Nicht Teil des Austauschformats (siehe <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/>):
        /// sie gilt nur in EINER Ablage und waere in einer exportierten Datei eine Zahl, die anderswo auf
        /// etwas anderes zeigt. Wofuer sie da ist: eine laufende Instanz verweist ueber sie auf GENAU die
        /// Definition, mit der sie gestartet wurde. Ueber Name und Version allein waere der Verweis
        /// mehrdeutig, sobald es eine oeffentliche und eine mandanteneigene Definition desselben Namens
        /// gibt - und die Instanz liefe beim naechsten Vortrieb still auf einem anderen Graphen weiter.
        /// </remarks>
        [JsonIgnore]
        public int Key { get; set; }

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

        /// <summary>
        /// Ob diese Definition <b>oeffentlich</b> ist (fuer alle Mandanten sichtbar und startbar).
        /// </summary>
        /// <remarks>
        /// Die ausdrueckliche Entscheidung, und deshalb ein eigenes Feld: ohne sie waere „kein Mandant
        /// gesetzt" nicht von „oeffentlich gemeint" zu unterscheiden, und die Ablage legte im Zweifel
        /// still eine oeffentliche Definition an. Setzt jemand beides (oeffentlich UND ein Mandant),
        /// ist das ein Widerspruch und keine Auslegungsfrage - die Ablage weist ihn ab.
        /// <para>
        /// Eine oeffentliche Definition anzulegen oder zu aendern verlangt eine eigene Berechtigung
        /// (<c>Workflow.DesignPublic</c>); die Oberflaeche macht daraus zwei getrennte Wege.
        /// </para>
        /// </remarks>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Gesetzt, wenn die Definition Validierungsfehler hat und deshalb NICHT gestartet werden darf. So
        /// laesst sich eine fehlerhafte Definition speichern (Zwischenstand), ohne dass daraus versehentlich
        /// eine Instanz entsteht, die sofort faultet. Der Editor setzt das Flag beim Speichern (Fehler =
        /// true, sonst false; Warnungen setzen es NICHT); <see cref="Instances"/> bzw.
        /// <c>WorkflowEngine.CreateInstance</c> verweigert bei true den Start mit einer klaren Meldung.
        /// Bereits laufende Instanzen laufen weiter - das Flag sperrt nur den START.
        /// </summary>
        public bool DisabledForStart { get; set; }

        /// <summary>
        /// Die Vorgabe-Dringlichkeit fuer neue Instanzen dieser Definition (<b>kleinere Zahl =
        /// wichtiger</b>, siehe <see cref="Instances.WorkflowPriority"/>). Null = der Standard
        /// (<see cref="Instances.WorkflowPriority.Normal"/>). Wer eine Instanz startet, kann den Wert
        /// einzeln uebersteuern.
        /// </summary>
        /// <remarks>
        /// Gedacht fuer Definitionen, deren Rolle von vornherein feststeht: eine naechtliche
        /// Aufraeum-Kaskade laeuft dauerhaft auf <see cref="Instances.WorkflowPriority.Lowest"/>, eine
        /// Freigabe mit Kundenkontakt auf <see cref="Instances.WorkflowPriority.High"/>.
        /// </remarks>
        public int? DefaultPriority { get; set; }

        /// <summary>
        /// Uebersteuert fuer Instanzen dieser Definition die Mindest-Stufe des Ablauf-Protokolls. Null =
        /// es gilt der Filter der Engine bzw. der prozessweite
        /// <c>WorkflowHistoryFilter.Default</c>. Damit laesst sich EIN Workflow ausfuehrlich
        /// mitschreiben, waehrend der Rest knapp bleibt (oder umgekehrt).
        /// </summary>
        public Instances.HistorySeverity? MinHistorySeverity { get; set; }

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

        /// <summary>
        /// Die Start-Knoten der <b>obersten Ebene</b> - also die, an denen eine Instanz beginnt.
        /// </summary>
        /// <remarks>
        /// Start-Knoten INNERHALB eines <see cref="SubProcessNode"/> gehoeren nicht dazu: sie starten
        /// ihren Abschnitt, nicht den Workflow. Wuerden sie mitgezaehlt, bekaeme jede neue Instanz
        /// zusaetzliche Tokens mitten in ihren Subprozessen.
        /// </remarks>
        public IEnumerable<StartNode> StartNodes()
        {
            return Nodes.OfType<StartNode>().Where(n => n.ParentNodeId == null);
        }

        /// <summary>Die Knoten, die unmittelbar in dem angegebenen Behaelter liegen (null = oberste Ebene).</summary>
        public IEnumerable<WorkflowNode> NodesIn(string parentNodeId)
        {
            return Nodes.Where(n => n != null && n.ParentNodeId == parentNodeId);
        }

        /// <summary>Der Start-Knoten eines Subprozesses, oder null.</summary>
        public StartNode StartNodeOf(string subProcessId)
        {
            return Nodes.OfType<StartNode>().FirstOrDefault(n => n.ParentNodeId == subProcessId);
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
