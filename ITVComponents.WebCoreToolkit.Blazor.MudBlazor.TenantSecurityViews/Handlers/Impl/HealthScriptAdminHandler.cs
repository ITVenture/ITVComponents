using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class HealthScriptAdminHandler : IHealthScriptAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;

    public HealthScriptAdminHandler(ICoreSystemContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        this.db.ShowAllTenants = true;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<HealthScriptViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "HealthChecks.View", "HealthChecks.Write", "Sysadmin"))
            return new PagedResult<HealthScriptViewModel>();

        var q = db.HealthScripts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(h => h.HealthScriptName.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(h => h.HealthScriptName) : q.OrderBy(h => h.HealthScriptName);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(h => new HealthScriptViewModel
            {
                HealthScriptId = h.HealthScriptId,
                HealthScriptName = h.HealthScriptName,
                Script = h.Script
            }).ToListAsync();
        return new PagedResult<HealthScriptViewModel> { Items = items, TotalCount = total };
    }

    public async Task<HealthScriptViewModel?> CreateAsync(ClaimsPrincipal user, HealthScriptViewModel input)
    {
        if (!HasPermission(user, "HealthChecks.Write", "Sysadmin")) return null;
        var entity = new HealthScript
        {
            HealthScriptName = input.HealthScriptName,
            Script = input.Script ?? string.Empty
        };
        db.HealthScripts.Add(entity);
        await db.SaveChangesAsync();
        input.HealthScriptId = entity.HealthScriptId;
        return input;
    }

    public async Task<HealthScriptViewModel?> UpdateAsync(ClaimsPrincipal user, HealthScriptViewModel input)
    {
        if (!HasPermission(user, "HealthChecks.Write", "Sysadmin")) return null;
        var entity = await db.HealthScripts.FirstOrDefaultAsync(h => h.HealthScriptId == input.HealthScriptId);
        if (entity == null) return null;
        entity.HealthScriptName = input.HealthScriptName;
        entity.Script = input.Script ?? string.Empty;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int healthScriptId)
    {
        if (!HasPermission(user, "HealthChecks.Write", "Sysadmin")) return false;
        var entity = await db.HealthScripts.FirstOrDefaultAsync(h => h.HealthScriptId == healthScriptId);
        if (entity == null) return false;
        db.HealthScripts.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
