using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IHealthScriptAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<HealthScriptViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<HealthScriptViewModel?> CreateAsync(ClaimsPrincipal user, HealthScriptViewModel input);
    Task<HealthScriptViewModel?> UpdateAsync(ClaimsPrincipal user, HealthScriptViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int healthScriptId);
}
