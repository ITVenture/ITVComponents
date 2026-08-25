using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class HealthScriptAdminHandler : IHealthScriptAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public HealthScriptAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<HealthScriptViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("HealthChecks.View", "HealthChecks.Write", "Sysadmin"))
            return new PagedResult<HealthScriptViewModel>();

        return await factory.UseAsync(async db =>
        {
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
        });
    }

    public async Task<HealthScriptViewModel?> CreateAsync(ClaimsPrincipal user, HealthScriptViewModel input)
    {
        if (!HasPermission("HealthChecks.Write", "Sysadmin")) return null;

        return await factory.UseAsync<HealthScriptViewModel?>(async db =>
        {
            var entity = new HealthScript
            {
                HealthScriptName = input.HealthScriptName,
                Script = input.Script ?? string.Empty
            };
            db.HealthScripts.Add(entity);
            await db.SaveChangesAsync();
            input.HealthScriptId = entity.HealthScriptId;
            return input;
        });
    }

    public async Task<HealthScriptViewModel?> UpdateAsync(ClaimsPrincipal user, HealthScriptViewModel input)
    {
        if (!HasPermission("HealthChecks.Write", "Sysadmin")) return null;

        return await factory.UseAsync<HealthScriptViewModel?>(async db =>
        {
            var entity = await db.HealthScripts.FirstOrDefaultAsync(h => h.HealthScriptId == input.HealthScriptId);
            if (entity == null) return null;
            entity.HealthScriptName = input.HealthScriptName;
            entity.Script = input.Script ?? string.Empty;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int healthScriptId)
    {
        if (!HasPermission("HealthChecks.Write", "Sysadmin")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.HealthScripts.FirstOrDefaultAsync(h => h.HealthScriptId == healthScriptId);
            if (entity == null) return false;
            db.HealthScripts.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }
}
