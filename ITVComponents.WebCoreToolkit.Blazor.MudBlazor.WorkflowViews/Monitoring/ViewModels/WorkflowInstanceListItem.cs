using System;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels
{
    /// <summary>
    /// Eine Zeile der Instanz-Uebersicht (aus den indizierten Spalten der persistierten Instanz).
    /// </summary>
    public sealed class WorkflowInstanceListItem
    {
        /// <summary>Instanz-Id.</summary>
        public string Id { get; init; } = "";

        /// <summary>Fachliche Id der Definition.</summary>
        public string DefinitionId { get; init; } = "";

        /// <summary>Version der Definition.</summary>
        public int DefinitionVersion { get; init; }

        /// <summary>Status als Name (Running/Waiting/Completed/Faulted/Cancelled).</summary>
        public string Status { get; init; } = "";

        /// <summary>
        /// Die Dringlichkeit der Instanz in der Hintergrund-Abarbeitung (kleinere Zahl = wichtiger).
        /// </summary>
        public int Priority { get; init; }

        /// <summary>Korrelationsschluessel, oder null.</summary>
        public string? CorrelationKey { get; init; }

        /// <summary>Erstellzeitpunkt (UTC).</summary>
        public DateTime CreatedUtc { get; init; }

        /// <summary>Zeitpunkt der letzten Aenderung (UTC).</summary>
        public DateTime UpdatedUtc { get; init; }
    }
}
