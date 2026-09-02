using System;
using System.IO;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.ViewModels
{
    /// <summary>
    /// Welcher Ausschnitt der Aufgaben angezeigt wird. Die Zustaendigkeit selbst haengt an der
    /// Permission - dieser Filter waehlt nur, welche der sichtbaren Aufgaben gezeigt werden.
    /// </summary>
    public enum UserTaskScope
    {
        /// <summary>Mir zugewiesen.</summary>
        Mine,

        /// <summary>Niemandem zugewiesen (Pool) - jeder mit der Permission darf sie nehmen.</summary>
        Pool,

        /// <summary>Alles, was ich sehen darf (meine, Pool und die anderer Bearbeiter).</summary>
        All
    }

    /// <summary>Die Listenabfrage der Arbeitsliste.</summary>
    public sealed class UserTaskListQuery
    {
        /// <summary>Nullbasierte Seitennummer.</summary>
        public int Page { get; init; }

        /// <summary>Seitengroesse.</summary>
        public int PageSize { get; init; } = 25;

        /// <summary>Sortierspalte, oder null (Standard: aelteste zuerst).</summary>
        public string? SortColumn { get; init; }

        /// <summary>Absteigend sortieren.</summary>
        public bool SortDescending { get; init; }

        /// <summary>Freitext ueber Titel, Aufgabenart, Instanz und Definition.</summary>
        public string? Search { get; init; }

        /// <summary>Nur eine bestimmte Aufgabenart, oder null fuer alle.</summary>
        public string? TaskKey { get; init; }

        /// <summary>
        /// Nur Aufgaben aus einer bestimmten Definition (deren fachliche Id, siehe
        /// <see cref="UserTaskListItem.DefinitionId"/>), oder null fuer alle.
        /// </summary>
        /// <remarks>
        /// Gefiltert wird ueber die <b>fachliche</b> Id und nicht ueber die Zeilenkennung der Definition:
        /// die Id ist stabil ueber Versionen und Mandanten hinweg und steht damit auch in einem
        /// Navigationslink (<c>/Workflow/Tasks/{workflow}</c>), der eine Neupublikation ueberleben soll.
        /// </remarks>
        public string? DefinitionId { get; init; }

        /// <summary>Welcher Ausschnitt (Standard: meine).</summary>
        public UserTaskScope Scope { get; init; } = UserTaskScope.Mine;

        /// <summary>Nur ueberfaellige Aufgaben.</summary>
        public bool OverdueOnly { get; init; }
    }

    /// <summary>
    /// Ein Kommentar am Vorgang - Rueckfrage, Vermerk, Begruendung.
    /// </summary>
    public sealed class WorkflowComment
    {
        /// <summary>Der technische Schluessel.</summary>
        public int CommentKey { get; init; }

        /// <summary>Der Vorgang.</summary>
        public string InstanceId { get; init; } = string.Empty;

        /// <summary>Die Aufgabe, bei der er entstanden ist, oder null.</summary>
        public string? TokenId { get; init; }

        /// <summary>Wer geschrieben hat.</summary>
        public string? Author { get; init; }

        /// <summary>Wann (UTC).</summary>
        public DateTime CreatedUtc { get; init; }

        /// <summary>Der Text.</summary>
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>Die Beschreibung eines Anhangs am Vorgang - ohne seinen Inhalt.</summary>
    public sealed class WorkflowAttachment
    {
        /// <summary>Der technische Schluessel.</summary>
        public int AttachmentKey { get; init; }

        /// <summary>Der Vorgang.</summary>
        public string InstanceId { get; init; } = string.Empty;

        /// <summary>Der Dateiname.</summary>
        public string FileName { get; init; } = string.Empty;

        /// <summary>Der Inhaltstyp, oder null.</summary>
        public string? ContentType { get; init; }

        /// <summary>Die Groesse in Bytes.</summary>
        public long SizeBytes { get; init; }

        /// <summary>Wer hochgeladen hat.</summary>
        public string? Author { get; init; }

        /// <summary>Wann (UTC).</summary>
        public DateTime CreatedUtc { get; init; }
    }

    /// <summary>
    /// Ein geoeffneter Anhang: der Datenstrom samt dem, was der Browser zum Speichern braucht.
    /// </summary>
    /// <remarks>
    /// Der Empfaenger schliesst den Strom - bis dahin haelt er Speicher bzw. eine Datenbankressource.
    /// </remarks>
    public sealed class WorkflowAttachmentDownload : IDisposable
    {
        /// <summary>Der Inhalt.</summary>
        public Stream Content { get; init; } = Stream.Null;

        /// <summary>Der Dateiname.</summary>
        public string FileName { get; init; } = "download";

        /// <summary>Der Inhaltstyp, oder null.</summary>
        public string? ContentType { get; init; }

        /// <inheritdoc/>
        public void Dispose() => Content?.Dispose();
    }

    /// <summary>Eine Zeile der Arbeitsliste.</summary>
    public sealed class UserTaskListItem
    {
        /// <summary>Die Instanz, zu der die Aufgabe gehoert.</summary>
        public string InstanceId { get; init; } = string.Empty;

        /// <summary>Das wartende Token - die Aufgabe selbst.</summary>
        public string TokenId { get; init; } = string.Empty;

        /// <summary>Die Definition, aus der die Aufgabe stammt.</summary>
        public string DefinitionId { get; init; } = string.Empty;

        /// <summary>Der Knoten der Aufgabe.</summary>
        public string NodeId { get; init; } = string.Empty;

        /// <summary>Die Aufgabenart.</summary>
        public string TaskKey { get; init; } = string.Empty;

        /// <summary>
        /// Der Titel - <b>unaufgeloest</b> (Klartext oder Kultur-JSON). Uebersetzt wird in der Anzeige,
        /// die die Kultur des Lesers kennt.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>Der Zustaendige, oder null fuer eine Pool-Aufgabe.</summary>
        public string? AssignedTo { get; init; }

        /// <summary>
        /// Die Permission, die diese Aufgabe verlangt (null/leer = keine). Sie steht hier, damit die
        /// Auswahl beim Umtragen nur Leute anbieten kann, die die Aufgabe anschliessend auch sehen -
        /// sonst landet sie unsichtbar bei jemandem, und niemand merkt es.
        /// </summary>
        public string? RequiredPermission { get; init; }

        /// <summary>Wann die Aufgabe entstanden ist (UTC).</summary>
        public DateTime? CreatedUtc { get; init; }

        /// <summary>Die Frist (UTC), oder null.</summary>
        public DateTime? DueUtc { get; init; }

        /// <summary>Wer die Aufgabe gerade offen hat (weiche Sperre), oder null.</summary>
        public string? ClaimedBy { get; init; }

        /// <summary>Bis wann die weiche Sperre gilt (UTC), oder null.</summary>
        public DateTime? ClaimedUntil { get; init; }

        /// <summary>Der Korrelationsschluessel der Instanz (fachlicher Bezug), oder null.</summary>
        public string? CorrelationKey { get; init; }
    }
}
