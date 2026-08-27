using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

/// <summary>
/// Das Zugriffsprotokoll der Freigaben eines Mandanten - lesend.
/// </summary>
public interface IAssetAccessLogAdminHandler
{
    /// <summary>
    /// Die Zugriffe des aktuellen Mandanten, neueste zuerst.
    /// </summary>
    /// <param name="user">wer fragt</param>
    /// <param name="assetKey">auf eine Freigabe einschraenken, oder null fuer alle</param>
    /// <param name="deniedOnly">nur die Verweigerungen - die Haelfte, die man meistens sucht</param>
    /// <param name="query">Seite und Suchbegriff</param>
    Task<PagedResult<AssetAccessViewModel>> ListAsync(ClaimsPrincipal user, string? assetKey, bool deniedOnly,
        ListQuery query);
}
