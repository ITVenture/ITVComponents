using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface IGlobalRoleAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<GlobalRoleViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<GlobalRoleViewModel?> CreateAsync(ClaimsPrincipal user, GlobalRoleViewModel input);
    Task<GlobalRoleViewModel?> UpdateAsync(ClaimsPrincipal user, GlobalRoleViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int globalRoleId);

    Task<PagedResult<GlobalPermissionAssignmentViewModel>> ListPermissionsForGlobalRoleAsync(
        ClaimsPrincipal user, int globalRoleId, ListQuery query);
    Task<bool> SetPermissionAssignmentForGlobalRoleAsync(
        ClaimsPrincipal user, int globalRoleId, int permissionId, bool assigned);

    Task<PagedResult<GlobalRoleForLocalRoleAssignmentViewModel>> ListGlobalRolesForLocalRoleAsync(
        ClaimsPrincipal user, int localRoleId, ListQuery query);
    Task<bool> SetGlobalRoleForLocalRoleAsync(
        ClaimsPrincipal user, int globalRoleId, int localRoleId, bool assigned);
}
