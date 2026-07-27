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
        /// Verweist auf die auszufuehrende Aktivitaet. Ein <see cref="Activities.IActivityHost"/>
        /// loest diesen Verweis zur Laufzeit auf (Plugin aus der Factory).
        /// </summary>
        public string ActivityRef { get; set; }

        /// <summary>
        /// Optionale, statische Konfiguration fuer <b>generische</b> Aktivitaeten (z.B. Skripte), die
        /// keine deklarierten Parameter haben und ihre Konfiguration selbst aus diesem Dictionary
        /// lesen. Spezialisierte (Plugin-)Aktivitaeten mit deklarierten Parametern nutzen stattdessen
        /// <see cref="Inputs"/> und <see cref="Outputs"/>.
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Datenfluss <b>hinein</b>: bindet die deklarierten Eingabeparameter der Aktivitaet an
        /// Wertquellen (Konstante, Variable oder Ausdruck). Die Engine loest diese Bindungen vor der
        /// Ausfuehrung auf und stellt die Werte ueber
        /// <see cref="Activities.WorkflowActivityContext.Inputs"/> bereit.
        /// </summary>
        public List<ActivityInputBinding> Inputs { get; set; } = new List<ActivityInputBinding>();

        /// <summary>
        /// Datenfluss <b>heraus</b>: bildet die deklarierten Ausgabeparameter der Aktivitaet auf
        /// Instanz-Variablen ab. Nach der Ausfuehrung schreibt die Engine die von der Aktivitaet in
        /// <see cref="Activities.WorkflowActivityContext.Outputs"/> abgelegten Werte in diese Variablen.
        /// </summary>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>
        /// Wie die Ausgaben in den Scope einfliessen. Standard <see cref="ActivityScopeMode.Extend"/>
        /// (additiv). <see cref="ActivityScopeMode.Replace"/> macht den Knoten zu einer
        /// <b>Konsolidierung</b>: danach besteht der Scope nur noch aus den Ausgaben (plus
        /// <see cref="RetainVariables"/>).
        /// </summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>
        /// Bei <see cref="ActivityScopeMode.Replace"/>: Namen von Variablen, die ueber die
        /// Konsolidierung hinaus erhalten bleiben (z.B. langlebige Korrelations-/Konfig-Werte). Bei
        /// <see cref="ActivityScopeMode.Extend"/> ohne Wirkung.
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();

        /// <summary>
        /// Optionales Ausfuehrungs-Ziel fuer den verteilten Betrieb: der (freie) Name eines Host-Ziels,
        /// auf dem diese Aktivitaet laufen MUSS (z.B. "backend", "web"). Ist der Wert gesetzt und der
        /// aktuelle Runner bedient dieses Ziel nicht, parkt der Zweig hier
        /// (<see cref="Instances.TokenStatus.WaitingForTarget"/>) und wird von einem Runner mit passendem
        /// Ziel aufgenommen und dort ausgefuehrt. Null oder leer bedeutet: die Aktivitaet laeuft auf einem
        /// beliebigen Runner (der Standard - deckt den nicht-verteilten Betrieb ab).
        /// </summary>
        public string ExecutionTarget { get; set; }

        /// <summary>
        /// Optionaler <b>Fehler-Ausgang</b>: die Id der ausgehenden Kante, die genommen wird, wenn die
        /// Aktivitaet scheitert - durch eine Exception ODER kontrolliert ueber
        /// <see cref="Activities.WorkflowActivityContext.Fail"/>. Der Erfolgs-Ausgang ist dann die einzige
        /// andere ausgehende Kante. Ist der Wert null/leer, faultet ein Fehler wie bisher die ganze Instanz
        /// (Standard, rueckwaerts-kompatibel). Erlaubt Fehlerbehandlung im Graphen (Retry-Schleifen,
        /// Verzweigung nach Fehlerart/-anzahl, Benutzer-Korrektur).
        /// </summary>
        public string ErrorFlowId { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Instanz-Variable, die die Fehlermeldung erhaelt (fuer Anzeige/
        /// Verzweigung). Null/leer = nicht setzen.
        /// </summary>
        public string ErrorVariable { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Instanz-Variable, die den <b>Fehlversuchs-Zaehler</b> erhaelt - um
        /// +1 erhoeht bei jedem Fehlerlauf dieses Knotens, auf 0 zurueckgesetzt bei Erfolg. Damit laesst
        /// sich im Graphen nach Anzahl der Versuche verzweigen (z.B. 1x Auto-Korrektur, dann Benutzer-UI,
        /// dann Aufgeben). Null/leer = nicht mitzaehlen.
        /// </summary>
        public string AttemptVariable { get; set; }
    }

    /// <summary>
    /// Ruft einen anderen Workflow als <b>Subworkflow</b> auf. Der aufrufende Zweig parkt, bis der
    /// Subworkflow endet; danach laeuft er ueber die einzige ausgehende Kante weiter. So laesst sich ein
    /// parametrierter Workflow wie eine Aktivitaet in einen anderen einbauen (mit Ein- und Ausgabewerten).
    /// </summary>
    /// <remarks>
    /// Der Subworkflow laeuft als eigene, vollwertige Instanz (eigene Tokens/History/Monitoring), erbt den
    /// Tenant des Elternprozesses und ist ueber einen Rueck-Link mit dem wartenden Eltern-Token verbunden.
    /// Endet er, werden seine End-Variablen ueber <see cref="Outputs"/> auf die Eltern-Variablen abgebildet;
    /// faultet er, faultet standardmaessig der aufrufende Knoten. Das Vorantreiben von Subworkflows
    /// uebernimmt der <c>WorkflowRunner</c> (der nebenlaeufige Ausfuehrungspfad).
    /// </remarks>
    public class CallWorkflowNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.CallWorkflow;

        /// <summary>Fachliche Id der aufzurufenden (Sub-)Workflow-Definition.</summary>
        public string SubDefinitionId { get; set; }

        /// <summary>
        /// Optionale feste Version der Subworkflow-Definition. Null = jeweils die hoechste Version (wie beim
        /// Start eines Workflows).
        /// </summary>
        public int? SubDefinitionVersion { get; set; }

        /// <summary>
        /// Datenfluss <b>hinein</b>: bindet Startvariablen des Subworkflows an Wertquellen des
        /// Elternprozesses (Konstante, Variable oder Ausdruck). <see cref="ActivityInputBinding.Parameter"/>
        /// ist der Name der zu setzenden Kind-Variable.
        /// </summary>
        public List<ActivityInputBinding> Inputs { get; set; } = new List<ActivityInputBinding>();

        /// <summary>
        /// Datenfluss <b>heraus</b>: bildet End-Variablen des Subworkflows auf Eltern-Variablen ab.
        /// <see cref="ActivityOutputBinding.Parameter"/> ist der Name der Kind-Endvariable,
        /// <see cref="ActivityOutputBinding.Variable"/> die Ziel-Variable im Elternprozess.
        /// </summary>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>
        /// Optionaler <b>Fehler-Ausgang</b>: die Id der ausgehenden Kante, die genommen wird, wenn der
        /// Subworkflow scheitert (faultet oder abgebrochen wird). Der Erfolgs-Ausgang ist dann die einzige
        /// andere ausgehende Kante. Ist der Wert null/leer, faultet ein gescheiterter Subworkflow wie bisher
        /// den aufrufenden Knoten (Standard, rueckwaerts-kompatibel). Erlaubt Fehlerbehandlung im Graphen
        /// (Alternativpfad, Kompensation, oder - mit <see cref="AttemptVariable"/> - Wiederholung des
        /// Subworkflows).
        /// </summary>
        public string ErrorFlowId { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Eltern-Variable, die die Fehlermeldung des Subworkflows erhaelt.
        /// Null/leer = nicht setzen.
        /// </summary>
        public string ErrorVariable { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Eltern-Variable, die den <b>Fehlversuchs-Zaehler</b> erhaelt -
        /// +1 bei jedem gescheiterten Subworkflow-Lauf, 0 bei Erfolg. Ist er gesetzt, bekommt jeder Versuch
        /// eine EIGENE Kind-Instanz (die Fehlerkante kann also zum selben Knoten zurueckfuehren = echte
        /// Wiederholung); ohne ihn wird der Subworkflow nicht neu angelegt. Null/leer = nicht mitzaehlen.
        /// </summary>
        public string AttemptVariable { get; set; }
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
