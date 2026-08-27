using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

/// <summary>
/// Die Freigaben eines Mandanten - anlegen, ansehen, zurueckziehen.
/// <para>
/// Bewusst duenn: die Regeln liegen im <see cref="ISharedAssetAdapter"/> (Berechtigung der Vorlage,
/// Mandantengrenze, Ortsbindung, harte Pruefung der Argumente). Dieser Handler uebersetzt nur
/// zwischen Maske und Adapter - haette er eigene Regeln, gaebe es zwei Wahrheiten.
/// </para>
/// </summary>
public interface ISharedAssetAdminHandler
{
    /// <summary>
    /// Die Vorlagen, mit denen an dieser Stelle geteilt werden darf - samt der Argumente, die sie
    /// verlangen. Leer heisst: hier gibt es nichts zu teilen.
    /// </summary>
    Task<AssetTemplateInfo[]> GetEligibleAsync(ClaimsPrincipal user, string requestPath);

    /// <summary>
    /// Legt eine Freigabe an und liefert den fertigen Link.
    /// </summary>
    /// <param name="user">der Teilende</param>
    /// <param name="request">was geteilt wird</param>
    /// <param name="origin">Schema und Host fuer den Link (im Circuit gibt es keine Anfrage)</param>
    Task<ShareResultViewModel> CreateAsync(ClaimsPrincipal user, ShareRequestViewModel request, string origin);

    Task<PagedResult<SharedAssetListItem>> ListAsync(ClaimsPrincipal user, ListQuery query);

    /// <summary>
    /// Der Link zu einer bestehenden Freigabe. <paramref name="anonymous"/> liefert die Fassung mit
    /// Zugangs-Token - die funktioniert ohne Anmeldung und ist entsprechend zu behandeln.
    /// </summary>
    Task<string> GetLinkAsync(ClaimsPrincipal user, string assetKey, bool anonymous, string origin);

    /// <summary>
    /// Erneuert das Geheimnis: verschickte anonyme Links werden ungueltig, die Freigabe bleibt.
    /// </summary>
    Task<bool> RotateAsync(ClaimsPrincipal user, string assetKey);

    /// <summary>
    /// Laedt eine Freigabe zum Bearbeiten. Liefert null, wenn der Aufrufer sie nicht verwalten darf -
    /// die vollen Angaben (Gueltigkeit, Filter) gibt es nur mit den Rechten der Vorlage.
    /// </summary>
    Task<SharedAssetEditViewModel?> GetForEditAsync(ClaimsPrincipal user, string assetKey);

    /// <summary>
    /// Aendert Titel, Gueltigkeit, Empfaenger und Filter einer bestehenden Freigabe.
    /// <para>
    /// <b>Nicht, worauf sie zeigt.</b> Die Argumentwerte stehen seit dem Anlegen fest; sie umzubiegen
    /// hiesse, einen verschickten Link stillschweigend auf ein anderes Objekt zu richten.
    /// </para>
    /// </summary>
    Task<bool> UpdateAsync(ClaimsPrincipal user, SharedAssetEditViewModel input);

    Task<bool> DeleteAsync(ClaimsPrincipal user, string assetKey);
}
