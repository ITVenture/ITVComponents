using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Workflow.EntityFramework.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Die <b>eingebaute</b> Ablage fuer Anhaenge: die Bytes liegen in der Workflow-Datenbank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Damit funktionieren Anhaenge ohne jede Einrichtung - das ist ihr Zweck. Fuer grosse Dateien oder
    /// viele Vorgaenge ist eine Datenbank nicht der beste Ort; wer das merkt, registriert eine eigene
    /// <see cref="IWorkflowAttachmentStore"/>-Umsetzung, und weder die Beschreibung noch die Oberflaeche
    /// aendern sich dadurch.
    /// </para>
    /// <para>
    /// Der Kontext wird - wie im uebrigen Store - je Aufruf frisch geholt: eine Ablage, die einen
    /// DbContext ueber ihre Lebensdauer festhielte, waere unter Blazor genau die Quelle der
    /// "second operation"-Fehler.
    /// </para>
    /// </remarks>
    public class EfWorkflowAttachmentStore : IWorkflowAttachmentStore
    {
        private readonly Func<WorkflowContext> contextFactory;

        /// <summary>Erzeugt die Ablage ueber eine Kontext-Quelle.</summary>
        /// <param name="contextFactory">liefert je Aufruf einen frischen Kontext</param>
        public EfWorkflowAttachmentStore(Func<WorkflowContext> contextFactory)
        {
            this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        /// <inheritdoc/>
        public async Task<string> SaveAsync(byte[] content, string contentType, string downloadName,
            CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            string identifier = Guid.NewGuid().ToString("N");
            await using WorkflowContext ctx = contextFactory();
            ctx.WorkflowAttachmentBlobs.Add(new WorkflowAttachmentBlobRow
            {
                FileIdentifier = identifier,
                ContentType = contentType,
                DownloadName = downloadName,
                Content = content
            });
            await ctx.SaveChangesAsync(cancellationToken);
            return identifier;
        }

        /// <inheritdoc/>
        public async Task<WorkflowAttachmentContent> OpenAsync(string fileIdentifier,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileIdentifier))
            {
                return null;
            }

            await using WorkflowContext ctx = contextFactory();
            WorkflowAttachmentBlobRow row = await ctx.WorkflowAttachmentBlobs.AsNoTracking()
                .FirstOrDefaultAsync(b => b.FileIdentifier == fileIdentifier, cancellationToken);
            if (row == null)
            {
                // Kein Fehler - die Beschreibung kann den Inhalt ueberlebt haben (fremde Ablage, manuelles
                // Aufraeumen). Stillschweigen darf es trotzdem nicht: sonst sucht man den fehlenden
                // Download an der Oberflaeche.
                LogEnvironment.LogEvent(
                    $"WorkflowAttachmentStore: zu '{fileIdentifier}' gibt es keinen Inhalt (mehr).",
                    LogSeverity.Report);
                return null;
            }

            return new WorkflowAttachmentContent
            {
                // Der Inhalt wird als GANZES geladen und in einen Speicher-Strom gelegt: der Kontext wird
                // gleich geschlossen, ein an ihn gebundener Strom waere danach tot.
                Content = new MemoryStream(row.Content ?? Array.Empty<byte>(), writable: false),
                ContentType = row.ContentType,
                DownloadName = row.DownloadName
            };
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string fileIdentifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileIdentifier))
            {
                return;
            }

            try
            {
                await using WorkflowContext ctx = contextFactory();
                await ctx.WorkflowAttachmentBlobs
                    .Where(b => b.FileIdentifier == fileIdentifier)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Nach bestem Bemuehen: dass ein Inhalt liegenbleibt, soll das Loeschen des Anhangs nicht
                // aufhalten - der Benutzer sieht ihn danach ohnehin nicht mehr. Ohne Log waere aber nicht
                // nachvollziehbar, warum die Datenbank waechst.
                LogEnvironment.LogEvent(
                    $"WorkflowAttachmentStore: der Inhalt '{fileIdentifier}' konnte nicht geloescht werden "
                    + $"und bleibt liegen: {ex.OutlineException()}", LogSeverity.Error);
            }
        }
    }
}
