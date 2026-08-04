using System.Text.Json.Serialization;

namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Die Art eines Knotens. Dient der Engine zur Fallunterscheidung und als
    /// Serialisierungs-Diskriminator fuer den Modeler.
    /// </summary>
    public enum NodeKind
    {
        /// <summary>Einstiegspunkt eines Workflows.</summary>
        Start,

        /// <summary>Endpunkt eines Zweigs; verbraucht ein Token.</summary>
        End,

        /// <summary>Automatischer Schritt, der eine Aktivitaet ausfuehrt.</summary>
        AutomatedActivity,

        /// <summary>Wartepunkt, der auf ein benanntes Signal (Benutzer/Ereignis) wartet.</summary>
        Wait,

        /// <summary>Eine Aufgabe fuer einen Menschen: wartet, bis sie in der Oberflaeche erledigt wird.</summary>
        UserActivity,

        /// <summary>Wartepunkt, der bis zu einem Zeitpunkt wartet.</summary>
        Timer,

        /// <summary>
        /// <b>Sendet</b> eine Nachricht oder einen Rundruf - das Gegenstueck zum Wartepunkt. Haelt den
        /// Zweig nicht an.
        /// </summary>
        SendMessage,

        /// <summary>Exklusives Gateway (XOR): genau ein Ausgang wird gewaehlt.</summary>
        ExclusiveGateway,

        /// <summary>Paralleles Gateway (AND): Split auf alle Ausgaenge / Join aller Eingaenge.</summary>
        ParallelGateway,

        /// <summary>
        /// Inklusives Gateway (OR): Split auf <b>alle zutreffenden</b> Ausgaenge; der zugehoerige Join
        /// wartet auf genau die Zweige, die dieser Split aktiviert hat.
        /// </summary>
        InclusiveGateway,

        /// <summary>Ruft einen anderen Workflow als Subworkflow auf (mit Ein-/Ausgabewerten).</summary>
        CallWorkflow,

        /// <summary>
        /// Ein Fristen-Timer, der an einem Schritt <b>haengt</b> und einen Nebenpfad ausloest (Eskalation),
        /// ohne den Hauptfluss anzuhalten.
        /// </summary>
        BoundaryTimer,

        /// <summary>Endpunkt eines <b>Nebenpfads</b>: verbraucht das Token, ohne den Workflow zu beenden.</summary>
        SidePathEnd,

        /// <summary>
        /// Beendet die <b>ganze Instanz</b> sofort - auch alle anderen laufenden Zweige und
        /// Subworkflows.
        /// </summary>
        TerminateEnd,

        /// <summary>
        /// Ereignisbasiertes Gateway: wartet auf <b>mehrere</b> Ereignisse gleichzeitig; das erste, das
        /// eintrifft, gewinnt, die uebrigen werden verworfen.
        /// </summary>
        EventGateway,

        /// <summary>
        /// Ein <b>eingebetteter</b> Teilablauf: eigene Knoten im selben Graphen, eigener Variablen-Scope,
        /// aber KEINE eigene Instanz.
        /// </summary>
        SubProcess,

        /// <summary>
        /// Ein <b>Rueckabwicklungs-Pfad</b>, der an einem Schritt haengt: er laeuft nicht im normalen
        /// Fluss, sondern nur, wenn spaeter rueckabgewickelt wird.
        /// </summary>
        Compensation,

        /// <summary>
        /// Loest die <b>Rueckabwicklung</b> aus: die bereits erledigten Schritte werden in umgekehrter
        /// Reihenfolge zurueckgenommen.
        /// </summary>
        Compensate
    }

    /// <summary>
    /// Ein Knoten einer Workflow-Definition. Basisklasse aller Knotentypen.
    /// </summary>
    /// <remarks>
    /// Ein Knoten ist reine Definition (Daten), kein Laufzeitobjekt - der Laufzeitfortschritt lebt
    /// in den Tokens einer Instanz. Erst dadurch ist eine Instanz vollstaendig serialisierbar und
    /// wiederaufnehmbar.
    ///
    /// Die Polymorphie wird ueber stabile, selbstgewaehlte Diskriminatoren ("kind") abgebildet -
    /// bewusst nicht ueber .NET-Typnamen, damit sich gespeicherte Definitionen bei Umbenennungen
    /// nicht loesen und der spaetere Modeler denselben stabilen Vertrag nutzen kann.
    /// </remarks>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(StartNode), "start")]
    [JsonDerivedType(typeof(EndNode), "end")]
    [JsonDerivedType(typeof(AutomatedActivityNode), "activity")]
    [JsonDerivedType(typeof(WaitNode), "wait")]
    [JsonDerivedType(typeof(SendMessageNode), "send")]
    [JsonDerivedType(typeof(UserActivityNode), "usertask")]
    [JsonDerivedType(typeof(TimerNode), "timer")]
    [JsonDerivedType(typeof(ExclusiveGatewayNode), "xor")]
    [JsonDerivedType(typeof(ParallelGatewayNode), "and")]
    [JsonDerivedType(typeof(InclusiveGatewayNode), "or")]
    [JsonDerivedType(typeof(CallWorkflowNode), "call")]
    [JsonDerivedType(typeof(BoundaryTimerNode), "boundarytimer")]
    [JsonDerivedType(typeof(SidePathEndNode), "sidepathend")]
    [JsonDerivedType(typeof(TerminateEndNode), "terminateend")]
    [JsonDerivedType(typeof(EventGatewayNode), "eventgateway")]
    [JsonDerivedType(typeof(SubProcessNode), "subprocess")]
    [JsonDerivedType(typeof(CompensationNode), "compensation")]
    [JsonDerivedType(typeof(CompensateNode), "compensate")]
    public abstract class WorkflowNode : INodeIdentity
    {
        /// <summary>Innerhalb der Definition eindeutige Kennung des Knotens.</summary>
        public string Id { get; set; }

        /// <summary>
        /// Die Id des <see cref="SubProcessNode"/>, in dem dieser Knoten liegt; null auf der obersten
        /// Ebene.
        /// </summary>
        /// <remarks>
        /// Die Zugehoerigkeit haengt am Kind und nicht als Knotenliste am Subprozess - der Graph bleibt
        /// dadurch <b>flach</b>. Das ist keine Schoenheitsfrage: Knotenindex, ausgehende Kanten,
        /// Validierung, Layout und Serialisierung arbeiten alle ueber die eine flache Liste. Ein
        /// verschachteltes Modell haette jede dieser Stellen angefasst, und der Gewinn waere nur die
        /// Baumform im JSON gewesen.
        /// </remarks>
        public string ParentNodeId { get; set; }

        /// <summary>Anzeigename des Knotens (fuer Modeler und Protokoll).</summary>
        public string Name { get; set; }

        /// <summary>Die Art des Knotens. Redundant zum Serialisierungs-Diskriminator, daher nicht mitserialisiert.</summary>
        [JsonIgnore]
        public abstract NodeKind Kind { get; }

        /// <summary>Grafische Angaben fuer den Modeler; von der Engine ignoriert.</summary>
        public DiagramShape Diagram { get; set; }
    }
}
