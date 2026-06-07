using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IPermissionAdminHandler
{
    AdminContext GetContext(ClaimsPrincipal user);

    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<PermissionViewModel>> ListPermissionsAsync(ClaimsPrincipal user, int? tenantId, ListQuery query);
    Task<PermissionViewModel?> CreatePermissionAsync(ClaimsPrincipal user, int? tenantId, PermissionViewModel input);
    Task<PermissionViewModel?> UpdatePermissionAsync(ClaimsPrincipal user, PermissionViewModel input);
    Task<bool> DeletePermissionAsync(ClaimsPrincipal user, int permissionId);
}
