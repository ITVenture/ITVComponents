using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface IPermissionSetAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<PermissionSetViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<PermissionSetViewModel?> CreateAsync(ClaimsPrincipal user, PermissionSetViewModel input);
    Task<PermissionSetViewModel?> UpdateAsync(ClaimsPrincipal user, PermissionSetViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int appPermissionSetId);

    Task<PagedResult<AppPermissionAssignmentViewModel>> ListPermissionsForSetAsync(
        ClaimsPrincipal user, int appPermissionSetId, ListQuery query);
    Task<bool> SetPermissionForSetAsync(
        ClaimsPrincipal user, int appPermissionSetId, int permissionId, bool assigned);
}
