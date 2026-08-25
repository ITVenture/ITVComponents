using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IDashboardWidgetAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<DashboardWidgetViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<DashboardWidgetViewModel?> CreateAsync(ClaimsPrincipal user, DashboardWidgetViewModel input);
    Task<DashboardWidgetViewModel?> UpdateAsync(ClaimsPrincipal user, DashboardWidgetViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int dashboardWidgetId);

    /// <summary>
    /// Legt ein Widget in der Standard-Sammlung vor oder hinter ein anderes.
    /// </summary>
    /// <param name="user">der handelnde Benutzer - braucht <c>DashboardWidgets.Write</c></param>
    /// <param name="draggedWidgetId">das verschobene Widget</param>
    /// <param name="anchorWidgetId">das Widget, an dem es abgelegt wurde</param>
    /// <param name="below">true = dahinter, false = davor</param>
    /// <returns>false, wenn die Berechtigung fehlt oder eines der beiden Widgets nicht existiert</returns>
    Task<bool> MoveAsync(ClaimsPrincipal user, int draggedWidgetId, int anchorWidgetId, bool below);

    Task<PagedResult<DashboardParamViewModel>> ListParamsAsync(ClaimsPrincipal user, int dashboardWidgetId, ListQuery query);
    Task<DashboardParamViewModel?> CreateParamAsync(ClaimsPrincipal user, int dashboardWidgetId, DashboardParamViewModel input);
    Task<DashboardParamViewModel?> UpdateParamAsync(ClaimsPrincipal user, DashboardParamViewModel input);
    Task<bool> DeleteParamAsync(ClaimsPrincipal user, int dashboardParamId);

    Task<PagedResult<DashboardWidgetLocalizationViewModel>> ListLocalesAsync(ClaimsPrincipal user, int dashboardWidgetId, ListQuery query);
    Task<DashboardWidgetLocalizationViewModel?> CreateLocaleAsync(ClaimsPrincipal user, int dashboardWidgetId, DashboardWidgetLocalizationViewModel input);
    Task<DashboardWidgetLocalizationViewModel?> UpdateLocaleAsync(ClaimsPrincipal user, DashboardWidgetLocalizationViewModel input);
    Task<bool> DeleteLocaleAsync(ClaimsPrincipal user, int dashboardWidgetLocalizationId);
}
