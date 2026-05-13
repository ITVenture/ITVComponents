using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface ITenantTemplateAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<TenantTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<TenantTemplateViewModel?> CreateAsync(ClaimsPrincipal user, TenantTemplateViewModel input);
    Task<TenantTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, TenantTemplateViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int tenantTemplateId);
}
