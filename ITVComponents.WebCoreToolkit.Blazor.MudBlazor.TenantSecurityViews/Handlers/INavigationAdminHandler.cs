using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface INavigationAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<NavigationMenuViewModel>> ListAsync(ClaimsPrincipal user, int? parentId, ListQuery query);
    Task<NavigationMenuViewModel?> CreateAsync(ClaimsPrincipal user, NavigationMenuViewModel input);
    Task<NavigationMenuViewModel?> UpdateAsync(ClaimsPrincipal user, NavigationMenuViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int navigationMenuId);

    Task<IReadOnlyList<NavigationParentChoice>> ListAllNavigationItemsAsync(ClaimsPrincipal user);
    Task<IReadOnlyList<TenantChoice>> ListAllTenantsAsync(ClaimsPrincipal user);
}
