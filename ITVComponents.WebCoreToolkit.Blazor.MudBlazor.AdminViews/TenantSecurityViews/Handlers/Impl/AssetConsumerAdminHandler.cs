using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

/// <summary>
/// Nicht generisch - und das ist der Punkt: die beiden Tabellen haengen an keinem Mandanten und an
/// keiner der austauschbaren Entitaeten, sondern liegen als Systemtabellen im
/// <see cref="ICoreSystemContextFactory"/>-Kontext. Was ein Endpunkt versteht, ist fuer alle Mandanten
/// dasselbe.
/// </summary>
public class AssetConsumerAdminHandler : IAssetConsumerAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public AssetConsumerAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<AssetConsumerViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("Sysadmin"))
        {
            return new PagedResult<AssetConsumerViewModel>();
        }

        return await factory.UseAsync(async db =>
        {
            var q = db.AssetConsumers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(n => n.DeclarationKey.Contains(s));
            }

            var total = await q.CountAsync();
            q = query.SortDescending
                ? q.OrderByDescending(n => n.DeclarationKey)
                : q.OrderBy(n => n.DeclarationKey);
            var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(n => new AssetConsumerViewModel
                {
                    AssetConsumerId = n.AssetConsumerId,
                    DeclarationKind = n.DeclarationKind,
                    DeclarationKey = n.DeclarationKey,
                    FirstSeenUtc = n.FirstSeenUtc,
                    LastSeenUtc = n.LastSeenUtc,
                    ArgumentCount = n.Arguments.Count
                }).ToListAsync();
            return new PagedResult<AssetConsumerViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<PagedResult<AssetConsumerArgumentViewModel>> ListArgumentsAsync(ClaimsPrincipal user,
        int assetConsumerId, ListQuery query)
    {
        if (!HasPermission("Sysadmin"))
        {
            return new PagedResult<AssetConsumerArgumentViewModel>();
        }

        return await factory.UseAsync(async db =>
        {
            var q = db.AssetConsumerArguments.AsNoTracking()
                .Where(n => n.AssetConsumerId == assetConsumerId);
            var total = await q.CountAsync();
            // Nach SortOrder, nicht alphabetisch: das ist die Reihenfolge, in der der Endpunkt seine
            // Argumente nennt, und die soll der Betrachter sehen.
            var items = await q.OrderBy(n => n.SortOrder).Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(n => new AssetConsumerArgumentViewModel
                {
                    AssetConsumerArgumentId = n.AssetConsumerArgumentId,
                    AssetConsumerId = n.AssetConsumerId,
                    ArgumentName = n.ArgumentName,
                    ArgumentType = n.ArgumentType,
                    Required = n.Required,
                    SortOrder = n.SortOrder
                }).ToListAsync();
            return new PagedResult<AssetConsumerArgumentViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int assetConsumerId)
    {
        if (!HasPermission("Sysadmin"))
        {
            return false;
        }

        return await factory.UseAsync(async db =>
        {
            var entity = await db.AssetConsumers.FirstOrDefaultAsync(n => n.AssetConsumerId == assetConsumerId);
            if (entity == null)
            {
                return false;
            }

            // Die Argumente haengen per Cascade daran.
            db.AssetConsumers.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }
}
