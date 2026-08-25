using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface ITenantTemplateAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<TenantTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<TenantTemplateViewModel?> CreateAsync(ClaimsPrincipal user, TenantTemplateViewModel input);
    Task<TenantTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, TenantTemplateViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int tenantTemplateId);
}
