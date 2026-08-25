using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface ITenantTypeAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<TenantTypeViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<TenantTypeViewModel?> CreateAsync(ClaimsPrincipal user, TenantTypeViewModel input);
    Task<TenantTypeViewModel?> UpdateAsync(ClaimsPrincipal user, TenantTypeViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int tenantTypeId);
}
