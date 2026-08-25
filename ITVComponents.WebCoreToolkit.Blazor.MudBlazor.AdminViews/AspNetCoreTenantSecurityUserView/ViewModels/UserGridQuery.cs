using MudBlazor;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.ViewModels;

/// <summary>
/// Das Gegenstueck zu <c>TenantSecurityViews.ViewModels.GridQuery</c> fuer die Benutzer-Ansichten.
/// </summary>
/// <remarks>
/// Zweimal dasselbe, und das ist bekannt: <see cref="ListQuery"/> und <c>ListQuery</c> sind Feld
/// fuer Feld identisch, <c>PagedResult&lt;T&gt;</c> ist es ebenfalls. Sie zusammenzulegen ist ein
/// <b>breaking change</b> - die Vertraege stehen in den Handler-Schnittstellen, die Hosts selbst
/// implementieren, und ein Namensraum-Wechsel bricht sie. Der Bericht hat das ausdruecklich
/// zurueckgestellt; bis dahin ist eine zweite kleine Uebersetzung der ehrlichere Preis als ein Umbau,
/// der fremden Code bricht.
/// </remarks>
public static class UserGridQuery
{
    /// <summary>
    /// Baut die Listen-Abfrage aus dem Rasterzustand - siehe
    /// <c>TenantSecurityViews.ViewModels.GridQuery.ToListQuery</c>, auch zur Begruendung.
    /// </summary>
    /// <param name="state">der Zustand, den <c>MudDataGrid.ServerData</c> uebergibt</param>
    /// <param name="search">der Suchbegriff der Maske, oder null</param>
    /// <param name="tenantId">der Mandant, falls die Seite auf einen eingeschraenkt ist</param>
    public static ListQuery ToUserListQuery<T>(this GridState<T> state, string? search = null,
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
