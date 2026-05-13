using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class FeatureAdminHandler : IFeatureAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;

    public FeatureAdminHandler(ICoreSystemContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        this.db.ShowAllTenants = true;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<FeatureViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "Features.View", "Features.Write"))
            return new PagedResult<FeatureViewModel>();

        var q = db.Features.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(f => f.FeatureName.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(f => f.FeatureName) : q.OrderBy(f => f.FeatureName);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(f => new FeatureViewModel
            {
                FeatureId = f.FeatureId,
                FeatureName = f.FeatureName,
                FeatureDescription = f.FeatureDescription,
                Enabled = f.Enabled
            }).ToListAsync();
        return new PagedResult<FeatureViewModel> { Items = items, TotalCount = total };
    }

    public async Task<FeatureViewModel?> CreateAsync(ClaimsPrincipal user, FeatureViewModel input)
    {
        if (!HasPermission(user, "Features.Write")) return null;
        var entity = new Feature
        {
            FeatureName = input.FeatureName,
            FeatureDescription = input.FeatureDescription ?? string.Empty,
            Enabled = input.Enabled
        };
        db.Features.Add(entity);
        await db.SaveChangesAsync();
        input.FeatureId = entity.FeatureId;
        return input;
    }

    public async Task<FeatureViewModel?> UpdateAsync(ClaimsPrincipal user, FeatureViewModel input)
    {
        if (!HasPermission(user, "Features.Write")) return null;
        var entity = await db.Features.FirstOrDefaultAsync(f => f.FeatureId == input.FeatureId);
        if (entity == null) return null;
        entity.FeatureName = input.FeatureName;
        entity.FeatureDescription = input.FeatureDescription ?? string.Empty;
        entity.Enabled = input.Enabled;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int featureId)
    {
        if (!HasPermission(user, "Features.Write")) return false;
        var entity = await db.Features.FirstOrDefaultAsync(f => f.FeatureId == featureId);
        if (entity == null) return false;
        db.Features.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
