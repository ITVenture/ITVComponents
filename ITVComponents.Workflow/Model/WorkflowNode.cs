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

        /// <summary>Exklusives Gateway (XOR): genau ein Ausgang wird gewaehlt.</summary>
        ExclusiveGateway,

        /// <summary>Paralleles Gateway (AND): Split auf alle Ausgaenge / Join aller Eingaenge.</summary>
        ParallelGateway,

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
        EventGateway
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
    [JsonDerivedType(typeof(UserActivityNode), "usertask")]
    [JsonDerivedType(typeof(TimerNode), "timer")]
    [JsonDerivedType(typeof(ExclusiveGatewayNode), "xor")]
    [JsonDerivedType(typeof(ParallelGatewayNode), "and")]
    [JsonDerivedType(typeof(CallWorkflowNode), "call")]
    [JsonDerivedType(typeof(BoundaryTimerNode), "boundarytimer")]
    [JsonDerivedType(typeof(SidePathEndNode), "sidepathend")]
    [JsonDerivedType(typeof(TerminateEndNode), "terminateend")]
    [JsonDerivedType(typeof(EventGatewayNode), "eventgateway")]
    public abstract class WorkflowNode : INodeIdentity
    {
        /// <summary>Innerhalb der Definition eindeutige Kennung des Knotens.</summary>
        public string Id { get; set; }

        /// <summary>Anzeigename des Knotens (fuer Modeler und Protokoll).</summary>
        public string Name { get; set; }

        /// <summary>Die Art des Knotens. Redundant zum Serialisierungs-Diskriminator, daher nicht mitserialisiert.</summary>
        [JsonIgnore]
        public abstract NodeKind Kind { get; }

        /// <summary>Grafische Angaben fuer den Modeler; von der Engine ignoriert.</summary>
        public DiagramShape Diagram { get; set; }
    }
}
