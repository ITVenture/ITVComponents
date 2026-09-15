using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

/// <summary>
/// Die Verwaltung der Anwendungen eines Mandanten und ihrer Zugaenge.
/// </summary>
/// <remarks>
/// <b>Arbeitet mandantengefiltert</b> - anders als die Template-Verwaltung, die global ist. Das Template
/// bestimmt, was zur Auswahl steht; hier entscheidet der Mandanten-Administrator, welchen Freiheitsgrad
/// er einer Anwendung tatsaechlich zugesteht.
/// </remarks>
public interface IClientAppAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<ClientAppViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);

    /// <summary>Die Templates, die beim Anlegen zur Auswahl stehen.</summary>
    Task<IReadOnlyList<ClientAppTemplateViewModel>> ListTemplatesAsync(ClaimsPrincipal user);

    /// <summary>
    /// Legt eine Anwendung an. Der <c>ClientKey</c> wird dabei erzeugt - er ist systemweit eindeutig und
    /// darf deshalb nicht von aussen kommen.
    /// </summary>
    Task<ClientAppViewModel?> CreateAsync(ClaimsPrincipal user, ClientAppViewModel input);

    /// <summary>Aendert Name und Schalter. Template und Kennung bleiben, wie sie sind.</summary>
    Task<ClientAppViewModel?> UpdateAsync(ClaimsPrincipal user, ClientAppViewModel input);

    /// <summary>
    /// Loescht eine Anwendung - nur wenn kein nicht-widerrufener Zugang mehr an ihr haengt.
    /// </summary>
    Task<bool> DeleteAsync(ClaimsPrincipal user, int clientAppId);

    /// <summary>Die Rechtebuendel des Templates dieser Anwendung, mit ihrem Zustand.</summary>
    Task<PagedResult<ClientAppPermissionSetViewModel>> ListPermissionSetsAsync(ClaimsPrincipal user,
        int clientAppId, ListQuery query);

    /// <summary>
    /// Stellt ein Buendel zu oder entzieht es. Ein Buendel, das nicht zum Template DIESER Anwendung
    /// gehoert, wird abgewiesen.
    /// </summary>
    Task<bool> SetPermissionSetAsync(ClaimsPrincipal user, int clientAppId, int appPermissionSetId,
        bool assigned);

    /// <summary>Die Zugaenge einer Anwendung.</summary>
    Task<PagedResult<ClientAppAccessViewModel>> ListAccessesAsync(ClaimsPrincipal user, int clientAppId,
        ListQuery query);

    /// <summary>Widerruft einen Zugang. Das Geraet kommt beim naechsten Aufruf nicht mehr herein.</summary>
    Task<bool> RevokeAccessAsync(ClaimsPrincipal user, int clientAppAccessId);
}
