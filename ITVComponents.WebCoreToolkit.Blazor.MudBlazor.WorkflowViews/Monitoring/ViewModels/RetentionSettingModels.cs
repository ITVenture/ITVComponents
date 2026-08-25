using System;
using ITVComponents.Workflow.Retention;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels
{
    /// <summary>
    /// Eine geltende Frist, wie die Oberflaeche sie zeigt: der Wert, <b>woher er stammt</b>, und ob der
    /// Wunsch des Mandanten dabei begrenzt wurde.
    /// </summary>
    /// <remarks>
    /// Die Herkunft ist kein Beiwerk. „90 Tage" beantwortet nicht die Frage, die ein Mandant hier hat -
    /// die lautet „warum 90, und kann ich das aendern?". Erst Herkunft und Rahmen machen die Zahl
    /// nachvollziehbar.
    /// </remarks>
    public sealed class RetentionValueItem
    {
        /// <summary>Die geltende Frist in Tagen, oder null = es wird nicht aufgeraeumt.</summary>
        public int? Days { get; init; }

        /// <summary>Woher sie stammt.</summary>
        public RetentionSource Source { get; init; }

        /// <summary>Ob der Wunsch des Mandanten vom Rahmen begrenzt wurde.</summary>
        public bool WasLimited { get; init; }

        /// <summary>Was der Mandant wollte, falls begrenzt wurde - sonst null.</summary>
        public int? RequestedDays { get; init; }

        /// <summary>Baut die Anzeige-Form aus dem Ergebnis der Regel.</summary>
        public static RetentionValueItem From(EffectiveRetention effective)
            => new RetentionValueItem
            {
                Days = effective.Days,
                Source = effective.Source,
                WasLimited = effective.WasLimited,
                RequestedDays = effective.RequestedDays
            };
    }

    /// <summary>
    /// Eine Zeile der Aufbewahrungs-Uebersicht: was fuer die Vorgaenge dieses Mandanten aus EINER
    /// Definition gilt, und was er selbst daran stellen darf.
    /// </summary>
    public sealed class RetentionSettingItem
    {
        /// <summary>Die fachliche Id der Definition.</summary>
        public string DefinitionId { get; init; } = "";

        /// <summary>Der Anzeigename, oder null.</summary>
        public string? Name { get; init; }

        /// <summary>
        /// Ob es die oeffentliche Definition ist. <b>Teil der Identitaet der Zeile</b>: ein Mandant kann
        /// eine eigene Definition gleichen Namens neben der oeffentlichen haben, und die beiden setzen
        /// verschiedene Rahmen.
        /// </summary>
        public bool IsPublic { get; init; }

        /// <summary>Ob die Definition einen Widerspruch dieses Mandanten ueberhaupt zulaesst.</summary>
        public bool MayObject { get; init; }

        /// <summary>Die geltende Frist bis zum Archivieren.</summary>
        public RetentionValueItem Archive { get; init; } = new RetentionValueItem();

        /// <summary>Die geltende Frist fuer die Anhang-Inhalte.</summary>
        public RetentionValueItem Attachments { get; init; } = new RetentionValueItem();

        /// <summary>Der eigene Wunsch zur Archiv-Frist, oder null.</summary>
        public int? MyRetentionDays { get; init; }

        /// <summary>Der eigene Wunsch zur Anhang-Frist, oder null.</summary>
        public int? MyAttachmentRetentionDays { get; init; }

        /// <summary>Untergrenze des Rahmens fuer die Archiv-Frist, oder null.</summary>
        public int? MinRetentionDays { get; init; }

        /// <summary>Obergrenze des Rahmens fuer die Archiv-Frist, oder null.</summary>
        public int? MaxRetentionDays { get; init; }

        /// <summary>Untergrenze des Rahmens fuer die Anhang-Frist, oder null.</summary>
        public int? MinAttachmentRetentionDays { get; init; }

        /// <summary>Obergrenze des Rahmens fuer die Anhang-Frist, oder null.</summary>
        public int? MaxAttachmentRetentionDays { get; init; }

        /// <summary>Wer den Widerspruch zuletzt gesetzt oder zurueckgenommen hat, oder null.</summary>
        public string? SetBy { get; init; }

        /// <summary>Wann (UTC), oder null.</summary>
        public DateTime? SetUtc { get; init; }

        /// <summary>Ob ueberhaupt schon einmal ein Widerspruch eingelegt wurde.</summary>
        public bool HasObjection => MyRetentionDays != null || MyAttachmentRetentionDays != null
                                    || SetUtc != null;
    }

    /// <summary>Der Wunsch eines Mandanten zu den Fristen einer Definition.</summary>
    /// <remarks>
    /// <b>Beide Fristen null ist die Ruecknahme</b>, nicht das Fehlen einer Angabe: dann gilt wieder die
    /// Vorgabe, und die Ablage haelt fest, wer sie wann zurueckgenommen hat.
    /// </remarks>
    public sealed class RetentionObjectionRequest
    {
        /// <summary>Die fachliche Id der Definition.</summary>
        public string DefinitionId { get; set; } = "";

        /// <summary>
        /// Ob die oeffentliche Definition gemeint ist. Der Besitzer wird daraus im Handler bestimmt und
        /// nicht vom Client uebernommen - er gehoert zur Identitaet des Widerspruchs.
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>Die gewuenschte Frist bis zum Archivieren, oder null.</summary>
        public int? RetentionDays { get; set; }

        /// <summary>Die gewuenschte Frist fuer die Anhang-Inhalte, oder null.</summary>
        public int? AttachmentRetentionDays { get; set; }
    }
}
