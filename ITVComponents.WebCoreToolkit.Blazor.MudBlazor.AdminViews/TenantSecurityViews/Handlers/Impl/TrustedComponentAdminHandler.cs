using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class TrustedComponentAdminHandler : ITrustedComponentAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public TrustedComponentAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<TrustedComponentViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("TrustedComponents.View", "TrustedComponents.Write"))
            return new PagedResult<TrustedComponentViewModel>();

        return await factory.UseAsync(async db =>
        {
            var q = db.TrustedFullAccessComponents.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(t => t.FullQualifiedTypeName.Contains(s));
            }
            var total = await q.CountAsync();
            q = query.SortDescending ? q.OrderByDescending(t => t.FullQualifiedTypeName) : q.OrderBy(t => t.FullQualifiedTypeName);
            var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(t => new TrustedComponentViewModel
                {
                    TrustedFullAccessComponentId = t.TrustedFullAccessComponentId,
                    FullQualifiedTypeName = t.FullQualifiedTypeName,
                    TargetQualifiedTypeName = t.TargetQualifiedTypeName,
                    Description = t.Description,
                    TrustLevelConfig = t.TrustLevelConfig
                }).ToListAsync();
            return new PagedResult<TrustedComponentViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<TrustedComponentViewModel?> CreateAsync(ClaimsPrincipal user, TrustedComponentViewModel input)
    {
        if (!HasPermission("TrustedComponents.Write")) return null;

        return await factory.UseAsync<TrustedComponentViewModel?>(async db =>
        {
            var entity = new TrustedFullAccessComponent
            {
                FullQualifiedTypeName = input.FullQualifiedTypeName,
                TargetQualifiedTypeName = input.TargetQualifiedTypeName ?? string.Empty,
                Description = input.Description ?? string.Empty,
                TrustLevelConfig = input.TrustLevelConfig ?? string.Empty
            };
            db.TrustedFullAccessComponents.Add(entity);
            await db.SaveChangesAsync();
            input.TrustedFullAccessComponentId = entity.TrustedFullAccessComponentId;
            return input;
        });
    }

    public async Task<TrustedComponentViewModel?> UpdateAsync(ClaimsPrincipal user, TrustedComponentViewModel input)
    {
        if (!HasPermission("TrustedComponents.Write")) return null;

        return await factory.UseAsync<TrustedComponentViewModel?>(async db =>
        {
            var entity = await db.TrustedFullAccessComponents.FirstOrDefaultAsync(t => t.TrustedFullAccessComponentId == input.TrustedFullAccessComponentId);
            if (entity == null) return null;
            entity.FullQualifiedTypeName = input.FullQualifiedTypeName;
            entity.TargetQualifiedTypeName = input.TargetQualifiedTypeName ?? string.Empty;
            entity.Description = input.Description ?? string.Empty;
            entity.TrustLevelConfig = input.TrustLevelConfig ?? string.Empty;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int trustedFullAccessComponentId)
    {
        if (!HasPermission("TrustedComponents.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.TrustedFullAccessComponents.FirstOrDefaultAsync(t => t.TrustedFullAccessComponentId == trustedFullAccessComponentId);
            if (entity == null) return false;
            db.TrustedFullAccessComponents.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }
}
