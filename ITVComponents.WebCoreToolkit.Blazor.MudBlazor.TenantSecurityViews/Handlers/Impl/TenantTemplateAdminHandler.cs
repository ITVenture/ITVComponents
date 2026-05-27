using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class TenantTemplateAdminHandler : ITenantTemplateAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;
    private readonly IForeignKeyWriteTracker fkWriteTracker;

    public TenantTemplateAdminHandler(ICoreSystemContext db, IServiceProvider services, IForeignKeyWriteTracker fkWriteTracker)
    {
        this.db = db;
        this.services = services;
        this.fkWriteTracker = fkWriteTracker;
        this.db.ShowAllTenants = true;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<TenantTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "TenantTemplates.View", "TenantTemplates.Write"))
            return new PagedResult<TenantTemplateViewModel>();

        var q = db.TenantTemplates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.Name.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(t => t.Name) : q.OrderBy(t => t.Name);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(t => new TenantTemplateViewModel
            {
                TenantTemplateId = t.TenantTemplateId,
                Name = t.Name,
                Description = t.Description,
                Markup = t.Markup
            }).ToListAsync();
        return new PagedResult<TenantTemplateViewModel> { Items = items, TotalCount = total };
    }

    public async Task<TenantTemplateViewModel?> CreateAsync(ClaimsPrincipal user, TenantTemplateViewModel input)
    {
        if (!HasPermission(user, "TenantTemplates.Write")) return null;
        var entity = new TenantTemplate
        {
            Name = input.Name,
            Description = input.Description ?? string.Empty,
            Markup = input.Markup ?? string.Empty
        };
        db.TenantTemplates.Add(entity);
        await db.SaveChangesAsync();
        fkWriteTracker.MarkWritten("TenantTemplates");
        input.TenantTemplateId = entity.TenantTemplateId;
        return input;
    }

    public async Task<TenantTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, TenantTemplateViewModel input)
    {
        if (!HasPermission(user, "TenantTemplates.Write")) return null;
        var entity = await db.TenantTemplates.FirstOrDefaultAsync(t => t.TenantTemplateId == input.TenantTemplateId);
        if (entity == null) return null;
        entity.Name = input.Name;
        entity.Description = input.Description ?? string.Empty;
        entity.Markup = input.Markup ?? string.Empty;
        await db.SaveChangesAsync();
        fkWriteTracker.MarkWritten("TenantTemplates");
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int tenantTemplateId)
    {
        if (!HasPermission(user, "TenantTemplates.Write")) return false;
        var entity = await db.TenantTemplates.FirstOrDefaultAsync(t => t.TenantTemplateId == tenantTemplateId);
        if (entity == null) return false;
        db.TenantTemplates.Remove(entity);
        await db.SaveChangesAsync();
        fkWriteTracker.MarkWritten("TenantTemplates");
        return true;
    }
}
