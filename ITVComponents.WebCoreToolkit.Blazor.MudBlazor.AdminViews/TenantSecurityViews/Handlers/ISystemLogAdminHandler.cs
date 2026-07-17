using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface ISystemLogAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<SystemEventViewModel>> ListAsync(ClaimsPrincipal user, SystemLogQuery query);

    /// <summary>
    /// Loads the entries logged around a single entry, ignoring the filters of the surrounding list, so that the
    /// sequence of events leading to and following the anchor can be followed.
    /// </summary>
    Task<SystemLogContextResult> GetContextAsync(ClaimsPrincipal user, SystemLogContextQuery query);
}
