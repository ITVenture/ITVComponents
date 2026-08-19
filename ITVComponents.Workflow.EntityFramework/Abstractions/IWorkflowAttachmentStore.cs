using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.Workflow.EntityFramework.Abstractions
{
    /// <summary>
    /// Wo der <b>Inhalt</b> eines Vorgangs-Anhangs liegt. Die Beschreibung (Name, Groesse, wer, wann)
    /// bleibt in der Workflow-Datenbank; hier geht es nur um die Bytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bewusst eine eigene, schmale Abstraktion und <b>nicht</b> der <c>IFileHandler</c> des Toolkits: der
    /// ist auf den Upload-<b>Endpunkt</b> zugeschnitten - er nimmt eine Datei entgegen und verarbeitet
    /// sie, gibt aber keine Kennung zurueck, mit der sie sich spaeter wieder lesen liesse. Genau die
    /// braucht ein Anhang.
    /// </para>
    /// <para>
    /// Dasselbe Muster wie beim Hilfesystem (<c>IHelpResourceStore</c>): eingebaut ist eine Ablage in der
    /// Datenbank, und wer seine Dateien woanders haben will (Dateisystem, Objektspeicher), registriert
    /// eine eigene Umsetzung - ohne dass die Oberflaeche oder die Beschreibung davon etwas mitbekommen.
    /// </para>
    /// </remarks>
    public interface IWorkflowAttachmentStore
    {
        /// <summary>
        /// Legt den Inhalt ab und liefert die Kennung, unter der er wiederzufinden ist.
        /// </summary>
        /// <param name="content">die Bytes</param>
        /// <param name="contentType">der Inhaltstyp, oder null</param>
        /// <param name="downloadName">der Dateiname, oder null</param>
        /// <param name="cancellationToken">Abbruch</param>
        Task<string> SaveAsync(byte[] content, string contentType, string downloadName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Oeffnet einen abgelegten Inhalt, oder null, wenn es ihn nicht mehr gibt.
        /// </summary>
        Task<WorkflowAttachmentContent> OpenAsync(string fileIdentifier,
            CancellationToken cancellationToken = default);

        /// <summary>Entfernt einen Inhalt (nach bestem Bemuehen; kein Fehler, wenn er schon weg ist).</summary>
        Task DeleteAsync(string fileIdentifier, CancellationToken cancellationToken = default);
    }

    /// <summary>Ein geoeffneter Anhang: der Datenstrom samt dem, was zur Anzeige noetig ist.</summary>
    public sealed class WorkflowAttachmentContent : IDisposable
    {
        /// <summary>Der Inhalt.</summary>
        public Stream Content { get; init; } = Stream.Null;

        /// <summary>Der Inhaltstyp, oder null.</summary>
        public string ContentType { get; init; }

        /// <summary>Der vorgeschlagene Dateiname, oder null.</summary>
        public string DownloadName { get; init; }

        /// <inheritdoc/>
        public void Dispose() => Content?.Dispose();
    }
}
