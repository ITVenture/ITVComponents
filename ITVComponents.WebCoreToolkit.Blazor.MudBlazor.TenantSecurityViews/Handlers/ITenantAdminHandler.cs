using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface ITenantAdminHandler
{
    AdminContext GetContext(ClaimsPrincipal user);

    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<TenantViewModel>> ListTenantsAsync(ClaimsPrincipal user, ListQuery query);
    Task<TenantViewModel?> CreateTenantAsync(ClaimsPrincipal user, TenantViewModel input);
    Task<TenantViewModel?> UpdateTenantAsync(ClaimsPrincipal user, TenantViewModel input);
    Task<bool> DeleteTenantAsync(ClaimsPrincipal user, int tenantId);

    Task<PagedResult<TenantAssignmentViewModel>> ListTenantAssignmentsForUserAsync(
        ClaimsPrincipal user, string userId, ListQuery query);
    Task<bool> SetUserTenantAssignmentAsync(
        ClaimsPrincipal user, string userId, int tenantId, bool assigned);
}
