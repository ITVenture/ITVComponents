using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class TenantTemplateAdminHandler : ITenantTemplateAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public TenantTemplateAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<TenantTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("TenantTemplates.View", "TenantTemplates.Write"))
            return new PagedResult<TenantTemplateViewModel>();

        return await factory.UseAsync(async db =>
        {
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
        });
    }

    public async Task<TenantTemplateViewModel?> CreateAsync(ClaimsPrincipal user, TenantTemplateViewModel input)
    {
        if (!HasPermission("TenantTemplates.Write")) return null;

        return await factory.UseAsync<TenantTemplateViewModel?>(async db =>
        {
            var entity = new TenantTemplate
            {
                Name = input.Name,
                Description = input.Description ?? string.Empty,
                Markup = input.Markup ?? string.Empty
            };
            db.TenantTemplates.Add(entity);
            await db.SaveChangesAsync();
            input.TenantTemplateId = entity.TenantTemplateId;
            return input;
        });
    }

    public async Task<TenantTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, TenantTemplateViewModel input)
    {
        if (!HasPermission("TenantTemplates.Write")) return null;

        return await factory.UseAsync<TenantTemplateViewModel?>(async db =>
        {
            var entity = await db.TenantTemplates.FirstOrDefaultAsync(t => t.TenantTemplateId == input.TenantTemplateId);
            if (entity == null) return null;
            entity.Name = input.Name;
            entity.Description = input.Description ?? string.Empty;
            entity.Markup = input.Markup ?? string.Empty;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int tenantTemplateId)
    {
        if (!HasPermission("TenantTemplates.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.TenantTemplates.FirstOrDefaultAsync(t => t.TenantTemplateId == tenantTemplateId);
            if (entity == null) return false;
            db.TenantTemplates.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }
}
