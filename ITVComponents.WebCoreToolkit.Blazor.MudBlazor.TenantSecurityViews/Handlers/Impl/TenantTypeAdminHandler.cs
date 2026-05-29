using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class TenantTypeAdminHandler : ITenantTypeAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;
    private readonly IForeignKeyWriteTracker fkWriteTracker;

    public TenantTypeAdminHandler(ICoreSystemContext db, IServiceProvider services, IForeignKeyWriteTracker fkWriteTracker)
    {
        this.db = db;
        this.services = services;
        this.fkWriteTracker = fkWriteTracker;
        this.db.ShowAllTenants = true;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<TenantTypeViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "TenantTypes.View", "TenantTypes.Write"))
            return new PagedResult<TenantTypeViewModel>();

        var q = db.TenantTypes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.TenantTypeName.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(t => t.TenantTypeName) : q.OrderBy(t => t.TenantTypeName);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(t => new TenantTypeViewModel
            {
                TenantTypeId = t.TenantTypeId,
                TenantTypeName = t.TenantTypeName,
                TypeMetaData = t.TypeMetaData,
                TenantTemplateId = t.TenantTemplateId
            }).ToListAsync();
        return new PagedResult<TenantTypeViewModel> { Items = items, TotalCount = total };
    }

    public async Task<TenantTypeViewModel?> CreateAsync(ClaimsPrincipal user, TenantTypeViewModel input)
    {
        if (!HasPermission(user, "TenantTypes.Write")) return null;
        var entity = new TenantType
        {
            TenantTypeName = input.TenantTypeName,
            TypeMetaData = input.TypeMetaData,
            TenantTemplateId = input.TenantTemplateId
        };
        db.TenantTypes.Add(entity);
        await db.SaveChangesAsync();
        fkWriteTracker.MarkWritten("TenantTypes");
        input.TenantTypeId = entity.TenantTypeId;
        return input;
    }

    public async Task<TenantTypeViewModel?> UpdateAsync(ClaimsPrincipal user, TenantTypeViewModel input)
    {
        if (!HasPermission(user, "TenantTypes.Write")) return null;
        var entity = await db.TenantTypes.FirstOrDefaultAsync(t => t.TenantTypeId == input.TenantTypeId);
        if (entity == null) return null;
        entity.TenantTypeName = input.TenantTypeName;
        entity.TypeMetaData = input.TypeMetaData;
        entity.TenantTemplateId = input.TenantTemplateId;
        await db.SaveChangesAsync();
        fkWriteTracker.MarkWritten("TenantTypes");
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int tenantTypeId)
    {
        if (!HasPermission(user, "TenantTypes.Write")) return false;
        var entity = await db.TenantTypes.FirstOrDefaultAsync(t => t.TenantTypeId == tenantTypeId);
        if (entity == null) return false;
        db.TenantTypes.Remove(entity);
        await db.SaveChangesAsync();
        fkWriteTracker.MarkWritten("TenantTypes");
        return true;
    }
}
