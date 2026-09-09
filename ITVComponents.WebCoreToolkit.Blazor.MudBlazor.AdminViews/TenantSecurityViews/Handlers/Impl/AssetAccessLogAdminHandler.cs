using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

/// <summary>
/// Liest das Zugriffsprotokoll.
/// <para>
/// <b>Die Mandantengrenze zieht dieser Handler von Hand</b> - die Tabelle liegt als Systemtabelle im
/// mandantenfreien Kontext, weil die Freigabe selbst generisch ist. Ohne den Filter unten saehe ein
/// Mandant die Zugriffe der anderen; er gehoert deshalb in JEDE Abfrage auf diese Tabelle.
/// </para>
/// </summary>
public class AssetAccessLogAdminHandler : IAssetAccessLogAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IPermissionScope permissionScope;

    public AssetAccessLogAdminHandler(ICoreSystemContextFactory factory, IPermissionScope permissionScope)
    {
        this.factory = factory;
        this.permissionScope = permissionScope;
    }

    public async Task<PagedResult<AssetAccessViewModel>> ListAsync(ClaimsPrincipal user, string? assetKey,
        bool deniedOnly, ListQuery query)
    {
        var tenant = permissionScope.PermissionPrefix;
        if (string.IsNullOrEmpty(tenant))
        {
            // Ohne Mandanten gibt es nichts zu zeigen - und ganz sicher nicht alles.
            return new PagedResult<AssetAccessViewModel>();
        }

        return await factory.UseAsync(async db =>
        {
            var q = db.SharedAssetAccesses.AsNoTracking().Where(n => n.TenantName == tenant);
            if (!string.IsNullOrEmpty(assetKey))
            {
                q = q.Where(n => n.AssetKey == assetKey);
            }

            if (deniedOnly)
            {
                q = q.Where(n => !n.Granted);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                q = q.Where(n => n.RequestPath.Contains(term) || n.AccessedBy.Contains(term)
                                                             || n.ArgumentSummary.Contains(term));
            }

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(n => n.SharedAssetAccessId).Page(n => n.SharedAssetAccessId, query)
                .Select(n => new AssetAccessViewModel
                {
                    SharedAssetAccessId = n.SharedAssetAccessId,
                    AssetKey = n.AssetKey,
                    TicketNonce = n.TicketNonce,
                    TemplateSystemKey = n.TemplateSystemKey,
                    RecipientLabel = n.RecipientLabel,
                    AccessedBy = n.AccessedBy,
                    RequestPath = n.RequestPath,
                    ArgumentSummary = n.ArgumentSummary,
                    Granted = n.Granted,
                    DenyReason = n.DenyReason,
                    Created = n.Created
                }).ToListAsync();
            return new PagedResult<AssetAccessViewModel> { Items = items, TotalCount = total };
        });
    }
}
