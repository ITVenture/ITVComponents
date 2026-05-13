using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class TrustedComponentAdminHandler : ITrustedComponentAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;

    public TrustedComponentAdminHandler(ICoreSystemContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        this.db.ShowAllTenants = true;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<TrustedComponentViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "TrustedComponents.View", "TrustedComponents.Write"))
            return new PagedResult<TrustedComponentViewModel>();

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
    }

    public async Task<TrustedComponentViewModel?> CreateAsync(ClaimsPrincipal user, TrustedComponentViewModel input)
    {
        if (!HasPermission(user, "TrustedComponents.Write")) return null;
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
    }

    public async Task<TrustedComponentViewModel?> UpdateAsync(ClaimsPrincipal user, TrustedComponentViewModel input)
    {
        if (!HasPermission(user, "TrustedComponents.Write")) return null;
        var entity = await db.TrustedFullAccessComponents.FirstOrDefaultAsync(t => t.TrustedFullAccessComponentId == input.TrustedFullAccessComponentId);
        if (entity == null) return null;
        entity.FullQualifiedTypeName = input.FullQualifiedTypeName;
        entity.TargetQualifiedTypeName = input.TargetQualifiedTypeName ?? string.Empty;
        entity.Description = input.Description ?? string.Empty;
        entity.TrustLevelConfig = input.TrustLevelConfig ?? string.Empty;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int trustedFullAccessComponentId)
    {
        if (!HasPermission(user, "TrustedComponents.Write")) return false;
        var entity = await db.TrustedFullAccessComponents.FirstOrDefaultAsync(t => t.TrustedFullAccessComponentId == trustedFullAccessComponentId);
        if (entity == null) return false;
        db.TrustedFullAccessComponents.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
