using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IAssetTemplateAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<AssetTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<AssetTemplateViewModel?> CreateAsync(ClaimsPrincipal user, AssetTemplateViewModel input);
    Task<AssetTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, AssetTemplateViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int assetTemplateId);

    Task<PagedResult<AssetTemplatePathViewModel>> ListPathsAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query);
    Task<AssetTemplatePathViewModel?> CreatePathAsync(ClaimsPrincipal user, int assetTemplateId, AssetTemplatePathViewModel input);
    Task<AssetTemplatePathViewModel?> UpdatePathAsync(ClaimsPrincipal user, AssetTemplatePathViewModel input);
    Task<bool> DeletePathAsync(ClaimsPrincipal user, int assetTemplatePathId);

    Task<PagedResult<AssetTemplatePermissionAssignmentViewModel>> ListPermissionsForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query);
    Task<bool> SetPermissionForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, int permissionId, bool assigned);

    Task<PagedResult<AssetTemplateFeatureAssignmentViewModel>> ListFeaturesForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query);
    Task<bool> SetFeatureForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, int featureId, bool assigned);

    // Objektsicherheit: worauf eine mit dieser Vorlage erzeugte Freigabe zeigen kann, und wo das
    // verstanden wird.
    Task<PagedResult<AssetTemplateArgumentViewModel>> ListArgumentsAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query);
    Task<AssetTemplateArgumentViewModel?> CreateArgumentAsync(ClaimsPrincipal user, int assetTemplateId, AssetTemplateArgumentViewModel input);
    Task<AssetTemplateArgumentViewModel?> UpdateArgumentAsync(ClaimsPrincipal user, AssetTemplateArgumentViewModel input);
    Task<bool> DeleteArgumentAsync(ClaimsPrincipal user, int assetTemplateArgumentId);

    Task<PagedResult<AssetTemplateConsumerViewModel>> ListConsumersAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query);
    Task<AssetTemplateConsumerViewModel?> CreateConsumerAsync(ClaimsPrincipal user, int assetTemplateId, AssetTemplateConsumerViewModel input);
    Task<bool> DeleteConsumerAsync(ClaimsPrincipal user, int assetTemplateConsumerId);

    /// <summary>
    /// Die bekannten Endpunkte aus der Registry - zur Auswahl beim Zuordnen.
    /// </summary>
    Task<PagedResult<AssetConsumerViewModel>> ListKnownConsumersAsync(ClaimsPrincipal user, ListQuery query);

    /// <summary>
    /// Prueft eine Vorlage gegen die Registry und liefert Hinweise.
    /// <para>
    /// <b>Nur Hinweise, nie ein Fehler:</b> die Registry kennt nur, was sich seit dem Start gemeldet hat
    /// bzw. gespeichert wurde. "Unbekannt" ist hier nicht "falsch". Hart geprueft wird erst beim
    /// Erzeugen einer Freigabe - das braucht nur die Vorlage und ist deshalb immer verlaesslich.
    /// </para>
    /// </summary>
    Task<string[]> CheckAsync(ClaimsPrincipal user, int assetTemplateId);
}
