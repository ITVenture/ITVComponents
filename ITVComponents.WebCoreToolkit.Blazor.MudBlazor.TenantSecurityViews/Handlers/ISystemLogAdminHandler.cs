using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface ISystemLogAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<SystemEventViewModel>> ListAsync(ClaimsPrincipal user, SystemLogQuery query);
}
