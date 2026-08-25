using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Workflow.EntityFramework.Abstractions;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Retention;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Eine Gruppe von Vorgaengen, deren Anhang-Bytes noch da sind - je Definition und Mandant eine.
    /// </summary>
    public sealed class WorkflowAttachmentGroup
    {
        /// <summary>Die Definitionszeile.</summary>
        public int DefinitionKey { get; set; }

        /// <summary>Der Mandant.</summary>
        public string TenantId { get; set; }

        /// <summary>Der aelteste Endzeitpunkt der Gruppe.</summary>
        public DateTime OldestEndedUtc { get; set; }
    }

    /// <summary>Ein Vorgang, dessen Anhang-Bytes faellig sind.</summary>
    public sealed class WorkflowAttachmentPurgeCandidate
    {
        /// <summary>Der Vorgang.</summary>
        public string InstanceId { get; set; }

        /// <summary>Ob er bereits im Archiv steht (dann haengt die Markierung an der Archiv-Zeile).</summary>
        public bool IsArchived { get; set; }

        /// <summary>Die Kennungen, unter denen die Ablage die Inhalte fuehrt.</summary>
        public List<string> FileIdentifiers { get; set; } = new List<string>();
    }

    /// <summary>Was ein Anhang-Lauf getan hat.</summary>
    public sealed class WorkflowAttachmentRetentionResult
    {
        /// <summary>Wie viele Gruppen er angesehen hat.</summary>
        public int GroupsSeen { get; set; }

        /// <summary>Wie viele davon nichts zu tun hatten.</summary>
        public int GroupsWithoutWork { get; set; }

        /// <summary>Wie viele Vorgaenge ihre Anhang-Inhalte verloren haben.</summary>
        public int ProcessesPurged { get; set; }

        /// <summary>Wie viele Dateien dabei zusammenkamen.</summary>
        public int FilesDeleted { get; set; }

        /// <summary>
        /// Wie viele Vorgaenge <b>nicht</b> fertig wurden, weil die Ablage beim Loeschen gescheitert
        /// ist. Sie bleiben unmarkiert und kommen beim naechsten Lauf wieder dran.
        /// </summary>
        public int ProcessesFailed { get; set; }
    }

    /// <summary>
    /// Der <b>Anhang-Lauf</b>: raeumt die Inhalte der Anhaenge weg, sobald ihre eigene Frist abgelaufen
    /// ist - unabhaengig davon, ob der Vorgang selbst noch aktiv liegt oder schon im Archiv steht.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Zwei Fristen, nicht eine.</b> Der Sinn ist gerade, dass die Dateien frueher wegfallen duerfen
    /// als der Vorgang: den Vorgang zehn Jahre, die PDFs ein Jahr. Deshalb faengt dieser Lauf auch bei
    /// noch aktiven, beendeten Vorgaengen an und nicht erst im Archiv - sonst biesse eine kurze
    /// Anhang-Frist neben einer langen Aufbewahrungsfrist nie.
    /// </para>
    /// <para>
    /// <b>Die Beschreibung bleibt in jedem Fall.</b> Weggeraeumt werden die Bytes; Name, Groesse, wer
    /// und wann bleiben stehen, samt Datei-Kennung. Ein Vorgang, der nicht mehr sagen kann „hier war
    /// eine Datei", waere unvollstaendig festgehalten.
    /// </para>
    /// <para>
    /// Eigene Klasse und nicht Teil von <see cref="WorkflowRetentionRunner"/>: Anhaenge gibt es nur in
    /// der Datenbank-Fassung, ihre Inhalte liegen hinter einer austauschbaren Ablage
    /// (<see cref="IWorkflowAttachmentStore"/>), und die ist asynchron. Der Kern-Lauf soll davon nichts
    /// wissen muessen.
    /// </para></remarks>
    public sealed class WorkflowAttachmentRetentionRunner
    {
        private readonly EfWorkflowStore store;
        private readonly IWorkflowAttachmentStore attachments;
        private readonly WorkflowRetentionDefaults defaults;

        /// <summary>Erzeugt einen Anhang-Lauf.</summary>
        /// <param name="store">die Ablage der Vorgaenge</param>
        /// <param name="attachments">die Ablage der Datei-Inhalte</param>
        /// <param name="defaults">die globalen Vorgaben, oder null = keine</param>
        public WorkflowAttachmentRetentionRunner(EfWorkflowStore store,
            IWorkflowAttachmentStore attachments, WorkflowRetentionDefaults defaults = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            this.defaults = defaults;
        }

        /// <summary>Faehrt einen Lauf.</summary>
        /// <param name="nowUtc">der Zeitpunkt, gegen den die Fristen gerechnet werden</param>
        /// <param name="maxPerGroup">wie viele Vorgaenge je Gruppe und Lauf hoechstens</param>
        /// <param name="cancellationToken">Abbruch</param>
        public async Task<WorkflowAttachmentRetentionResult> RunAsync(DateTime nowUtc,
            int maxPerGroup = 200, CancellationToken cancellationToken = default)
        {
            if (maxPerGroup <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPerGroup),
                    "A run that may delete nothing is not a run - pass a positive limit.");
            }

            var result = new WorkflowAttachmentRetentionResult();
            foreach (WorkflowAttachmentGroup group in store.ListAttachmentGroups())
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.GroupsSeen++;

                WorkflowDefinition definition = store.GetDefinition(group.DefinitionKey);
                WorkflowRetentionOverride objection = definition == null
                    ? null
                    : store.GetRetentionOverridesForDefinition(definition.TenantId, definition.Id)
                        .FirstOrDefault(o => o.TenantId == group.TenantId);
                DateTime? cutoff = WorkflowRetentionPolicy
                    .Attachments(definition, objection, defaults).DueBefore(nowUtc);
                if (cutoff == null || group.OldestEndedUtc >= cutoff.Value)
                {
                    result.GroupsWithoutWork++;
                    continue;
                }

                foreach (WorkflowAttachmentPurgeCandidate candidate in store.FindPurgeableAttachments(
                             group.DefinitionKey, group.TenantId, cutoff.Value, maxPerGroup))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await PurgeAsync(candidate, nowUtc, cancellationToken).ConfigureAwait(false))
                    {
                        result.ProcessesPurged++;
                        result.FilesDeleted += candidate.FileIdentifiers.Count;
                    }
                    else
                    {
                        result.ProcessesFailed++;
                    }
                }
            }

            LogEnvironment.LogEvent(
                $"Attachment retention run finished: {result.FilesDeleted} files of "
                + $"{result.ProcessesPurged} processes deleted, {result.ProcessesFailed} left for the "
                + $"next run, {result.GroupsWithoutWork} of {result.GroupsSeen} groups had nothing to do.",
                result.ProcessesFailed == 0 ? LogSeverity.Report : LogSeverity.Warning);
            return result;
        }

        /// <summary>
        /// Loescht die Inhalte eines Vorgangs und merkt es an. Liefert false, wenn dabei etwas
        /// schiefging - dann bleibt er unmarkiert und kommt wieder dran.
        /// </summary>
        private async Task<bool> PurgeAsync(WorkflowAttachmentPurgeCandidate candidate, DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            try
            {
                foreach (string fileIdentifier in candidate.FileIdentifiers)
                {
                    await attachments.DeleteAsync(fileIdentifier, cancellationToken)
                        .ConfigureAwait(false);
                }

                // ERST danach markieren. Andersherum bliebe bei einem Abbruch eine Datei liegen, die
                // niemand mehr sucht; so wird im schlimmsten Fall ein zweites Mal geloescht, und das
                // darf sein.
                store.MarkAttachmentsPurged(candidate.InstanceId, nowUtc);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Der Lauf soll an einer einzelnen Datei nicht sterben - aber er verschweigt sie auch
                // nicht. Unmarkiert bedeutet: der naechste Lauf versucht es wieder.
                LogEnvironment.LogEvent(
                    $"Attachment retention: could not delete the attachments of process "
                    + $"'{candidate.InstanceId}' ({candidate.FileIdentifiers.Count} files"
                    + $"{(candidate.IsArchived ? ", archived" : string.Empty)}). It stays unmarked and "
                    + $"will be retried: {ex.OutlineException()}", LogSeverity.Error);
                return false;
            }
        }
    }
}
