using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface IAppTemplateAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<ClientAppTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<ClientAppTemplateViewModel?> CreateAsync(ClaimsPrincipal user, ClientAppTemplateViewModel input);
    Task<ClientAppTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, ClientAppTemplateViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int clientAppTemplateId);

    Task<PagedResult<AppPermissionSetAssignmentViewModel>> ListPermissionSetsForTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, ListQuery query);
    Task<bool> SetPermissionSetForTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, int appPermissionSetId, bool assigned);
}
