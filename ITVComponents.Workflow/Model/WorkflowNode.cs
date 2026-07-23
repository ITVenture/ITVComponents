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

        /// <summary>Wartepunkt, der bis zu einem Zeitpunkt wartet.</summary>
        Timer,

        /// <summary>Exklusives Gateway (XOR): genau ein Ausgang wird gewaehlt.</summary>
        ExclusiveGateway,

        /// <summary>Paralleles Gateway (AND): Split auf alle Ausgaenge / Join aller Eingaenge.</summary>
        ParallelGateway
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
    [JsonDerivedType(typeof(TimerNode), "timer")]
    [JsonDerivedType(typeof(ExclusiveGatewayNode), "xor")]
    [JsonDerivedType(typeof(ParallelGatewayNode), "and")]
    public abstract class WorkflowNode
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
