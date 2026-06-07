using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface ITrustedComponentAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<TrustedComponentViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<TrustedComponentViewModel?> CreateAsync(ClaimsPrincipal user, TrustedComponentViewModel input);
    Task<TrustedComponentViewModel?> UpdateAsync(ClaimsPrincipal user, TrustedComponentViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int trustedFullAccessComponentId);
}
