using System.Security.Claims;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class GlobalSettingsAdminHandler : IGlobalSettingsAdminHandler
{
    private readonly ICoreSystemContext db;
    private readonly IServiceProvider services;

    public GlobalSettingsAdminHandler(ICoreSystemContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        this.db.ShowAllTenants = true;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<GlobalSettingViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "GlobalSettings.View", "GlobalSettings.Write"))
            return new PagedResult<GlobalSettingViewModel>();

        var q = db.GlobalSettings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(g => g.SettingsKey.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(g => g.SettingsKey) : q.OrderBy(g => g.SettingsKey);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(g => new GlobalSettingViewModel
            {
                GlobalSettingId = g.GlobalSettingId,
                SettingsKey = g.SettingsKey,
                SettingsValue = g.SettingsValue,
                JsonSetting = g.JsonSetting
            }).ToListAsync();
        return new PagedResult<GlobalSettingViewModel> { Items = items, TotalCount = total };
    }

    public async Task<GlobalSettingViewModel?> CreateAsync(ClaimsPrincipal user, GlobalSettingViewModel input)
    {
        if (!HasPermission(user, "GlobalSettings.Write")) return null;
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
    }

    public async Task<GlobalSettingViewModel?> UpdateAsync(ClaimsPrincipal user, GlobalSettingViewModel input)
    {
        if (!HasPermission(user, "GlobalSettings.Write")) return null;
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
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int globalSettingId)
    {
        if (!HasPermission(user, "GlobalSettings.Write")) return false;
        var entity = await db.GlobalSettings.FirstOrDefaultAsync(g => g.GlobalSettingId == globalSettingId);
        if (entity == null) return false;
        db.GlobalSettings.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
