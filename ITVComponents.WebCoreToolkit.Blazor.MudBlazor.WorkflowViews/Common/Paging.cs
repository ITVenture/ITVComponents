using System;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common
{
    /// <summary>
    /// Eine serverseitige Listenabfrage (Paging, Sortierung, Suche) - gemeinsame Form fuer die
    /// Grids dieses Moduls.
    /// </summary>
    public sealed class WorkflowListQuery
    {
        /// <summary>Nullbasierte Seitennummer.</summary>
        public int Page { get; init; }

        /// <summary>Seitengroesse.</summary>
        public int PageSize { get; init; } = 25;

        /// <summary>Sortierspalte, oder null.</summary>
        public string? SortColumn { get; init; }

        /// <summary>Absteigend sortieren.</summary>
        public bool SortDescending { get; init; }

        /// <summary>Freitext-Suche, oder null.</summary>
        public string? Search { get; init; }

        /// <summary>Optionaler Statusfilter (numerischer WorkflowStatus), oder null.</summary>
        public int? Status { get; init; }
    }


}
