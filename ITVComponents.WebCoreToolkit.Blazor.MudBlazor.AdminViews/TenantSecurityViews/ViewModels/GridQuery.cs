using MudBlazor;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

/// <summary>
/// Uebersetzt zwischen dem Zustand eines <c>MudDataGrid</c> und den Listen-Vertraegen der Handler.
/// </summary>
/// <remarks>
/// Das <c>ServerData</c>-Muster ist in ueber fuenfzig Rasterseiten dasselbe: Seitenzahl, Seitengroesse
/// und die erste Sortier-Vorgabe aus dem <see cref="GridState{T}"/> in eine <see cref="ListQuery"/>
/// tragen, das Ergebnis in ein <see cref="GridData{T}"/> zurueck.
/// <para>
/// <b>Warum das nicht bloss Tipparbeit spart:</b> die Sortierung geht ueber
/// <c>state.SortDefinitions.FirstOrDefault()</c> - wer die Zeile vergisst oder <c>SortColumn</c> nicht
/// setzt, bekommt ein Raster, dessen Spaltenkoepfe sich anklicken lassen und nichts bewirken. Genau
/// dieser Fehler stand schon einmal im Bericht (Runde 1, Befund 5: „Spaltensortierung im Tsc-Modus
/// wirkungslos"). Hier kann er nicht mehr entstehen.
/// </para>
/// <para>
/// Bewusst nur diese Uebersetzung und kein gemeinsames Raster-Bauteil: dafuer braeuchte es einen
/// gemeinsamen Handler-Vertrag (<c>ListAsync</c>/<c>CreateAsync</c>/…), den es nicht gibt - und ihn
/// nachzuruesten waere breaking fuer Hosts mit eigenen Handler-Implementierungen.
/// </para></remarks>
public static class GridQuery
{
    /// <summary>
    /// Baut die Listen-Abfrage aus dem Rasterzustand: Seite, Seitengroesse und die erste Sortier-Vorgabe.
    /// </summary>
    /// <param name="state">der Zustand, den <c>MudDataGrid.ServerData</c> uebergibt</param>
    /// <param name="search">der Suchbegriff der Maske, oder null</param>
    /// <param name="tenantId">der Mandant, falls die Seite auf einen eingeschraenkt ist</param>
    public static ListQuery ToListQuery<T>(this GridState<T> state, string? search = null,
        int? tenantId = null)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        return new ListQuery
        {
            Page = state.Page,
            PageSize = state.PageSize,
            SortColumn = sort?.SortBy,
            SortDescending = sort?.Descending ?? false,
            Search = search,
            TenantId = tenantId
        };
    }
}
