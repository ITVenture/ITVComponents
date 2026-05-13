using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface IFeatureAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<FeatureViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<FeatureViewModel?> CreateAsync(ClaimsPrincipal user, FeatureViewModel input);
    Task<FeatureViewModel?> UpdateAsync(ClaimsPrincipal user, FeatureViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int featureId);
}
