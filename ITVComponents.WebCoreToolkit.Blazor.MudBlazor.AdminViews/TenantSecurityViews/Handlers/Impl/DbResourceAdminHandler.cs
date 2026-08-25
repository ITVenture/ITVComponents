using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using DbLocale = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Localization;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class DbResourceAdminHandler : IDbResourceAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public DbResourceAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<CultureViewModel>> ListCulturesAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("DbCultures.View", "DbCultures.Write")) return new PagedResult<CultureViewModel>();
        return await factory.UseAsync(async db =>
        {
            var q = db.Cultures.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(c => c.Name.Contains(query.Search.Trim()));
            var total = await q.CountAsync();
            var items = await q.OrderBy(c => c.Name)
                .Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(c => new CultureViewModel { CultureId = c.CultureId, Name = c.Name })
                .ToListAsync();
            return new PagedResult<CultureViewModel> { Items = items, TotalCount = total };
        }, showAllTenants: false);
    }

    public async Task<CultureViewModel?> CreateCultureAsync(ClaimsPrincipal user, CultureViewModel input)
    {
        if (!HasPermission("DbCultures.Write")) return null;
        return await factory.UseAsync<CultureViewModel?>(async db =>
        {
            var entity = new Culture { Name = input.Name };
            db.Cultures.Add(entity);
            await db.SaveChangesAsync();
            input.CultureId = entity.CultureId;
            return input;
        }, showAllTenants: false);
    }

    public async Task<CultureViewModel?> UpdateCultureAsync(ClaimsPrincipal user, CultureViewModel input)
    {
        if (!HasPermission("DbCultures.Write")) return null;
        return await factory.UseAsync<CultureViewModel?>(async db =>
        {
            var entity = await db.Cultures.FirstOrDefaultAsync(c => c.CultureId == input.CultureId);
            if (entity == null) return null;
            entity.Name = input.Name;
            await db.SaveChangesAsync();
            return input;
        }, showAllTenants: false);
    }

    public async Task<bool> DeleteCultureAsync(ClaimsPrincipal user, int cultureId)
    {
        if (!HasPermission("DbCultures.Write")) return false;
        return await factory.UseAsync(async db =>
        {
            var entity = await db.Cultures.FirstOrDefaultAsync(c => c.CultureId == cultureId);
            if (entity == null) return false;
            db.Cultures.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        }, showAllTenants: false);
    }

    public async Task<PagedResult<LocalizationViewModel>> ListLocalizationsAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("DbResources.View", "DbResources.Write")) return new PagedResult<LocalizationViewModel>();
        return await factory.UseAsync(async db =>
        {
            var q = db.Localizations.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(l => l.Identifier.Contains(query.Search.Trim()));
            var total = await q.CountAsync();
            var items = await q.OrderBy(l => l.Identifier)
                .Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(l => new LocalizationViewModel { LocalizationId = l.LocalizationId, Identifier = l.Identifier })
                .ToListAsync();
            return new PagedResult<LocalizationViewModel> { Items = items, TotalCount = total };
        }, showAllTenants: false);
    }

    public async Task<LocalizationViewModel?> CreateLocalizationAsync(ClaimsPrincipal user, LocalizationViewModel input)
    {
        if (!HasPermission("DbResources.Write")) return null;
        return await factory.UseAsync<LocalizationViewModel?>(async db =>
        {
            var entity = new DbLocale { Identifier = input.Identifier };
            db.Localizations.Add(entity);
            await db.SaveChangesAsync();
            input.LocalizationId = entity.LocalizationId;
            return input;
        }, showAllTenants: false);
    }

    public async Task<LocalizationViewModel?> UpdateLocalizationAsync(ClaimsPrincipal user, LocalizationViewModel input)
    {
        if (!HasPermission("DbResources.Write")) return null;
        return await factory.UseAsync<LocalizationViewModel?>(async db =>
        {
            var entity = await db.Localizations.FirstOrDefaultAsync(l => l.LocalizationId == input.LocalizationId);
            if (entity == null) return null;
            entity.Identifier = input.Identifier;
            await db.SaveChangesAsync();
            return input;
        }, showAllTenants: false);
    }

    public async Task<bool> DeleteLocalizationAsync(ClaimsPrincipal user, int localizationId)
    {
        if (!HasPermission("DbResources.Write")) return false;
        return await factory.UseAsync(async db =>
        {
            var entity = await db.Localizations.FirstOrDefaultAsync(l => l.LocalizationId == localizationId);
            if (entity == null) return false;
            db.Localizations.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        }, showAllTenants: false);
    }

    public async Task<PagedResult<LocalizationCultureViewModel>> ListLocalizationCulturesAsync(ClaimsPrincipal user, int localizationId, ListQuery query)
    {
        if (!HasPermission("DbResources.View", "DbResources.Write")) return new PagedResult<LocalizationCultureViewModel>();
        return await factory.UseAsync(async db =>
        {
            var q = from lc in db.LocalizationCultures.AsNoTracking()
                    where lc.LocalizationId == localizationId
                    join c in db.Cultures.AsNoTracking() on lc.CultureId equals c.CultureId
                    select new LocalizationCultureViewModel
                    {
                        LocalizationCultureId = lc.LocalizationCultureId,
                        LocalizationId = lc.LocalizationId,
                        CultureId = lc.CultureId,
                        CultureName = c.Name
                    };
            var total = await q.CountAsync();
            var items = await q.OrderBy(x => x.CultureName)
                .Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
            return new PagedResult<LocalizationCultureViewModel> { Items = items, TotalCount = total };
        }, showAllTenants: false);
    }

    public async Task<LocalizationCultureViewModel?> CreateLocalizationCultureAsync(ClaimsPrincipal user, int localizationId, LocalizationCultureViewModel input)
    {
        if (!HasPermission("DbResources.Write")) return null;
        return await factory.UseAsync<LocalizationCultureViewModel?>(async db =>
        {
            var entity = new LocalizationCulture { LocalizationId = localizationId, CultureId = input.CultureId };
            db.LocalizationCultures.Add(entity);
            await db.SaveChangesAsync();
            input.LocalizationCultureId = entity.LocalizationCultureId;
            input.LocalizationId = localizationId;
            return input;
        }, showAllTenants: false);
    }

    public async Task<bool> DeleteLocalizationCultureAsync(ClaimsPrincipal user, int localizationCultureId)
    {
        if (!HasPermission("DbResources.Write")) return false;
        return await factory.UseAsync(async db =>
        {
            var entity = await db.LocalizationCultures.FirstOrDefaultAsync(c => c.LocalizationCultureId == localizationCultureId);
            if (entity == null) return false;
            db.LocalizationCultures.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        }, showAllTenants: false);
    }

    public async Task<PagedResult<LocalizationStringViewModel>> ListStringsAsync(ClaimsPrincipal user, int localizationCultureId, ListQuery query)
    {
        if (!HasPermission("DbResources.View", "DbResources.Write")) return new PagedResult<LocalizationStringViewModel>();
        return await factory.UseAsync(async db =>
        {
            var q = db.LocalizationCultureStrings.AsNoTracking().Where(s => s.LocalizationCultureId == localizationCultureId);
            if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(s => s.LocalizationKey.Contains(query.Search.Trim()));
            var total = await q.CountAsync();
            var items = await q.OrderBy(s => s.LocalizationKey)
                .Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(s => new LocalizationStringViewModel
                {
                    LocalizationStringId = s.LocalizationStringId,
                    LocalizationCultureId = s.LocalizationCultureId,
                    LocalizationKey = s.LocalizationKey,
                    LocalizationValue = s.LocalizationValue
                })
                .ToListAsync();
            return new PagedResult<LocalizationStringViewModel> { Items = items, TotalCount = total };
        }, showAllTenants: false);
    }

    public async Task<LocalizationStringViewModel?> CreateStringAsync(ClaimsPrincipal user, int localizationCultureId, LocalizationStringViewModel input)
    {
        if (!HasPermission("DbResources.Write")) return null;
        return await factory.UseAsync<LocalizationStringViewModel?>(async db =>
        {
            var entity = new LocalizationString
            {
                LocalizationCultureId = localizationCultureId,
                LocalizationKey = input.LocalizationKey,
                LocalizationValue = input.LocalizationValue
            };
            db.LocalizationCultureStrings.Add(entity);
            await db.SaveChangesAsync();
            input.LocalizationStringId = entity.LocalizationStringId;
            input.LocalizationCultureId = localizationCultureId;
            return input;
        }, showAllTenants: false);
    }

    public async Task<LocalizationStringViewModel?> UpdateStringAsync(ClaimsPrincipal user, LocalizationStringViewModel input)
    {
        if (!HasPermission("DbResources.Write")) return null;
        return await factory.UseAsync<LocalizationStringViewModel?>(async db =>
        {
            var entity = await db.LocalizationCultureStrings.FirstOrDefaultAsync(s => s.LocalizationStringId == input.LocalizationStringId);
            if (entity == null) return null;
            entity.LocalizationKey = input.LocalizationKey;
            entity.LocalizationValue = input.LocalizationValue;
            await db.SaveChangesAsync();
            return input;
        }, showAllTenants: false);
    }

    public async Task<bool> DeleteStringAsync(ClaimsPrincipal user, int localizationStringId)
    {
        if (!HasPermission("DbResources.Write")) return false;
        return await factory.UseAsync(async db =>
        {
            var entity = await db.LocalizationCultureStrings.FirstOrDefaultAsync(s => s.LocalizationStringId == localizationStringId);
            if (entity == null) return false;
            db.LocalizationCultureStrings.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        }, showAllTenants: false);
    }
}
