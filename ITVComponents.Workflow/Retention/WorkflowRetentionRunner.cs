using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Logging;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;

namespace ITVComponents.Workflow.Retention
{
    /// <summary>Was ein Aufbewahrungslauf getan hat.</summary>
    public sealed class WorkflowRetentionResult
    {
        /// <summary>Wie viele Gruppen (Definition mal Mandant) er angesehen hat.</summary>
        public int GroupsSeen { get; set; }

        /// <summary>Wie viele davon nichts zu tun hatten.</summary>
        public int GroupsWithoutWork { get; set; }

        /// <summary>Wie viele Prozessbaeume archiviert wurden.</summary>
        public int TreesArchived { get; set; }

        /// <summary>Wie viele Instanzen dabei zusammenkamen (Baeume mit Subworkflows zaehlen mehr).</summary>
        public int InstancesArchived { get; set; }

        /// <summary>
        /// Wie viele faellige Baeume die Ablage <b>nicht</b> archiviert hat - weil ein Subworkflow noch
        /// laeuft oder die Instanz inzwischen weg war. Steht getrennt da, damit „nichts zu tun" und
        /// „nicht gemacht" unterscheidbar bleiben.
        /// </summary>
        public int TreesRefused { get; set; }

        /// <summary>
        /// In wie vielen Gruppen die Obergrenze je Lauf erreicht wurde - dort ist noch mehr faellig.
        /// </summary>
        public int GroupsTruncated { get; set; }
    }

    /// <summary>
    /// Der <b>Aufbewahrungslauf</b>: raeumt beendete Vorgaenge ins Archiv, sobald ihre Frist abgelaufen
    /// ist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Er fragt nicht „was ist aelter als X" - <b>X gibt es nicht</b>. Die Frist ergibt sich aus
    /// Definition und Mandant, ist also je Gruppe eine andere. Deshalb der Umweg: erst die Gruppen (das
    /// sind wenige), je Gruppe die geltende Frist ausrechnen, und nur dort nachsehen, wo sie schon
    /// abgelaufen sein kann.
    /// </para>
    /// <para>
    /// <b>Sagt niemand etwas, wird nicht aufgeraeumt.</b> Eine Gruppe ohne Frist wird uebersprungen -
    /// das ist die sichere Richtung, und sie ist der Grund, warum dieser Lauf ohne Konfiguration
    /// gefahrlos mitlaufen kann.
    /// </para>
    /// <para>
    /// Ohne Uhr und ohne Ablage-Wissen: beides bringt der Aufrufer mit. So laesst sich ein Lauf im Test
    /// bis auf den Tag stellen, statt zu warten.
    /// </para></remarks>
    public sealed class WorkflowRetentionRunner
    {
        private readonly IWorkflowStore store;
        private readonly WorkflowRetentionDefaults defaults;

        /// <summary>Erzeugt einen Lauf ueber die angegebene Ablage.</summary>
        /// <param name="store">die Ablage</param>
        /// <param name="defaults">die globalen Vorgaben, oder null = keine</param>
        public WorkflowRetentionRunner(IWorkflowStore store, WorkflowRetentionDefaults defaults = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.defaults = defaults;
        }

        /// <summary>Faehrt einen Lauf.</summary>
        /// <param name="nowUtc">der Zeitpunkt, gegen den die Fristen gerechnet werden</param>
        /// <param name="maxPerGroup">wie viele Baeume je Gruppe und Lauf hoechstens</param>
        public WorkflowRetentionResult Run(DateTime nowUtc, int maxPerGroup = 200)
        {
            if (maxPerGroup <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPerGroup),
                    "A retention run that may archive nothing is not a run - pass a positive limit.");
            }

            var result = new WorkflowRetentionResult();
            foreach (WorkflowRetentionGroup group in store.ListEndedInstanceGroups())
            {
                result.GroupsSeen++;
                DateTime? cutoff = CutoffFor(group, nowUtc);
                if (cutoff == null || group.OldestEndedUtc >= cutoff.Value)
                {
                    // Entweder gilt keine Frist, oder selbst der aelteste Vorgang der Gruppe ist noch
                    // nicht so weit - dann muss keine einzige Instanz gelesen werden.
                    result.GroupsWithoutWork++;
                    continue;
                }

                IReadOnlyList<string> due = store.FindEndedInstances(group.DefinitionKey, group.TenantId,
                    cutoff.Value, maxPerGroup);
                foreach (string rootInstanceId in due)
                {
                    int archived = store.ArchiveInstanceTree(rootInstanceId, nowUtc);
                    if (archived == 0)
                    {
                        // Die Ablage hat gesagt, warum. Hier zaehlt es nur mit, damit der Lauf nicht
                        // "erledigt" meldet, wo er nichts erledigt hat.
                        result.TreesRefused++;
                        continue;
                    }

                    result.TreesArchived++;
                    result.InstancesArchived += archived;
                }

                if (due.Count == maxPerGroup)
                {
                    // Keine stille Deckelung: wer die Zahlen liest, soll sehen, dass hier noch etwas
                    // liegt - sonst sieht ein abgeschnittener Lauf aus wie ein vollstaendiger.
                    result.GroupsTruncated++;
                    LogEnvironment.LogEvent(
                        $"Retention run: group (definition {group.DefinitionKey}, tenant "
                        + $"'{group.TenantId}') hit the per-run limit of {maxPerGroup}. More is due and "
                        + "will be picked up by the next run.", LogSeverity.Report);
                }
            }

            LogEnvironment.LogEvent(
                $"Retention run finished: {result.InstancesArchived} instances in "
                + $"{result.TreesArchived} process trees archived, {result.TreesRefused} refused, "
                + $"{result.GroupsWithoutWork} of {result.GroupsSeen} groups had nothing to do"
                + (result.GroupsTruncated == 0 ? "." : $", {result.GroupsTruncated} truncated."),
                result.TreesRefused == 0 ? LogSeverity.Report : LogSeverity.Warning);
            return result;
        }

        /// <summary>
        /// Der Stichtag dieser Gruppe, oder null, wenn sie nicht aufgeraeumt wird.
        /// </summary>
        private DateTime? CutoffFor(WorkflowRetentionGroup group, DateTime nowUtc)
        {
            WorkflowDefinition definition = store.GetDefinition(group.DefinitionKey);
            if (definition == null)
            {
                // Die Definition ist weg, die Vorgaenge sind noch da. Ihre Vorgabe ist damit
                // unauffindbar, und ein Widerspruch liesse sich nicht einmal zuordnen (er haengt an
                // Besitzer und fachlicher Id). Es bleibt die globale Vorgabe - und die Meldung, denn
                // sonst wundert sich jemand, warum hier eine andere Frist gilt als eingestellt.
                LogEnvironment.LogEvent(
                    $"Retention run: definition {group.DefinitionKey} no longer exists, but "
                    + $"{group.Count} ended instances of it do (tenant '{group.TenantId}'). Falling back "
                    + "to the global default.", LogSeverity.Warning);
            }

            WorkflowRetentionOverride objection = definition == null
                ? null
                : store.GetRetentionOverridesForDefinition(definition.TenantId, definition.TechnicalName)
                    .FirstOrDefault(o => o.TenantId == group.TenantId);

            EffectiveRetention effective = WorkflowRetentionPolicy.Archive(definition, objection, defaults);
            return effective.DueBefore(nowUtc);
        }
    }
}
