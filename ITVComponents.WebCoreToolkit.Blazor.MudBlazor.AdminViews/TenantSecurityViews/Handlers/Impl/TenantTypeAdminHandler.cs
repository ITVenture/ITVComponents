using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class TenantTypeAdminHandler : ITenantTypeAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public TenantTypeAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<TenantTypeViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "TenantTypes.View", "TenantTypes.Write"))
            return new PagedResult<TenantTypeViewModel>();

        return await factory.UseAsync(async db =>
        {
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
        });
    }

    public async Task<TenantTypeViewModel?> CreateAsync(ClaimsPrincipal user, TenantTypeViewModel input)
    {
        if (!HasPermission(user, "TenantTypes.Write")) return null;

        return await factory.UseAsync<TenantTypeViewModel?>(async db =>
        {
            var entity = new TenantType
            {
                TenantTypeName = input.TenantTypeName,
                TypeMetaData = input.TypeMetaData,
                TenantTemplateId = input.TenantTemplateId
            };
            db.TenantTypes.Add(entity);
            await db.SaveChangesAsync();
            input.TenantTypeId = entity.TenantTypeId;
            return input;
        });
    }

    public async Task<TenantTypeViewModel?> UpdateAsync(ClaimsPrincipal user, TenantTypeViewModel input)
    {
        if (!HasPermission(user, "TenantTypes.Write")) return null;

        return await factory.UseAsync<TenantTypeViewModel?>(async db =>
        {
            var entity = await db.TenantTypes.FirstOrDefaultAsync(t => t.TenantTypeId == input.TenantTypeId);
            if (entity == null) return null;
            entity.TenantTypeName = input.TenantTypeName;
            entity.TypeMetaData = input.TypeMetaData;
            entity.TenantTemplateId = input.TenantTemplateId;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int tenantTypeId)
    {
        if (!HasPermission(user, "TenantTypes.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.TenantTypes.FirstOrDefaultAsync(t => t.TenantTypeId == tenantTypeId);
            if (entity == null) return false;
            db.TenantTypes.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }
}
