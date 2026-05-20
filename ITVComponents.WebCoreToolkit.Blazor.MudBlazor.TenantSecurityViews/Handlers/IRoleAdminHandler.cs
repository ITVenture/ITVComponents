using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface IRoleAdminHandler
{
    AdminContext GetContext(ClaimsPrincipal user);

    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<RoleViewModel>> ListRolesAsync(ClaimsPrincipal user, int tenantId, ListQuery query);
    Task<RoleViewModel?> CreateRoleAsync(ClaimsPrincipal user, int tenantId, RoleViewModel input);
    Task<RoleViewModel?> UpdateRoleAsync(ClaimsPrincipal user, RoleViewModel input);
    Task<bool> DeleteRoleAsync(ClaimsPrincipal user, int roleId);

    Task<PagedResult<RoleAssignmentViewModel>> ListRoleAssignmentsForTenantUserAsync(
        ClaimsPrincipal user, int tenantUserId, int tenantId, ListQuery query);
    Task<bool> SetRoleAssignmentForTenantUserAsync(
        ClaimsPrincipal user, int tenantUserId, int roleId, int tenantId, bool assigned);

    Task<PagedResult<PermissionAssignmentViewModel>> ListPermissionAssignmentsForRoleAsync(
        ClaimsPrincipal user, int roleId, int tenantId, ListQuery query);
    Task<bool> SetPermissionAssignmentForRoleAsync(
        ClaimsPrincipal user, int roleId, int permissionId, int tenantId, bool assigned);

    Task<PagedResult<RoleRoleAssignmentViewModel>> ListPermittedRolesForRoleAsync(
        ClaimsPrincipal user, int permissiveRoleId, int tenantId, ListQuery query);
    Task<bool> SetPermittedRoleForRoleAsync(
        ClaimsPrincipal user, int permissiveRoleId, int permittedRoleId, int tenantId, bool assigned);
}
