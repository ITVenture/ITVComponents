using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Scheduling;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Serialization;

namespace ITVComponents.Workflow.Stores
{
    /// <summary>Woraufhin ein Ausloeser feuert.</summary>
    public enum WorkflowStartTriggerKind
    {
        /// <summary>Eine eintreffende Nachricht dieses Namens.</summary>
        Message,

        /// <summary>Ein Zeitplan.</summary>
        Schedule
    }

    /// <summary>
    /// Ein <b>Ausloeser</b>: die materialisierte Form dessen, was ein <see cref="StartNode"/> ueber seinen
    /// Einstieg deklariert hat. Er beantwortet die Frage, die nur eine Abfrage beantworten kann - "welche
    /// Definition horcht auf diesen Namen?" bzw. "welcher Zeitplan ist faellig?" - ohne dass dafuer jede
    /// Definition geladen und ihr JSON ausgepackt werden muesste.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dasselbe Muster wie der Aufgaben-Stempel am Token: die Wahrheit steht im Modell, die abfragbare
    /// Form daneben. Sie wird beim Speichern der Definition neu aufgebaut und ist deshalb nie zu pflegen -
    /// wer den Ausloeser aendern will, aendert den Knoten.
    /// </para>
    /// <para>
    /// Der Ausloeser traegt nur die <b>Deklaration</b>. Wer ihn tatsaechlich fahren laesst und wie weit er
    /// dabei ist, steht in seinen <see cref="WorkflowStartTriggerActivation"/>en - eine je Mandant. Das
    /// ist der Schnitt, den die frueheren Fassungen nicht machten: sobald zwei Mandanten denselben
    /// zentralen Zeitplan fahren, hat jeder seinen eigenen Stand, und eine gemeinsame Zeile kann ihn
    /// nicht mehr tragen.
    /// </para>
    /// <para>
    /// <b>Achtung beim Verweisen:</b> der <see cref="TriggerKey"/> wird bei jedem Speichern der Definition
    /// neu vergeben (die Zeilen werden weggeraeumt und neu aufgebaut). Alles, was einen Ausloeser
    /// ueberdauern soll, verweist deshalb ueber seine fachliche Identitaet
    /// (<see cref="TenantId"/> + <see cref="DefinitionId"/> + <see cref="NodeId"/> + <see cref="Kind"/>)
    /// und nicht ueber diese Zahl.
    /// </para>
    /// </remarks>
    public class WorkflowStartTrigger
    {
        /// <summary>
        /// Der technische Schluessel dieser Zeile (von der Persistenz vergeben). <b>Nicht stabil</b> -
        /// siehe die Anmerkung an der Klasse.
        /// </summary>
        public int TriggerKey { get; set; }

        /// <summary>Die Definition, die gestartet wird - ihr technischer Schluessel.</summary>
        public int DefinitionKey { get; set; }

        /// <summary>Die fachliche Id der Definition. Teil der stabilen Identitaet des Ausloesers.</summary>
        public string DefinitionId { get; set; }

        /// <summary>Die Version der Definition, aus der dieser Ausloeser stammt.</summary>
        public int DefinitionVersion { get; set; }

        /// <summary>
        /// Der Mandant der <b>Definition</b> (null = oeffentlich) - nicht zu verwechseln mit dem
        /// Mandanten, der ihn fahren laesst (<see cref="WorkflowStartTriggerActivation.TenantId"/>).
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Ob die Definition <b>oeffentlich</b> ist.
        /// </summary>
        /// <remarks>
        /// Mitgefuehrt und nicht aus <see cref="TenantId"/> geschlossen, aus genau dem Grund, aus dem es
        /// <see cref="WorkflowDefinition.IsPublic"/> ueberhaupt gibt: "kein Mandant" heisst in einem
        /// Ein-Mandanten-Host schlicht "der eine Mandant" und in einem Mehr-Mandanten-Host "gehoert
        /// allen". Wer die beiden verwechselt, legt im Ein-Mandanten-Betrieb keine Aktivierung an - und
        /// dort laeuft dann gar nichts mehr.
        /// </remarks>
        public bool IsPublic { get; set; }

        /// <summary>Der Start-Knoten, an dem der Ausloeser deklariert ist.</summary>
        public string NodeId { get; set; }

        /// <summary>Woraufhin er feuert.</summary>
        public WorkflowStartTriggerKind Kind { get; set; }

        /// <summary>
        /// Das Feature, das der fahrende Mandant aktiviert haben muss - denormalisiert aus
        /// <see cref="WorkflowDefinition.RequiredFeature"/>.
        /// </summary>
        /// <remarks>
        /// Denormalisiert aus demselben Grund, aus dem es diese Tabelle ueberhaupt gibt: die Bedingung
        /// wird bei JEDEM Feuern geprueft und bei jedem Auflisten der uebernehmbaren Ablaeufe - dafuer
        /// jedes Mal das Definitions-JSON auszupacken, waere eine Last, die mit der Zahl der Prozesse
        /// waechst. Die Zeile wird beim Speichern der Definition ohnehin neu aufgebaut und kann deshalb
        /// nicht veralten.
        /// </remarks>
        public string RequiredFeature { get; set; }

        /// <summary>
        /// Die Berechtigung, die ein Benutzer zum Uebernehmen und Starten braucht - denormalisiert aus
        /// <see cref="WorkflowDefinition.RequiredPermission"/>.
        /// </summary>
        public string RequiredPermission { get; set; }

        /// <summary>Ob ein Mandant diesen Einstieg fuer sich uebernehmen darf.</summary>
        public bool AllowLocalActivation { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Message"/>: der Name der Nachricht.</summary>
        public string SignalName { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Message"/>: der Umgang mit laufenden Instanzen.</summary>
        public MessageStartMode Mode { get; set; }

        /// <summary>
        /// Bei <see cref="WorkflowStartTriggerKind.Message"/>: ob der Schluessel der Nachricht der
        /// Korrelationsschluessel der neuen Instanz wird.
        /// </summary>
        public bool AdoptCorrelationKey { get; set; }

        /// <summary>
        /// Bei <see cref="WorkflowStartTriggerKind.Message"/>: ob auch eine Nachricht <b>ohne</b>
        /// Ursprungs-Mandanten diesen Einstieg ausloest.
        /// </summary>
        public bool AllowTenantlessStart { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: das zentrale Zeitplan-Muster.</summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: ueberspringen, solange der vorige Lauf noch
        /// laeuft.
        /// </summary>
        public bool SkipWhilePreviousRuns { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: darf der Mandant ein eigenes Muster setzen?</summary>
        public bool AllowReschedule { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: darf der Mandant eigene Startwerte setzen?</summary>
        public bool AllowOwnVariables { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: die zentralen Startwerte als JSON, oder null.</summary>
        public string VariablesJson { get; set; }
    }

    /// <summary>
    /// Die <b>Aktivierung</b> eines Ausloesers durch einen Mandanten: die Zustimmung ("den will ich auch")
    /// und der Lauf-Zustand, der dazugehoert.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sie verweist ueber die <b>fachliche</b> Identitaet des Ausloesers und nicht ueber dessen
    /// Zeilennummer - siehe <see cref="WorkflowStartTrigger.TriggerKey"/>. Waere es anders, haenge jede
    /// Aktivierung nach der ersten Korrektur an der zentralen Definition im Leeren, und zwar lautlos: die
    /// Verknuepfung faende nichts, und der Zeitplan liefe einfach nicht mehr.
    /// </para>
    /// <para>
    /// <see cref="OwnerTenantId"/> gehoert zwingend dazu. Eine oeffentliche und eine mandanteneigene
    /// Definition duerfen dieselbe fachliche Id tragen (genau dafuer gibt es
    /// <see cref="WorkflowDefinition.Key"/>) - ohne die Spalte aktivierte man frueher oder spaeter die
    /// falsche.
    /// </para>
    /// <para>
    /// Wird ein Knoten umbenannt oder entfernt, bleibt die Aktivierung als <b>Waise</b> stehen: sie
    /// findet keinen Ausloeser mehr und wird nie aufgegriffen. Sie wird nicht geloescht, damit die
    /// Zustimmung erhalten bleibt, wenn der Knoten zurueckkommt - und damit man sieht, dass da mal etwas
    /// war.
    /// </para>
    /// </remarks>
    public class WorkflowStartTriggerActivation
    {
        /// <summary>Der technische Schluessel dieser Zeile (von der Persistenz vergeben).</summary>
        public int ActivationKey { get; set; }

        /// <summary>Der Mandant der DEFINITION, null = oeffentlich. Teil der Identitaet des Ausloesers.</summary>
        public string OwnerTenantId { get; set; }

        /// <summary>Die fachliche Id der Definition. Teil der Identitaet des Ausloesers.</summary>
        public string DefinitionId { get; set; }

        /// <summary>Der Start-Knoten. Teil der Identitaet des Ausloesers.</summary>
        public string NodeId { get; set; }

        /// <summary>Die Art des Ausloesers. Teil der Identitaet des Ausloesers.</summary>
        public WorkflowStartTriggerKind Kind { get; set; }

        /// <summary>Der Mandant, der den Ausloeser fuer sich fahren laesst.</summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Ob die Aktivierung gilt. Abhaken setzt sie auf false und loescht die Zeile <b>nicht</b>: sonst
        /// ginge die Historie verloren, und ein Muster mit "sofort"-Kennzeichen liefe beim erneuten
        /// Anhaken ein zweites Mal sofort an.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Das eigene Muster dieses Mandanten, oder null. Nur wirksam, wenn
        /// <see cref="WorkflowStartTrigger.AllowReschedule"/> es erlaubt.
        /// </summary>
        public string PatternOverride { get; set; }

        /// <summary>
        /// Die eigenen Startwerte dieses Mandanten als JSON, oder null. Nur wirksam, wenn
        /// <see cref="WorkflowStartTrigger.AllowOwnVariables"/> es erlaubt.
        /// </summary>
        public string VariablesJsonOverride { get; set; }

        /// <summary>Die naechste Faelligkeit (UTC), oder null. Bei Nachrichten-Aktivierungen immer null.</summary>
        public DateTime? NextDueUtc { get; set; }

        /// <summary>
        /// Wann zuletzt gestartet wurde (UTC). Null = noch nie - und genau daran haengt das
        /// "sofort"-Kennzeichen eines Musters.
        /// </summary>
        public DateTime? LastRunUtc { get; set; }

        /// <summary>Die zuletzt gestartete Instanz - die Grundlage fuer <c>SkipWhilePreviousRuns</c>.</summary>
        public string LastInstanceId { get; set; }

        /// <summary>Wer den Anspruch gerade haelt, oder null.</summary>
        public string ClaimedBy { get; set; }

        /// <summary>Bis wann der Anspruch gilt, oder null.</summary>
        public DateTime? ClaimedUntil { get; set; }

        /// <summary>Wer aktiviert hat - ein aktivierter Zahlungslauf tut etwas, das will nachvollziehbar sein.</summary>
        public string ActivatedBy { get; set; }

        /// <summary>Wann aktiviert wurde (UTC).</summary>
        public DateTime ActivatedUtc { get; set; }
    }

    /// <summary>
    /// Ein Ausloeser <b>zusammen mit</b> der Aktivierung, fuer die er gerade gilt - was der Aufgriff und
    /// die Zustellung liefern. Erst beides zusammen ergibt einen Start: der Ausloeser sagt WAS, die
    /// Aktivierung sagt FUER WEN und WIE WEIT.
    /// </summary>
    public class WorkflowStartTriggerMatch
    {
        /// <summary>Die Deklaration.</summary>
        public WorkflowStartTrigger Trigger { get; set; }

        /// <summary>Die Zustimmung samt Lauf-Zustand.</summary>
        public WorkflowStartTriggerActivation Activation { get; set; }

        /// <summary>Der Mandant, in dem gestartet wird.</summary>
        public string TenantId => Activation?.TenantId;

        /// <summary>
        /// Ob eine eigene Einstellung des Mandanten vorliegt, die er (nicht mehr) setzen darf. Der Fall,
        /// der protokolliert gehoert: von aussen sieht es aus, als haette sich der Termin grundlos
        /// verschoben.
        /// </summary>
        public bool HasIgnoredOverride =>
            (!string.IsNullOrWhiteSpace(Activation?.PatternOverride) && Trigger?.AllowReschedule != true)
            || (!string.IsNullOrWhiteSpace(Activation?.VariablesJsonOverride) && Trigger?.AllowOwnVariables != true);

        /// <summary>
        /// Das <b>wirksame</b> Muster: das eigene des Mandanten, wenn er eins setzen darf und gesetzt hat -
        /// sonst das zentrale.
        /// </summary>
        public string EffectivePattern =>
            Trigger?.AllowReschedule == true && !string.IsNullOrWhiteSpace(Activation?.PatternOverride)
                ? Activation.PatternOverride
                : Trigger?.Pattern;

        /// <summary>Die <b>wirksamen</b> Startwerte als JSON - nach derselben Regel.</summary>
        public string EffectiveVariablesJson =>
            Trigger?.AllowOwnVariables == true && !string.IsNullOrWhiteSpace(Activation?.VariablesJsonOverride)
                ? Activation.VariablesJsonOverride
                : Trigger?.VariablesJson;
    }

    /// <summary>
    /// Das Ergebnis der Suche nach Nachrichten-Ausloesern: was anspringt - und was auf den Namen horcht,
    /// aber wegen des Ursprungs-Mandanten NICHT anspringt.
    /// </summary>
    /// <remarks>
    /// Der zweite Teil ist kein Beiwerk. Ohne ihn waere "die Nachricht kam aus dem falschen Mandanten"
    /// von "auf den Namen horcht niemand" nicht zu unterscheiden - und genau dieser Unterschied ist es,
    /// den man bei der Fehlersuche braucht.
    /// </remarks>
    public class WorkflowMessageTriggerLookup
    {
        /// <summary>Was tatsaechlich anspringt; nie null.</summary>
        public IReadOnlyList<WorkflowStartTriggerMatch> Matches { get; set; }
            = new List<WorkflowStartTriggerMatch>();

        /// <summary>
        /// Beschreibungen der Ausloeser, die auf den Namen horchen, aber fuer diesen Ursprung nicht
        /// gelten ("'zahllauf' (Knoten 'start', Mandant 'acme')"); nie null.
        /// </summary>
        public IReadOnlyList<string> SuppressedByTenant { get; set; } = new List<string>();
    }

    /// <summary>
    /// Leitet aus einer Definition ihre Ausloeser ab - der EINE Ort, an dem das geschieht, damit die
    /// Stores nicht jeder ihre eigene Auslegung des Modells bekommen.
    /// </summary>
    public static class WorkflowStartTriggerFactory
    {
        /// <summary>
        /// Die Ausloeser dieser Definition. Leer, wenn sie keine deklariert - oder wenn sie gar nicht
        /// ausloesen DARF.
        /// </summary>
        /// <param name="definition">die Definition</param>
        /// <returns>die abgeleiteten Ausloeser; nie null</returns>
        /// <remarks>
        /// <para>
        /// Eine Definition, die die Validierung als fehlerhaft markiert hat
        /// (<see cref="WorkflowDefinition.DisabledForStart"/>), liefert bewusst nichts: sie wuerde beim
        /// Start ohnehin abgelehnt, und ein Zeitplan, der im Minutentakt am Start scheitert, ist
        /// schlimmer als keiner.
        /// </para>
        /// <para>
        /// <b>Oeffentliche Definitionen liefern jetzt sehr wohl Ausloeser</b> - anders als frueher. Sie
        /// feuern deswegen nicht von selbst: ohne Aktivierung gibt es keinen Mandanten, fuer den
        /// gestartet wuerde. Der Ausloeser ist die Deklaration, nicht die Zusage.
        /// </para>
        /// <para>
        /// Die Faelligkeit steht hier nicht mehr - sie gehoert zur Aktivierung. Wer eine anlegt, holt sie
        /// ueber <see cref="FirstDueUtc"/>.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<WorkflowStartTrigger> FromDefinition(WorkflowDefinition definition)
        {
            var result = new List<WorkflowStartTrigger>();
            if (definition == null || definition.DisabledForStart)
            {
                return result;
            }

            foreach (StartNode start in definition.Nodes.OfType<StartNode>())
            {
                if (start.MessageStart != null && !string.IsNullOrWhiteSpace(start.MessageStart.SignalName))
                {
                    WorkflowStartTrigger message = Common(definition, start,
                        WorkflowStartTriggerKind.Message);
                    message.SignalName = start.MessageStart.SignalName.Trim();
                    message.Mode = start.MessageStart.Mode;
                    message.AdoptCorrelationKey = start.MessageStart.AdoptCorrelationKey;
                    message.AllowLocalActivation = start.MessageStart.AllowLocalActivation;
                    message.AllowTenantlessStart = start.MessageStart.AllowTenantlessStart;
                    result.Add(message);
                }

                if (start.ScheduleStart == null || string.IsNullOrWhiteSpace(start.ScheduleStart.Pattern))
                {
                    continue;
                }

                WorkflowStartTrigger schedule = Common(definition, start, WorkflowStartTriggerKind.Schedule);
                schedule.Pattern = start.ScheduleStart.Pattern.Trim();
                schedule.SkipWhilePreviousRuns = start.ScheduleStart.SkipWhilePreviousRuns;
                schedule.AllowLocalActivation = start.ScheduleStart.AllowLocalActivation;
                schedule.AllowReschedule = start.ScheduleStart.AllowReschedule;
                schedule.AllowOwnVariables = start.ScheduleStart.AllowOwnVariables;
                schedule.VariablesJson = start.ScheduleStart.Variables == null
                                         || start.ScheduleStart.Variables.Count == 0
                    ? null
                    : WorkflowJson.SerializeVariables(start.ScheduleStart.Variables);
                result.Add(schedule);
            }

            return result;
        }

        /// <summary>
        /// Die <b>erste</b> Faelligkeit eines Musters - gerechnet als "noch nie gelaufen", denn genau das
        /// ist die Bedingung, unter der ein Muster mit "sofort"-Kennzeichen sofort meint.
        /// </summary>
        /// <param name="pattern">das Zeitplan-Muster</param>
        /// <param name="nowUtc">der aktuelle Zeitpunkt</param>
        /// <param name="diagnosticContext">wofuer gerechnet wird - steht in der Fehlermeldung</param>
        /// <returns>die Faelligkeit, oder null bei unlesbarem Muster</returns>
        /// <remarks>
        /// Ein unlesbares Muster liefert null statt zu werfen: die Aktivierung entsteht dann trotzdem und
        /// bleibt sichtbar (und im Editor korrigierbar), feuert aber nicht. Verschwaende sie still,
        /// suchte man den Fehler beim Runner.
        /// </remarks>
        public static DateTime? FirstDueUtc(string pattern, DateTime nowUtc, string diagnosticContext)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return null;
            }

            try
            {
                return ScheduleEvaluator.NextDueUtc(pattern, null, nowUtc);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Der Zeitplan '{pattern}' ({diagnosticContext}) ist nicht lesbar - die Aktivierung "
                    + $"wird angelegt, feuert aber nicht: {ex.OutlineException()}", LogSeverity.Error);
                return null;
            }
        }

        /// <summary>Was beide Arten gemeinsam haben.</summary>
        private static WorkflowStartTrigger Common(WorkflowDefinition definition, StartNode start,
            WorkflowStartTriggerKind kind)
        {
            return new WorkflowStartTrigger
            {
                DefinitionKey = definition.Key,
                DefinitionId = definition.Id,
                DefinitionVersion = definition.Version,
                TenantId = definition.TenantId,
                IsPublic = definition.IsPublic,
                NodeId = start.Id,
                Kind = kind,
                RequiredFeature = string.IsNullOrWhiteSpace(definition.RequiredFeature)
                    ? null
                    : definition.RequiredFeature.Trim(),
                RequiredPermission = string.IsNullOrWhiteSpace(definition.RequiredPermission)
                    ? null
                    : definition.RequiredPermission.Trim()
            };
        }
    }
}
