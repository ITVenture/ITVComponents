using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Retention;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels
{
    /// <summary>
    /// Eine Zeile der Archiv-Uebersicht - aus den <b>Spalten</b> der Archiv-Zeile, ohne die Nutzlast
    /// anzufassen.
    /// </summary>
    /// <remarks>
    /// Die Nutzlast bleibt hier bewusst aussen vor: sie traegt Verlauf, Variablen, Kommentare und
    /// Anhang-Beschreibungen eines ganzen Vorgangs. Eine Liste, die das je Zeile auspackt, laedt fuer
    /// zwanzig Zeilen zwanzig Vorgaenge - und zeigt davon vier Felder.
    /// </remarks>
    public sealed class ArchivedInstanceListItem
    {
        /// <summary>Die unveraenderte Instanz-Id.</summary>
        public string InstanceId { get; init; } = "";

        /// <summary>Die fachliche Id der Definition.</summary>
        public string DefinitionId { get; init; } = "";

        /// <summary>Die Version der Definition.</summary>
        public int DefinitionVersion { get; init; }

        /// <summary>Der Name der Definition, wie er beim Archivieren lautete.</summary>
        public string? DefinitionName { get; init; }

        /// <summary>Der Endstatus.</summary>
        public WorkflowStatus Status { get; init; }

        /// <summary>Wann der Vorgang begonnen hat (UTC).</summary>
        public DateTime CreatedUtc { get; init; }

        /// <summary>Wann er geendet hat (UTC).</summary>
        public DateTime? EndedUtc { get; init; }

        /// <summary>Wann er archiviert wurde (UTC).</summary>
        public DateTime ArchivedUtc { get; init; }

        /// <summary>Der Fehler-Code, oder null.</summary>
        public string? FaultCode { get; init; }

        /// <summary>Ob er zu einem Prozessbaum gehoerte (Eltern-Instanz vorhanden).</summary>
        public bool HasParent { get; init; }

        /// <summary>Wie viele Anhaenge er hatte.</summary>
        public int AttachmentCount { get; init; }

        /// <summary>Ob deren Inhalte inzwischen weggeraeumt sind.</summary>
        public bool AttachmentsPurged { get; init; }
    }

    /// <summary>
    /// Ein archivierter Vorgang in voller Breite - die Spalten plus die ausgepackte Nutzlast.
    /// </summary>
    public sealed class ArchivedInstanceDetail
    {
        /// <summary>Die Zeilen-Daten.</summary>
        public ArchivedInstanceListItem Head { get; init; } = new ArchivedInstanceListItem();

        /// <summary>Die Fehlermeldung, oder null.</summary>
        public string? FaultMessage { get; init; }

        /// <summary>Die oberste Instanz des Prozessbaums.</summary>
        public string? RootInstanceId { get; init; }

        /// <summary>Die aufrufende Instanz, oder null.</summary>
        public string? ParentInstanceId { get; init; }

        /// <summary>Der Endstand der Variablen.</summary>
        public IDictionary<string, object?> Variables { get; init; }
            = new Dictionary<string, object?>();

        /// <summary>Der Verlauf.</summary>
        public IReadOnlyList<HistoryEntry> History { get; init; } = new List<HistoryEntry>();

        /// <summary>Die Kommentare.</summary>
        public IReadOnlyList<WorkflowArchivedComment> Comments { get; init; }
            = new List<WorkflowArchivedComment>();

        /// <summary>Die Beschreibungen der Anhaenge - auch die, deren Inhalt weg ist.</summary>
        public IReadOnlyList<WorkflowArchivedAttachment> Attachments { get; init; }
            = new List<WorkflowArchivedAttachment>();
    }
}
