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

        /// <summary>Fachliche Id der zugrunde liegenden Definition.</summary>
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
        /// Der Variablen-Scope der Instanz. Aktivitaeten schreiben hier ihre Ergebnisse hin;
        /// CScript-Bedingungen und -Ausdruecke werten gegen diese Werte aus.
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

        /// <summary>Die aktiven, wartenden und verbrauchten Tokens dieser Instanz.</summary>
        public List<Token> Tokens { get; set; } = new List<Token>();

        /// <summary>Das Ausfuehrungsprotokoll (fuer Monitoring und den Modeler).</summary>
        public List<HistoryEntry> History { get; set; } = new List<HistoryEntry>();

        /// <summary>
        /// Optionaler fachlicher Korrelationsschluessel, ueber den ein Signal diese Instanz findet
        /// (alternativ zur Instanz-Id).
        /// </summary>
        public string CorrelationKey { get; set; }

        /// <summary>Bei <see cref="WorkflowStatus.Faulted"/>: die Fehlermeldung; sonst null.</summary>
        public string FaultMessage { get; set; }

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

        /// <summary>Haengt einen Protokolleintrag an (Zeitstempel wird gesetzt).</summary>
        public void Log(string @event, string nodeId = null, string detail = null,
            HistorySeverity severity = HistorySeverity.Info)
        {
            History.Add(new HistoryEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Event = @event,
                NodeId = nodeId,
                Detail = detail,
                Severity = severity
            });
        }
    }
}
