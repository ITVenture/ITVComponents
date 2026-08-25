using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IGlobalSettingsAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<GlobalSettingViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<GlobalSettingViewModel?> CreateAsync(ClaimsPrincipal user, GlobalSettingViewModel input);
    Task<GlobalSettingViewModel?> UpdateAsync(ClaimsPrincipal user, GlobalSettingViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int globalSettingId);
}
