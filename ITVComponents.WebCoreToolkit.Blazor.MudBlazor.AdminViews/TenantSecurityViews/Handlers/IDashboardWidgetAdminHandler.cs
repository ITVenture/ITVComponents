using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface IDashboardWidgetAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<DashboardWidgetViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<DashboardWidgetViewModel?> CreateAsync(ClaimsPrincipal user, DashboardWidgetViewModel input);
    Task<DashboardWidgetViewModel?> UpdateAsync(ClaimsPrincipal user, DashboardWidgetViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int dashboardWidgetId);

    Task<PagedResult<DashboardParamViewModel>> ListParamsAsync(ClaimsPrincipal user, int dashboardWidgetId, ListQuery query);
    Task<DashboardParamViewModel?> CreateParamAsync(ClaimsPrincipal user, int dashboardWidgetId, DashboardParamViewModel input);
    Task<DashboardParamViewModel?> UpdateParamAsync(ClaimsPrincipal user, DashboardParamViewModel input);
    Task<bool> DeleteParamAsync(ClaimsPrincipal user, int dashboardParamId);

    Task<PagedResult<DashboardWidgetLocalizationViewModel>> ListLocalesAsync(ClaimsPrincipal user, int dashboardWidgetId, ListQuery query);
    Task<DashboardWidgetLocalizationViewModel?> CreateLocaleAsync(ClaimsPrincipal user, int dashboardWidgetId, DashboardWidgetLocalizationViewModel input);
    Task<DashboardWidgetLocalizationViewModel?> UpdateLocaleAsync(ClaimsPrincipal user, DashboardWidgetLocalizationViewModel input);
    Task<bool> DeleteLocaleAsync(ClaimsPrincipal user, int dashboardWidgetLocalizationId);
}
