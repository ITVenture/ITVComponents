using System.Collections.Generic;

namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Einstiegspunkt eines Workflows. Beim Start bekommt jeder Start-Knoten ein Token.
    /// </summary>
    public class StartNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Start;
    }

    /// <summary>
    /// Endpunkt eines Zweigs. Erreicht ein Token diesen Knoten, wird es verbraucht. Sind danach
    /// keine Tokens mehr aktiv oder wartend, ist der Workflow abgeschlossen.
    /// </summary>
    public class EndNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.End;
    }

    /// <summary>
    /// Ein automatischer Schritt: fuehrt eine Aktivitaet aus (ohne Benutzerinteraktion) und laeuft
    /// dann ueber die einzige ausgehende Kante weiter.
    /// </summary>
    public class AutomatedActivityNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.AutomatedActivity;

        /// <summary>
        /// Verweist auf die auszufuehrende Aktivitaet. Ein <see cref="Activities.IActivityResolver"/>
        /// loest diesen Verweis zur Laufzeit auf (spaeter: Plugin aus der Factory).
        /// </summary>
        public string ActivityRef { get; set; }

        /// <summary>
        /// Optionale, statische Konfiguration fuer die Aktivitaet (z.B. fest verdrahtete
        /// Parameter). Dynamische Werte kommen ueber die Variablen der Instanz.
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Ein Wartepunkt, der den Workflow anhaelt, bis ein benanntes Signal eintrifft (z.B. eine
    /// Benutzereingabe oder ein externes Ereignis). Das Token wird waehrenddessen als wartend
    /// persistiert.
    /// </summary>
    public class WaitNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Wait;

        /// <summary>Der Name des Signals, auf das dieser Knoten wartet.</summary>
        public string SignalName { get; set; }

        /// <summary>
        /// Optionaler CScript-Ausdruck, der ueber den Variablen der Instanz einen
        /// Korrelationsschluessel liefert. Trifft ein Signal mit Korrelationsschluessel ein, findet
        /// es so die richtige wartende Instanz. Null bedeutet Korrelation ueber die Instanz-Id.
        /// </summary>
        public string CorrelationExpression { get; set; }
    }

    /// <summary>
    /// Ein Wartepunkt, der bis zu einem berechneten Zeitpunkt wartet.
    /// </summary>
    public class TimerNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Timer;

        /// <summary>
        /// CScript-Ausdruck, ausgewertet ueber den Variablen der Instanz. Liefert entweder einen
        /// <see cref="System.DateTime"/> (absoluter Faelligkeitszeitpunkt) oder eine
        /// <see cref="System.TimeSpan"/> (Wartedauer ab Betreten des Knotens).
        /// </summary>
        public string DueExpression { get; set; }
    }

    /// <summary>
    /// Exklusives Gateway (XOR): waehlt genau einen ausgehenden Pfad. Die ausgehenden Kanten tragen
    /// CScript-Bedingungen; die erste erfuellte gewinnt. Trifft keine zu, wird der Default-Ausgang
    /// genommen.
    /// </summary>
    public class ExclusiveGatewayNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.ExclusiveGateway;

        /// <summary>
        /// Id der ausgehenden Kante, die genommen wird, wenn keine Bedingung zutrifft. Null
        /// bedeutet: trifft keine Bedingung zu, faellt die Instanz auf Faulted.
        /// </summary>
        public string DefaultFlowId { get; set; }
    }

    /// <summary>
    /// Paralleles Gateway (AND). Mit mehreren Ausgaengen wirkt es als Split (ein Token je Ausgang),
    /// mit mehreren Eingaengen als Join (feuert erst, wenn auf jedem Eingang ein Token liegt).
    /// </summary>
    /// <remarks>
    /// Modelltyp ab Phase 0 vorhanden; die Ausfuehrung (Split/Join-Synchronisierung) folgt in
    /// Phase 2. Bis dahin lehnt die Engine das Betreten mit einer klaren Meldung ab.
    /// </remarks>
    public class ParallelGatewayNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.ParallelGateway;
    }
}
