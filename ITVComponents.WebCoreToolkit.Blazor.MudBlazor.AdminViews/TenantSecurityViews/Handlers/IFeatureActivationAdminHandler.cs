using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IFeatureActivationAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<FeatureActivationViewModel>> ListActivationsAsync(ClaimsPrincipal user, int featureId, ListQuery query);
    Task<FeatureActivationViewModel?> CreateActivationAsync(ClaimsPrincipal user, FeatureActivationViewModel input);
    Task<FeatureActivationViewModel?> UpdateActivationAsync(ClaimsPrincipal user, FeatureActivationViewModel input);
    Task<bool> DeleteActivationAsync(ClaimsPrincipal user, int tenantFeatureActivationId);
}
