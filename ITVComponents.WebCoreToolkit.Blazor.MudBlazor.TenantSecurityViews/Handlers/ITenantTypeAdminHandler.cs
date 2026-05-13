using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface ITenantTypeAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<TenantTypeViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<TenantTypeViewModel?> CreateAsync(ClaimsPrincipal user, TenantTypeViewModel input);
    Task<TenantTypeViewModel?> UpdateAsync(ClaimsPrincipal user, TenantTypeViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int tenantTypeId);
}
