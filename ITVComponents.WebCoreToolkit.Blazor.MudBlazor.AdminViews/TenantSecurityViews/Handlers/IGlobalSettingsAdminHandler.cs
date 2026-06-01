using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface IGlobalSettingsAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<GlobalSettingViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<GlobalSettingViewModel?> CreateAsync(ClaimsPrincipal user, GlobalSettingViewModel input);
    Task<GlobalSettingViewModel?> UpdateAsync(ClaimsPrincipal user, GlobalSettingViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int globalSettingId);
}
