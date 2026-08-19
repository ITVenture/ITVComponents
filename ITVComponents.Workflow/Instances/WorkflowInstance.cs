using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ITVComponents.Workflow.Instances
{
    /// <summary>
    /// Der Status einer Workflow-Instanz.
    /// </summary>
    public enum WorkflowStatus
    {
        /// <summary>Es gibt aktive Tokens; die Instanz kann vorangetrieben werden.</summary>
        Running,

        /// <summary>Alle Tokens warten (Signal/Timer); die Instanz ruht bis zu einem Trigger.</summary>
        Waiting,

        /// <summary>Alle Tokens sind verbraucht; der Workflow ist regulaer beendet.</summary>
        Completed,

        /// <summary>Die Ausfuehrung ist auf einen Fehler gelaufen.</summary>
        Faulted,

        /// <summary>Von aussen abgebrochen.</summary>
        Cancelled
    }

    /// <summary>
    /// Der Schweregrad eines Protokolleintrags - fuer Filterung/Nachvollzug im Monitoring.
    /// </summary>
    public enum HistorySeverity
    {
        /// <summary>Schritt-fuer-Schritt-Details (z.B. Betreten eines Knotens) - normalerweise ausgeblendet.</summary>
        Verbose,

        /// <summary>Meilenstein im normalen Ablauf (Start, Join, Warten, Signal, Abschluss).</summary>
        Info,

        /// <summary>Auffaellig, aber kein Fehler (z.B. Abbruch von aussen).</summary>
        Warning,

        /// <summary>Die Ausfuehrung ist auf einen Fehler gelaufen.</summary>
        Error
    }

    /// <summary>
    /// Ein Eintrag im Ausfuehrungsprotokoll einer Instanz.
    /// </summary>
    public class HistoryEntry
    {
        /// <summary>Zeitpunkt des Ereignisses (UTC).</summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>Betroffener Knoten, oder null bei instanzweiten Ereignissen.</summary>
        public string NodeId { get; set; }

        /// <summary>Kurzbezeichnung des Ereignisses (z.B. Entered, Completed, Waiting, Faulted).</summary>
        public string Event { get; set; }

        /// <summary>Freitext-Detail, oder null.</summary>
        public string Detail { get; set; }

        /// <summary>Der Schweregrad des Eintrags. Standard <see cref="HistorySeverity.Info"/>.</summary>
        public HistorySeverity Severity { get; set; } = HistorySeverity.Info;
    }

    /// <summary>
    /// Ein <b>vorgemerkter</b> Schritt: er ist erfolgreich durchgelaufen und traegt einen
    /// Rueckabwicklungs-Pfad (<see cref="Model.CompensationNode"/>), kann also zurueckgenommen werden.
    /// </summary>
    /// <remarks>
    /// Der Variablen-Schnappschuss ist der Kern: der Pfad laeuft mit dem Stand, den der Schritt bei
    /// seiner Vollendung hinterlassen hat, nicht mit dem aktuellen. Eine Stornierung braucht die
    /// Buchungsnummer von damals - der laufende Prozess hat sie laengst ueberschrieben.
    /// <para>
    /// Bewusst eine eigene Liste und nicht aus dem Protokoll abgeleitet: das Protokoll ist seit dem
    /// Filter nicht mehr vollstaendig, und was rueckabgewickelt werden muss, darf nicht davon abhaengen,
    /// wie gespraechig jemand sein Log eingestellt hat.
    /// </para></remarks>
    public class CompensationEntry
    {
        /// <summary>
        /// Die Identitaet dieses Eintrags - stabil ueber den Merge nebenlaeufiger Zweige hinweg.
        /// </summary>
        /// <remarks>
        /// Nicht die <see cref="Sequence"/>: zwei parallele Zweige merken jeder auf seiner eigenen Kopie
        /// vor und vergeben dabei dieselbe Nummer. Beim Zusammenfuehren waeren das zwei verschiedene
        /// Eintraege mit gleicher Nummer - und „dieser eine ist zurueckgenommen" traefe still beide.
        /// </remarks>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Die Reihenfolge der Vollendung. Rueckabgewickelt wird absteigend; bei Gleichstand (parallele
        /// Zweige) entscheidet die Reihenfolge in der Liste - beide Reihenfolgen sind vertretbar, weil
        /// die Schritte tatsaechlich nebeneinander liefen.
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>Der Knoten, der erledigt wurde.</summary>
        public string NodeId { get; set; }

        /// <summary>Der <see cref="Model.CompensationNode"/>, der seine Ruecknahme beschreibt.</summary>
        public string HandlerNodeId { get; set; }

        /// <summary>
        /// Der Abschnitt, in dem der Schritt liegt (<see cref="Model.WorkflowNode.ParentNodeId"/>), oder
        /// null fuer die oberste Ebene. Bestimmt, welcher <see cref="Model.CompensateNode"/> ihn meint.
        /// </summary>
        public string ScopeNodeId { get; set; }

        /// <summary>Der Variablen-Stand bei der Vollendung des Schritts.</summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

        /// <summary>Wurde dieser Schritt bereits zurueckgenommen (oder wird gerade)?</summary>
        /// <remarks>
        /// Wird gesetzt, sobald die Ruecknahme <b>beginnt</b> - nicht erst, wenn sie fertig ist. Sonst
        /// griffe der naechste Durchgang denselben Eintrag erneut, und der Pfad liefe doppelt.
        /// </remarks>
        public bool Compensated { get; set; }
    }

    /// <summary>
    /// Eine laufende (oder ruhende/beendete) Ausfuehrung einer <see cref="Model.WorkflowDefinition"/>.
    /// </summary>
    /// <remarks>
    /// Der vollstaendige Laufzeitzustand steckt hier als Daten: Variablen, Tokens, Status, Protokoll.
    /// Genau das erlaubt Anhalten, Serialisieren und spaeteres Fortsetzen - es gibt keinen Zustand,
    /// der an Objektinstanzen oder Threads klebt.
    /// </remarks>
    public class WorkflowInstance
    {
        /// <summary>Eindeutige Kennung dieser Instanz.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Die <b>technische</b> Kennung der Definitionszeile, mit der diese Instanz gestartet wurde -
        /// der eigentliche Verweis.
        /// </summary>
        /// <remarks>
        /// <see cref="DefinitionId"/> und <see cref="DefinitionVersion"/> stehen weiterhin daneben, aber
        /// als <b>Anzeige und Filter</b>, nicht als Verweis: ueber Name und Version allein waere nicht
        /// entscheidbar, ob die oeffentliche oder die mandanteneigene Definition desselben Namens gemeint
        /// ist. Die Engine laedt ueber diese Kennung - eine laufende Instanz bleibt damit an genau dem
        /// Graphen, mit dem sie angefangen hat.
        /// </remarks>
        public int DefinitionKey { get; set; }

        /// <summary>Fachliche Id der Definition (Anzeige und Filter - der Verweis ist <see cref="DefinitionKey"/>).</summary>
        public string DefinitionId { get; set; }

        /// <summary>Version der Definition, an der diese Instanz laeuft.</summary>
        public int DefinitionVersion { get; set; }

        /// <summary>
        /// Name des Tenants, in dessen Kontext diese Instanz laeuft, oder null fuer eine tenant-freie
        /// Ausfuehrung. Die Ausfuehrung ist strikt an diesen Tenant gebunden. Der Kern wertet den Wert
        /// nicht aus - er wird von der Persistenzschicht gesetzt/gefiltert.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>Der aktuelle Status der Instanz.</summary>
        public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;

        /// <summary>
        /// Bei <see cref="WorkflowStatus.Faulted"/>: der <b>Fehler-Code</b> - ein kurzer, stabiler
        /// Schluessel der FehlerART, oder null. Er ist es, den ein aufrufender Prozess auswertet.
        /// </summary>
        /// <remarks>
        /// Neben <see cref="FaultMessage"/> und nicht statt ihrer: die Meldung ist fuer Menschen, der Code
        /// fuer den Ablauf. Ein Aufrufer, der nach der Meldung verzweigt, muesste sie parsen - und haenge
        /// damit an einer Formulierung, die jederzeit jemand umschreibt oder uebersetzt.
        /// </remarks>
        public string FaultCode { get; set; }

        /// <summary>
        /// Ist diese Instanz <b>angehalten</b>? Dann wird sie von keinem Runner mehr vorangetrieben - sie
        /// bleibt stehen, wo sie steht, bis jemand sie fortsetzt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Bewusst ein EIGENES Feld und kein weiterer <see cref="WorkflowStatus"/>: der Status beschreibt
        /// den Lebenszyklus (laeuft, wartet, fertig, gescheitert, abgebrochen), und er wird an vielen
        /// Stellen des Vortriebs auf <see cref="WorkflowStatus.Running"/> zurueckgesetzt - ein
        /// Status-Wert waere dort stillschweigend wieder aufgehoben. Das Anhalten ist eine Aussage
        /// DARUEBER, nicht ein Teil davon.
        /// </para>
        /// <para>
        /// <b>Was weiterhin geschieht:</b> Nachrichten und Signale kommen an, Tokens werden dadurch
        /// aktiv, Fristen bleiben gesetzt. Nur ausgefuehrt wird nichts. Andernfalls gingen genau die
        /// Ereignisse verloren, die waehrend der Pause eintreffen - und das ist der Zeitraum, in dem man
        /// sie am wenigsten verlieren will.
        /// </para>
        /// </remarks>
        public bool Suspended { get; set; }

        /// <summary>
        /// Warum diese Instanz angehalten wurde, oder null. Steht im Klartext daneben, weil ein
        /// angehaltener Vorgang sonst wie ein haengender aussieht.
        /// </summary>
        public string SuspendedReason { get; set; }

        /// <summary>
        /// Die Dringlichkeit dieser Instanz in der Hintergrund-Abarbeitung - <b>kleinere Zahl =
        /// wichtiger</b> (siehe <see cref="WorkflowPriority"/>). Die Ausfuehrungsschicht reicht den Wert
        /// unveraendert an ihren Task-Processor durch; dessen gewichtete Auswahl laesst hoeher
        /// priorisierte Instanzen niedrigere <b>ueberholen</b>, ohne sie verhungern zu lassen.
        /// </summary>
        /// <remarks>
        /// Der Kern selbst wertet den Wert nicht aus - er entscheidet nichts am Ablauf, nur an der
        /// Reihenfolge. Fuer die reihum-freie, sequenzielle Ausfuehrung
        /// (<c>WorkflowEngine.StartWorkflow</c>/<c>Advance</c>) ist er folgenlos.
        /// </remarks>
        public int Priority { get; set; } = WorkflowPriority.Normal;

        /// <summary>
        /// Der Variablen-Scope der Instanz. Aktivitaeten schreiben hier ihre Ergebnisse hin;
        /// CScript-Bedingungen und -Ausdruecke werten gegen diese Werte aus.
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

        /// <summary>Die aktiven, wartenden und verbrauchten Tokens dieser Instanz.</summary>
        public List<Token> Tokens { get; set; } = new List<Token>();

        /// <summary>
        /// Die noch nicht zugestellten Nachrichten dieser Instanz - vorgemerkt im selben Commit wie der
        /// Zweig, der sie ausgeloest hat (siehe <see cref="OutgoingMessage"/>).
        /// </summary>
        public List<OutgoingMessage> OutgoingMessages { get; set; } = new List<OutgoingMessage>();

        /// <summary>Das Ausfuehrungsprotokoll (fuer Monitoring und den Modeler).</summary>
        public List<HistoryEntry> History { get; set; } = new List<HistoryEntry>();

        /// <summary>
        /// Die erledigten Schritte, die sich <b>zurueecknehmen</b> lassen - in der Reihenfolge ihrer
        /// Vollendung. Wird beim Rueckabwickeln von hinten abgearbeitet.
        /// </summary>
        /// <remarks>
        /// Wie das Protokoll append-only: der Zweig-Commit haengt nur an, was neu dazugekommen ist.
        /// Nebenlaeufige Zweige tragen so unabhaengig voneinander ein, ohne einander zu ueberschreiben.
        /// </remarks>
        public List<CompensationEntry> Compensations { get; set; } = new List<CompensationEntry>();

        /// <summary>
        /// Optionaler fachlicher Korrelationsschluessel, ueber den ein Signal diese Instanz findet
        /// (alternativ zur Instanz-Id).
        /// </summary>
        public string CorrelationKey { get; set; }

        /// <summary>Bei <see cref="WorkflowStatus.Faulted"/>: die Fehlermeldung; sonst null.</summary>
        public string FaultMessage { get; set; }

        /// <summary>
        /// Bei einem Subworkflow: die Id der aufrufenden (Eltern-)Instanz; sonst null. Ueber diesen
        /// Rueck-Link liefert der Subworkflow bei seinem Ende sein Ergebnis an den wartenden Eltern-Zweig.
        /// </summary>
        public string ParentInstanceId { get; set; }

        /// <summary>
        /// Bei einem Subworkflow: die Id des wartenden Eltern-Tokens (des aufrufenden
        /// <see cref="Model.CallWorkflowNode"/>); sonst null.
        /// </summary>
        public string ParentTokenId { get; set; }

        /// <summary>
        /// Die Id der obersten Instanz des Prozessbaums. Fuer eine Top-Level-Instanz null (dann gilt die
        /// eigene <see cref="Id"/>); ein Subworkflow erbt die Root seines Elternprozesses. Ermoeglicht die
        /// aggregierte Protokoll-/Monitoring-Ansicht ueber Subworkflows hinweg.
        /// </summary>
        public string RootInstanceId { get; set; }

        /// <summary>
        /// Verschachtelungstiefe im Prozessbaum (0 = Top-Level). Ein Subworkflow hat die Tiefe seines
        /// Elternprozesses + 1. Dient dem Schutz vor unbegrenzter Selbst-/Wechselrekursion.
        /// </summary>
        public int CallDepth { get; set; }

        /// <summary>Die effektive Root des Prozessbaums (<see cref="RootInstanceId"/>, ersatzweise die eigene Id).</summary>
        [JsonIgnore]
        public string EffectiveRootInstanceId => RootInstanceId ?? Id;

        /// <summary>
        /// Optimistischer Nebenlaeufigkeits-Zaehler. Beim Laden aus dem Store gesetzt; ein
        /// nebenlaeufiger Commit (<c>IWorkflowStore.TryCommitInstance</c>) gelingt nur, wenn dieser Wert
        /// noch dem Stand in der Datenbank entspricht - so serialisieren sich gleichzeitige Zweig-Merges
        /// derselben Instanz, ohne einander zu ueberschreiben. Der Kern wertet den Wert nicht aus; er
        /// wird von der Persistenzschicht gefuehrt.
        /// </summary>
        public int Version { get; set; }

        /// <summary>Erstellzeitpunkt (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Zeitpunkt der letzten Aenderung (UTC).</summary>
        public DateTime UpdatedUtc { get; set; }

        /// <summary>Die aktuell aktiven Tokens (stehen auf einem Knoten, bereit zur Verarbeitung).</summary>
        [JsonIgnore]
        public IEnumerable<Token> ActiveTokens => Tokens.Where(t => t.Status == TokenStatus.Active);

        /// <summary>Die aktuell wartenden Tokens (Signal oder Timer).</summary>
        [JsonIgnore]
        public IEnumerable<Token> WaitingTokens => Tokens.Where(t => t.Status == TokenStatus.Waiting);

        /// <summary>
        /// Entscheidet, welche Eintraege <see cref="Log"/> ueberhaupt anhaengt. Null = der prozessweite
        /// <see cref="WorkflowHistoryFilter.Default"/>. Die Engine setzt hier ihren (ggf. von der
        /// Definition ueberschriebenen) Filter, sobald sie eine Instanz in die Hand nimmt.
        /// </summary>
        [JsonIgnore]
        public IWorkflowHistoryFilter HistoryFilter { get; set; }

        /// <summary>
        /// Haengt einen Protokolleintrag an (Zeitstempel wird gesetzt) - sofern der
        /// <see cref="HistoryFilter"/> ihn durchlaesst. Ein herausgefilterter Eintrag entsteht gar nicht
        /// erst und wird damit auch nicht persistiert.
        /// </summary>
        public void Log(string @event, string nodeId = null, string detail = null,
            HistorySeverity severity = HistorySeverity.Info)
        {
            IWorkflowHistoryFilter filter = HistoryFilter ?? WorkflowHistoryFilter.Default;
            if (filter != null && !filter.ShouldLog(@event, nodeId, severity))
            {
                return;
            }

            var entry = new HistoryEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Event = @event,
                NodeId = nodeId,
                Detail = detail,
                Severity = severity
            };

            // Der Vortrieb eines Zweigs ist einthreadig - mit einer Ausnahme: eine Aktivitaet mit
            // paralleler Iteration laeuft je Element auf einem eigenen Thread und kann von dort
            // protokollieren (ueber den Kontext ist die Instanz erreichbar). Ein List.Add aus mehreren
            // Threads verliert Eintraege oder wirft; das kostet hier ein unumstrittenes Lock.
            lock (History)
            {
                History.Add(entry);
            }
        }
    }
}
