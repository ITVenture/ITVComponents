using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public enum NavigationMoveAnchor
{
    Above,
    Into,
    Below
}

public interface INavigationAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<NavigationMenuViewModel>> ListAsync(ClaimsPrincipal user, int? parentId, ListQuery query);
    Task<NavigationMenuViewModel?> CreateAsync(ClaimsPrincipal user, NavigationMenuViewModel input);
    Task<NavigationMenuViewModel?> UpdateAsync(ClaimsPrincipal user, NavigationMenuViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int navigationMenuId);

    Task<IReadOnlyList<NavigationParentChoice>> ListAllNavigationItemsAsync(ClaimsPrincipal user);
    Task<IReadOnlyList<TenantChoice>> ListAllTenantsAsync(ClaimsPrincipal user);

    Task<bool> MoveAsync(ClaimsPrincipal user, int draggedItemId, int? anchorItemId, NavigationMoveAnchor anchor);
}
