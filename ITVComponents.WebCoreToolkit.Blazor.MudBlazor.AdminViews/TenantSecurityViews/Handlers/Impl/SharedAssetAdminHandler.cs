using System.Linq;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

/// <summary>
/// Uebersetzt zwischen Maske und <see cref="ISharedAssetAdapter"/>. Absichtlich ohne eigene Regeln:
/// Berechtigung der Vorlage, Mandantengrenze, Ortsbindung und die harte Pruefung der Argumente liegen
/// im Adapter. Zwei Stellen mit Regeln waeren zwei Wahrheiten.
/// </summary>
public class SharedAssetAdminHandler : ISharedAssetAdminHandler
{
    /// <summary>
    /// Der Platzhalter, der auf jeden Angemeldeten passt. Derselbe Wert, den die Filterliste als Knopf
    /// anbietet - er steht hier noch einmal, weil eine Maske keine Konstante fuer eine andere ist.
    /// </summary>
    private const string EveryonePlaceholder = "%";

    private readonly ISharedAssetAdapter adapter;
    private readonly ILogger<SharedAssetAdminHandler> logger;

    public SharedAssetAdminHandler(ISharedAssetAdapter adapter, ILogger<SharedAssetAdminHandler> logger)
    {
        this.adapter = adapter;
        this.logger = logger;
    }

    public Task<AssetTemplateInfo[]> GetEligibleAsync(ClaimsPrincipal user, string requestPath)
        => Task.FromResult(adapter.GetEligibleShares(requestPath ?? "/"));

    public Task<ShareResultViewModel> CreateAsync(ClaimsPrincipal user, ShareRequestViewModel request, string origin)
    {
        var template = new AssetTemplateInfo { TemplateKey = request.TemplateKey };
        if (request.AdHoc)
        {
            // Ein Ticket entsteht und verschwindet mit seiner URL - es gibt hinterher nichts, was man
            // auflisten oder erneut abrufen koennte. Der Link ist die einzige Ausfertigung.
            var lifetime = request.LifetimeMinutes is > 0
                ? TimeSpan.FromMinutes(request.LifetimeMinutes.Value)
                : (TimeSpan?)null;
            var ticket = adapter.CreateAdHocTicket(request.RequestPath ?? "/", template, request.ArgumentValues,
                request.RecipientLabel, lifetime, origin, out var ticketError);
            if (ticket == null)
            {
                logger.LogInformation("An ad-hoc ticket was not created: {Reason}", ticketError);
                return Task.FromResult(new ShareResultViewModel { Success = false, Error = ticketError });
            }

            return Task.FromResult(new ShareResultViewModel { Success = true, Link = ticket });
        }

        // Die Reichweite geht mit in die Anlage und nicht erst in den Link. Ein anonymer Link auf eine
        // Freigabe, die niemanden ohne Anmeldung einlaesst, sieht richtig aus und fuehrt in einen 404 -
        // und dasselbe gilt fuer jede andere Freigabe ohne Reichweite.
        var users = request.Reach switch
        {
            ShareReach.AnyoneSignedIn => new List<string> { EveryonePlaceholder },
            ShareReach.Recipients => request.UserFilters,
            _ => new List<string>()
        };
        var tenants = request.Reach == ShareReach.Tenants ? request.TenantFilters : new List<string>();
        var created = adapter.CreateSharedAsset(request.RequestPath ?? "/", template, request.Title,
            request.ArgumentValues, request.RecipientLabel, request.Anonymous, users, tenants, out var error);
        if (created == null)
        {
            logger.LogInformation("A share was not created: {Reason}", error);
            return Task.FromResult(new ShareResultViewModel { Success = false, Error = error });
        }

        var link = request.Anonymous
            ? adapter.CreateAnonymousLink(created, origin)
            : adapter.CreateLink(created, origin);
        return Task.FromResult(new ShareResultViewModel
        {
            Success = true,
            AssetKey = created.AssetKey,
            Link = link
        });
    }

    public Task<PagedResult<SharedAssetListItem>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        var items = adapter.ListSharedAssets(query.Search, query.Page * query.PageSize, query.PageSize,
            out var total);
        return Task.FromResult(new PagedResult<SharedAssetListItem> { Items = items, TotalCount = total });
    }

    public Task<string> GetLinkAsync(ClaimsPrincipal user, string assetKey, bool anonymous, string origin)
    {
        // asOwner: nur wer die Freigabe verwalten darf, bekommt das Geheimnis zu sehen - und ohne das
        // laesst sich der anonyme Link gar nicht bauen.
        var info = adapter.GetAssetInfo(assetKey, user, asOwner: true);
        if (info == null)
        {
            logger.LogInformation("No link for '{AssetKey}': the share is not accessible for this user.", assetKey);
            return Task.FromResult(string.Empty);
        }

        return Task.FromResult(anonymous
            ? adapter.CreateAnonymousLink(info, origin) ?? string.Empty
            : adapter.CreateLink(info, origin));
    }

    public Task<bool> RotateAsync(ClaimsPrincipal user, string assetKey)
        => Task.FromResult(adapter.RotateAnonymousToken(assetKey));

    public Task<SharedAssetEditViewModel?> GetForEditAsync(ClaimsPrincipal user, string assetKey)
    {
        // asOwner: die Gueltigkeit und die Filter bekommt nur zu sehen, wer die Freigabe auch verwalten
        // darf - der Adapter entscheidet das anhand der Vorlage.
        if (adapter.GetAssetInfo(assetKey, user, asOwner: true) is not FullAssetInfo full)
        {
            logger.LogInformation("'{AssetKey}' can not be edited by this user.", assetKey);
            return Task.FromResult<SharedAssetEditViewModel?>(null);
        }

        var values = full.Values;
        return Task.FromResult<SharedAssetEditViewModel?>(new SharedAssetEditViewModel
        {
            AssetKey = full.AssetKey,
            AssetTitle = full.AssetTitle ?? string.Empty,
            NotBefore = full.NotBefore,
            NotAfter = full.NotAfter,
            RecipientLabel = full.RecipientLabel,
            UserFilters = full.UserShares.ToList(),
            TenantFilters = full.UserScopeShares.ToList(),
            ArgumentSummary = values == null || values.IsEmpty
                ? null
                : string.Join(", ", values.Names.Select(n => $"{n}={values[n]}"))
        });
    }

    public Task<bool> UpdateAsync(ClaimsPrincipal user, SharedAssetEditViewModel input)
    {
        if (adapter.GetAssetInfo(input.AssetKey, user, asOwner: true) is not FullAssetInfo full)
        {
            logger.LogInformation("'{AssetKey}' was not updated: not accessible for this user.", input.AssetKey);
            return Task.FromResult(false);
        }

        full.AssetTitle = input.AssetTitle;
        full.NotBefore = input.NotBefore;
        full.NotAfter = input.NotAfter;
        full.RecipientLabel = input.RecipientLabel;
        // Die beiden Listen sind der Soll-Zustand; der Adapter rechnet aus, was dazukommt und was geht.
        full.UserShares.Clear();
        full.UserShares.AddRange(input.UserFilters.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()));
        full.UserScopeShares.Clear();
        full.UserScopeShares.AddRange(input.TenantFilters.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()));
        return Task.FromResult(adapter.UpdateSharedAsset(full));
    }

    public Task<bool> DeleteAsync(ClaimsPrincipal user, string assetKey)
    {
        var info = adapter.GetAssetInfo(assetKey, user, asOwner: true);
        if (info is not FullAssetInfo full)
        {
            logger.LogInformation("'{AssetKey}' was not deleted: the share is not accessible for this user.", assetKey);
            return Task.FromResult(false);
        }

        return Task.FromResult(adapter.DeleteSharedAsset(full));
    }
}
