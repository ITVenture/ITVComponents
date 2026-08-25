using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IPlugInAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<WebPluginViewModel>> ListPluginsAsync(ClaimsPrincipal user, int? tenantId, ListQuery query);
    Task<WebPluginViewModel?> CreatePluginAsync(ClaimsPrincipal user, int? tenantId, WebPluginViewModel input);
    Task<WebPluginViewModel?> UpdatePluginAsync(ClaimsPrincipal user, WebPluginViewModel input);
    Task<bool> DeletePluginAsync(ClaimsPrincipal user, int webPluginId);

    Task<PagedResult<WebPluginGenericParameterViewModel>> ListPluginParametersAsync(ClaimsPrincipal user, int webPluginId, ListQuery query);
    Task<WebPluginGenericParameterViewModel?> CreatePluginParameterAsync(ClaimsPrincipal user, int webPluginId, WebPluginGenericParameterViewModel input);
    Task<WebPluginGenericParameterViewModel?> UpdatePluginParameterAsync(ClaimsPrincipal user, WebPluginGenericParameterViewModel input);
    Task<bool> DeletePluginParameterAsync(ClaimsPrincipal user, int webPluginGenericParameterId);

    Task<PagedResult<WebPluginConstantViewModel>> ListConstantsAsync(ClaimsPrincipal user, int? tenantId, ListQuery query);
    Task<WebPluginConstantViewModel?> CreateConstantAsync(ClaimsPrincipal user, int? tenantId, WebPluginConstantViewModel input);
    Task<WebPluginConstantViewModel?> UpdateConstantAsync(ClaimsPrincipal user, WebPluginConstantViewModel input);
    Task<bool> DeleteConstantAsync(ClaimsPrincipal user, int webPluginConstantId);
}
