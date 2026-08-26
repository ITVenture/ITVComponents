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

        /// <summary>
        /// Der <b>sprechende</b> Name des Workflows - ueber Versionen hinweg stabil und je Mandant
        /// eindeutig. Gedacht zum <b>Suchen</b> und Wiedererkennen, nicht als Aufhaenger einer Aktion:
        /// welche Zeile dieser Name gerade meint, haengt am Mandanten und an der Version. Wer eine
        /// bestimmte Definition meint, nimmt <see cref="Key"/>.
        /// </summary>
        /// <remarks>
        /// Der JSON-Name bleibt <c>Id</c>, und das mit Absicht: die Definition wird als Ganzes in die
        /// Ablage serialisiert (<c>DefinitionJson</c>) und ist zugleich das Austauschformat. Wanderte der
        /// Name im JSON mit, waere jede bereits abgelegte und jede exportierte Definition nicht mehr
        /// lesbar - der Bestand verlore stillschweigend seinen Namen. Umbenannt wird deshalb nur die
        /// Eigenschaft, nicht das Format.
        /// </remarks>
        [JsonPropertyName("Id")]
        public string TechnicalName { get; set; }

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
        /// Das <b>Feature</b>, das ein Mandant aktiviert haben muss, um diese Definition zu verwenden -
        /// sie zu sehen, sie zu uebernehmen und sie zu starten. Null/leer = keines verlangt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Gedacht fuer oeffentliche Definitionen: der Betreiber pflegt einen Prozess zentral und
        /// entscheidet ueber das Feature, wer ihn ueberhaupt bekommt. Setzen darf das nur ein Sysadmin -
        /// es ist eine Aussage ueber alle Mandanten.
        /// </para>
        /// <para>
        /// <b>Als Name, nicht als Schluessel</b>, aus demselben Grund wie bei <see cref="Key"/>: die
        /// Definition ist exportierbar, und eine Zeilennummer zeigt in der Nachbaranlage auf etwas
        /// anderes. Der Kern setzt den Namen nicht durch - das tut der Mantel, so wie bei
        /// <see cref="UserActivityNode.RequiredPermission"/>.
        /// </para>
        /// <para>
        /// Das Feature ist die Bedingung, die <b>bei jedem Lauf</b> nachgeprueft wird, nicht nur beim
        /// Uebernehmen: es haengt am Mandanten, nicht an einem Benutzer, und ein Zeitplan laeuft ohne
        /// Benutzer. Faellt es weg, setzt der Zeitplan aus, bis es wieder da ist.
        /// </para>
        /// </remarks>
        public string RequiredFeature { get; set; }

        /// <summary>
        /// Die <b>Berechtigung</b>, die ein Benutzer braucht, um diese Definition zu verwenden - sie zu
        /// sehen, sie zu uebernehmen und sie von Hand zu starten. Null/leer = keine verlangt.
        /// </summary>
        /// <remarks>
        /// Anders als <see cref="RequiredFeature"/> beim Feuern eines Zeitplans <b>nicht</b> pruefbar:
        /// dort gibt es keinen Benutzer. Sie gatet deshalb das Uebernehmen und den Start von Hand.
        /// Entzieht man sie jemandem, hebt das eine frueher gesetzte Uebernahme nicht auf.
        /// </remarks>
        public string RequiredPermission { get; set; }

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

        /// <summary>
        /// Nach wie vielen Tagen <b>ab dem Ende</b> eines Vorgangs dieser Definition er archiviert wird.
        /// Null = die globale Vorgabe gilt.
        /// </summary>
        /// <remarks>
        /// <b>Ab dem Ende und nicht ab dem Start</b>: sonst archivierte sich ein Vorgang, der ein Jahr
        /// laeuft, mitten im Betrieb selbst.
        /// </remarks>
        public int? RetentionDays { get; set; }

        /// <summary>
        /// Nach wie vielen Tagen ab dem Ende die <b>Anhang-Inhalte</b> wegfallen. Null = die globale
        /// Vorgabe gilt.
        /// </summary>
        /// <remarks>
        /// Eine eigene Frist, weil die Bytes das eigentliche Volumen sind und laenger oder kuerzer
        /// aufzubewahren sein koennen als der Vorgang selbst. Die Beschreibung des Anhangs (Name,
        /// Groesse, wer, wann) bleibt im Archiv stehen - ein Archiv, das nicht mehr sagen kann "hier war
        /// eine Datei", haette den Vorgang unvollstaendig festgehalten.
        /// </remarks>
        public int? AttachmentRetentionDays { get; set; }

        /// <summary>
        /// Darf ein Mandant den Fristen dieser Definition <b>widersprechen</b> und eigene setzen?
        /// Vorgabe: nein.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Dieselbe Form wie beim Zeitplan eines Ausloesers: dort traegt der Ausloeser das Muster, die
        /// Uebernahme des Mandanten darf ein eigenes setzen - <b>aber nur, wenn
        /// <c>AllowReschedule</c> es erlaubt</b>. Hier ist es dasselbe, nur fuer Fristen.
        /// </para>
        /// <para>
        /// Die Vorgabe ist bewusst die restriktive Seite. Eine Frist, der ein Mandant unbemerkt
        /// widersprechen kann, ist keine Frist - und wo eine Aufbewahrung vorgeschrieben ist, gehoert die
        /// Entscheidung nicht dem, der die Daten loswerden moechte.
        /// </para></remarks>
        public bool AllowTenantRetentionOverride { get; set; }

        /// <summary>
        /// Die kuerzeste Frist bis zum Archivieren, die ein Mandant waehlen darf. Null = keine
        /// Untergrenze.
        /// </summary>
        /// <remarks>
        /// Der Rahmen begrenzt <b>nur den Widerspruch des Mandanten</b>, nicht die Vorgabe der Definition
        /// selbst - die IST die Norm. Gedacht fuer den Fall, dass eine Aufbewahrung vorgeschrieben ist:
        /// der Mandant darf laenger aufheben, aber nicht kuerzer.
        /// </remarks>
        public int? MinTenantRetentionDays { get; set; }

        /// <summary>
        /// Die laengste Frist bis zum Archivieren, die ein Mandant waehlen darf. Null = keine Obergrenze.
        /// </summary>
        /// <remarks>
        /// Die Gegenrichtung zu <see cref="MinTenantRetentionDays"/>: wo eine Loeschfrist gilt, darf ein
        /// Mandant nicht beliebig lange aufheben.
        /// </remarks>
        public int? MaxTenantRetentionDays { get; set; }

        /// <summary>Untergrenze fuer den Widerspruch zur Anhang-Frist, oder null.</summary>
        public int? MinTenantAttachmentRetentionDays { get; set; }

        /// <summary>Obergrenze fuer den Widerspruch zur Anhang-Frist, oder null.</summary>
        public int? MaxTenantAttachmentRetentionDays { get; set; }

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
