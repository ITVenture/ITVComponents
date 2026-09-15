using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IPermissionSetAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<PermissionSetViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);

    /// <summary>
    /// Die Templates fuer die Auswahl beim Anlegen eines Buendels.
    /// </summary>
    /// <remarks>
    /// Ein Buendel gehoert genau einem Template, und die Zugehoerigkeit ist Pflicht - ohne diese Liste
    /// gaebe es auf dieser Seite keinen Weg, sie zu setzen.
    /// </remarks>
    Task<IReadOnlyList<ClientAppTemplateViewModel>> ListTemplatesAsync(ClaimsPrincipal user);
    Task<PermissionSetViewModel?> CreateAsync(ClaimsPrincipal user, PermissionSetViewModel input);
    Task<PermissionSetViewModel?> UpdateAsync(ClaimsPrincipal user, PermissionSetViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int appPermissionSetId);

    Task<PagedResult<AppPermissionAssignmentViewModel>> ListPermissionsForSetAsync(
        ClaimsPrincipal user, int appPermissionSetId, ListQuery query);
    Task<bool> SetPermissionForSetAsync(
        ClaimsPrincipal user, int appPermissionSetId, int permissionId, bool assigned);
}
