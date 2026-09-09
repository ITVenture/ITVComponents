using System.Security.Claims;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class GlobalSettingsAdminHandler : IGlobalSettingsAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public GlobalSettingsAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<GlobalSettingViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("GlobalSettings.View", "GlobalSettings.Write"))
            return new PagedResult<GlobalSettingViewModel>();

        return await factory.UseAsync(async db =>
        {
            var q = db.GlobalSettings.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(g => g.SettingsKey.Contains(s));
            }
            var total = await q.CountAsync();
            var sorted = query.SortDescending ? q.OrderByDescending(g => g.SettingsKey) : q.OrderBy(g => g.SettingsKey);
            var items = await sorted.Page(g => g.GlobalSettingId, query)
                .Select(g => new GlobalSettingViewModel
                {
                    GlobalSettingId = g.GlobalSettingId,
                    SettingsKey = g.SettingsKey,
                    SettingsValue = g.SettingsValue,
                    JsonSetting = g.JsonSetting
                }).ToListAsync();
            return new PagedResult<GlobalSettingViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<GlobalSettingViewModel?> CreateAsync(ClaimsPrincipal user, GlobalSettingViewModel input)
    {
        if (!HasPermission("GlobalSettings.Write")) return null;

        return await factory.UseAsync<GlobalSettingViewModel?>(async db =>
        {
            var settingsVal = input.SettingsValue;
            if (!string.IsNullOrEmpty(settingsVal) && input.JsonSetting)
            {
                settingsVal = settingsVal.EncryptJsonValues();
            }

            var entity = new GlobalSetting
            {
                SettingsKey = input.SettingsKey,
                SettingsValue = settingsVal,
                JsonSetting = input.JsonSetting
            };

            db.GlobalSettings.Add(entity);
            await db.SaveChangesAsync();
            input.GlobalSettingId = entity.GlobalSettingId;
            return input;
        });
    }

    public async Task<GlobalSettingViewModel?> UpdateAsync(ClaimsPrincipal user, GlobalSettingViewModel input)
    {
        if (!HasPermission("GlobalSettings.Write")) return null;

        return await factory.UseAsync<GlobalSettingViewModel?>(async db =>
        {
            var entity = await db.GlobalSettings.FirstOrDefaultAsync(g => g.GlobalSettingId == input.GlobalSettingId);
            if (entity == null) return null;
            var settingsVal = input.SettingsValue;
            if (!string.IsNullOrEmpty(settingsVal) && input.JsonSetting)
            {
                settingsVal = settingsVal.EncryptJsonValues();
            }

            entity.SettingsKey = input.SettingsKey;
            entity.SettingsValue = settingsVal;
            entity.JsonSetting = input.JsonSetting;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int globalSettingId)
    {
        if (!HasPermission("GlobalSettings.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.GlobalSettings.FirstOrDefaultAsync(g => g.GlobalSettingId == globalSettingId);
            if (entity == null) return false;
            db.GlobalSettings.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }
}
