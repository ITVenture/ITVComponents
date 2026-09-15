using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IAppTemplateAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<ClientAppTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<ClientAppTemplateViewModel?> CreateAsync(ClaimsPrincipal user, ClientAppTemplateViewModel input);
    Task<ClientAppTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, ClientAppTemplateViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int clientAppTemplateId);

    /// <summary>Die Rechtebuendel, die dieses Template anbietet.</summary>
    Task<PagedResult<AppPermissionSetAssignmentViewModel>> ListPermissionSetsForTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, ListQuery query);

    /// <summary>
    /// Legt ein Rechtebuendel unter diesem Template an.
    /// </summary>
    /// <remarks>
    /// Hier und nicht auf der Buendel-Seite ist der natuerliche Ort: das Template steht fest, es gibt
    /// also nichts auszuwaehlen und nichts falsch zu waehlen.
    /// </remarks>
    Task<AppPermissionSetAssignmentViewModel?> CreatePermissionSetForTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, string name);

    /// <summary>
    /// Benennt ein Rechtebuendel dieses Templates um.
    /// </summary>
    Task<bool> RenamePermissionSetOfTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, int appPermissionSetId, string name);

    /// <summary>
    /// Loescht ein Rechtebuendel dieses Templates. Ersetzt das fruehere "Zuordnung entziehen" - ein
    /// Buendel gehoert genau einem Template, es gibt also nichts mehr zu entziehen.
    /// </summary>
    Task<bool> DeletePermissionSetFromTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, int appPermissionSetId);
}
