using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

/// <summary>
/// Zugriff auf die Endpunkte, die Argumente eines geteilten Assets verstehen.
/// <para>
/// Bewusst <b>ohne Anlegen und Bearbeiten</b>: die Zeilen sind eine Eigenschaft des Codes und werden
/// bei der naechsten Meldung des Endpunkts ueberschrieben. Von Hand geaenderte Werte waeren also
/// hoechstens bis zum naechsten Seitenaufbau haltbar. Was die Maske kann, ist zeigen - und aufraeumen,
/// wenn ein Endpunkt es nicht mehr gibt.
/// </para>
/// </summary>
public interface IAssetConsumerAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<AssetConsumerViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);

    Task<PagedResult<AssetConsumerArgumentViewModel>> ListArgumentsAsync(ClaimsPrincipal user, int assetConsumerId,
        ListQuery query);

    /// <summary>
    /// Entfernt einen Konsumenten samt seiner Argumente. Der einzige Schreibvorgang der Maske - und
    /// einer, der von Hand ausgeloest werden muss: dass sich ein Endpunkt seit dem Neustart nicht
    /// gemeldet hat, heisst nicht, dass es ihn nicht mehr gibt.
    /// </summary>
    Task<bool> DeleteAsync(ClaimsPrincipal user, int assetConsumerId);
}
