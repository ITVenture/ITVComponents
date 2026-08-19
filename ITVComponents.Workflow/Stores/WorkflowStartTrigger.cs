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
    /// Ein Ausloeser gehoert dem <b>Mandanten seiner Definition</b>. Oeffentliche Definitionen bekommen
    /// bewusst keinen: sie gehoeren allen, und "fuer alle einmal starten" waere eine voellig andere
    /// Zusage als die, die hier gemacht wird.
    /// </para>
    /// </remarks>
    public class WorkflowStartTrigger
    {
        /// <summary>Der technische Schluessel dieses Ausloesers (von der Persistenz vergeben).</summary>
        public int TriggerKey { get; set; }

        /// <summary>Die Definition, die gestartet wird - ihr technischer Schluessel.</summary>
        public int DefinitionKey { get; set; }

        /// <summary>Die fachliche Id der Definition (fuer Meldungen und Diagnose).</summary>
        public string DefinitionId { get; set; }

        /// <summary>Die Version der Definition, aus der dieser Ausloeser stammt.</summary>
        public int DefinitionVersion { get; set; }

        /// <summary>Der Mandant der Definition, oder null.</summary>
        public string TenantId { get; set; }

        /// <summary>Der Start-Knoten, an dem der Ausloeser deklariert ist.</summary>
        public string NodeId { get; set; }

        /// <summary>Woraufhin er feuert.</summary>
        public WorkflowStartTriggerKind Kind { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Message"/>: der Name der Nachricht.</summary>
        public string SignalName { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Message"/>: der Umgang mit laufenden Instanzen.</summary>
        public MessageStartMode Mode { get; set; }

        /// <summary>
        /// Bei <see cref="WorkflowStartTriggerKind.Message"/>: ob der Schluessel der Nachricht der
        /// Korrelationsschluessel der neuen Instanz wird.
        /// </summary>
        public bool AdoptCorrelationKey { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: das Zeitplan-Muster.</summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: ueberspringen, solange der vorige Lauf noch
        /// laeuft.
        /// </summary>
        public bool SkipWhilePreviousRuns { get; set; }

        /// <summary>
        /// Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: die naechste Faelligkeit (UTC). Null = dieser
        /// Plan hat keinen weiteren Termin.
        /// </summary>
        public DateTime? NextDueUtc { get; set; }

        /// <summary>
        /// Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: wann zuletzt gestartet wurde (UTC). Null =
        /// noch nie - und genau daran haengt das "sofort"-Kennzeichen eines Musters.
        /// </summary>
        public DateTime? LastRunUtc { get; set; }

        /// <summary>
        /// Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: die zuletzt gestartete Instanz - die Grundlage
        /// fuer <see cref="SkipWhilePreviousRuns"/>.
        /// </summary>
        public string LastInstanceId { get; set; }

        /// <summary>Bei <see cref="WorkflowStartTriggerKind.Schedule"/>: die festen Startwerte als JSON, oder null.</summary>
        public string VariablesJson { get; set; }
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
        /// <param name="nowUtc">der aktuelle Zeitpunkt - Grundlage der ersten Faelligkeit</param>
        /// <returns>die abgeleiteten Ausloeser; nie null</returns>
        /// <remarks>
        /// <para>
        /// Zwei Faelle liefern bewusst nichts: eine <b>oeffentliche</b> Definition (sie gehoert keinem
        /// Mandanten, fuer den gestartet werden koennte) und eine, die die Validierung als fehlerhaft
        /// markiert hat (<see cref="WorkflowDefinition.DisabledForStart"/>) - die wuerde beim Start
        /// ohnehin abgelehnt, und ein Zeitplan, der im Minutentakt am Start scheitert, ist schlimmer als
        /// keiner.
        /// </para>
        /// <para>
        /// Ein unlesbares Zeitplan-Muster erzeugt einen Ausloeser <b>ohne</b> Faelligkeit statt gar
        /// keinen: er bleibt damit sichtbar (und im Editor korrigierbar), feuert aber nicht. Verschwaende
        /// er still, suchte man den Fehler beim Runner.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<WorkflowStartTrigger> FromDefinition(WorkflowDefinition definition,
            DateTime nowUtc)
        {
            var result = new List<WorkflowStartTrigger>();
            if (definition == null || definition.IsPublic || definition.DisabledForStart)
            {
                return result;
            }

            foreach (StartNode start in definition.Nodes.OfType<StartNode>())
            {
                if (start.MessageStart != null && !string.IsNullOrWhiteSpace(start.MessageStart.SignalName))
                {
                    result.Add(new WorkflowStartTrigger
                    {
                        DefinitionKey = definition.Key,
                        DefinitionId = definition.Id,
                        DefinitionVersion = definition.Version,
                        TenantId = definition.TenantId,
                        NodeId = start.Id,
                        Kind = WorkflowStartTriggerKind.Message,
                        SignalName = start.MessageStart.SignalName.Trim(),
                        Mode = start.MessageStart.Mode,
                        AdoptCorrelationKey = start.MessageStart.AdoptCorrelationKey
                    });
                }

                if (start.ScheduleStart == null || string.IsNullOrWhiteSpace(start.ScheduleStart.Pattern))
                {
                    continue;
                }

                var schedule = new WorkflowStartTrigger
                {
                    DefinitionKey = definition.Key,
                    DefinitionId = definition.Id,
                    DefinitionVersion = definition.Version,
                    TenantId = definition.TenantId,
                    NodeId = start.Id,
                    Kind = WorkflowStartTriggerKind.Schedule,
                    Pattern = start.ScheduleStart.Pattern.Trim(),
                    SkipWhilePreviousRuns = start.ScheduleStart.SkipWhilePreviousRuns,
                    VariablesJson = start.ScheduleStart.Variables == null
                                    || start.ScheduleStart.Variables.Count == 0
                        ? null
                        : WorkflowJson.SerializeVariables(start.ScheduleStart.Variables)
                };

                try
                {
                    // Noch nie gelaufen: genau die Bedingung, unter der ein Muster mit "sofort"-Kennzeichen
                    // sofort meint. Ein umgeschriebener Zeitplan faengt damit bewusst neu an - er ist ein
                    // anderer Plan, und sein erster Lauf gehoert ihm.
                    schedule.NextDueUtc = ScheduleEvaluator.NextDueUtc(schedule.Pattern, null, nowUtc);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Der Zeitplan '{schedule.Pattern}' des Start-Knotens '{start.Id}' in Definition "
                        + $"'{definition.Id}' v{definition.Version} ist nicht lesbar - der Ausloeser wird "
                        + $"angelegt, feuert aber nicht: {ex.OutlineException()}", LogSeverity.Error);
                }

                result.Add(schedule);
            }

            return result;
        }
    }
}
