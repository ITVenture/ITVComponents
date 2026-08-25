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

public class FeatureAdminHandler : IFeatureAdminHandler
{
    private readonly ICoreSystemContextFactory factory;
    private readonly IServiceProvider services;

    public FeatureAdminHandler(ICoreSystemContextFactory factory, IServiceProvider services)
    {
        this.factory = factory;
        this.services = services;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<FeatureViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("Features.View", "Features.Write"))
            return new PagedResult<FeatureViewModel>();

        return await factory.UseAsync(async db =>
        {
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
        });
    }

    public async Task<FeatureViewModel?> CreateAsync(ClaimsPrincipal user, FeatureViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<FeatureViewModel?>(async db =>
        {
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
        });
    }

    public async Task<FeatureViewModel?> UpdateAsync(ClaimsPrincipal user, FeatureViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<FeatureViewModel?>(async db =>
        {
            var entity = await db.Features.FirstOrDefaultAsync(f => f.FeatureId == input.FeatureId);
            if (entity == null) return null;
            entity.FeatureName = input.FeatureName;
            entity.FeatureDescription = input.FeatureDescription ?? string.Empty;
            entity.Enabled = input.Enabled;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int featureId)
    {
        if (!HasPermission("Features.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.Features.FirstOrDefaultAsync(f => f.FeatureId == featureId);
            if (entity == null) return false;
            db.Features.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }

    public async Task<PagedResult<TemplateModuleViewModel>> ListTemplateModulesAsync(ClaimsPrincipal user, int featureId, ListQuery query)
    {
        if (!HasPermission("Features.View", "Features.Write"))
            return new PagedResult<TemplateModuleViewModel>();

        return await factory.UseAsync(async db =>
        {
            var q = db.TemplateModules.AsNoTracking().Where(m => m.FeatureId == featureId);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(m => m.TemplateModuleName.Contains(s));
            }
            var total = await q.CountAsync();
            var items = await q.OrderBy(m => m.TemplateModuleName)
                .Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(m => new TemplateModuleViewModel
                {
                    TemplateModuleId = m.TemplateModuleId,
                    TemplateModuleName = m.TemplateModuleName,
                    FeatureId = m.FeatureId
                }).ToListAsync();
            return new PagedResult<TemplateModuleViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<TemplateModuleViewModel?> CreateTemplateModuleAsync(ClaimsPrincipal user, int featureId, TemplateModuleViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<TemplateModuleViewModel?>(async db =>
        {
            var entity = new TemplateModule
            {
                TemplateModuleName = input.TemplateModuleName,
                FeatureId = featureId
            };
            db.TemplateModules.Add(entity);
            await db.SaveChangesAsync();
            input.TemplateModuleId = entity.TemplateModuleId;
            input.FeatureId = featureId;
            return input;
        });
    }

    public async Task<TemplateModuleViewModel?> UpdateTemplateModuleAsync(ClaimsPrincipal user, TemplateModuleViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<TemplateModuleViewModel?>(async db =>
        {
            var entity = await db.TemplateModules.FirstOrDefaultAsync(m => m.TemplateModuleId == input.TemplateModuleId);
            if (entity == null) return null;
            entity.TemplateModuleName = input.TemplateModuleName;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteTemplateModuleAsync(ClaimsPrincipal user, int templateModuleId)
    {
        if (!HasPermission("Features.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.TemplateModules.FirstOrDefaultAsync(m => m.TemplateModuleId == templateModuleId);
            if (entity == null) return false;
            db.TemplateModules.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }

    public async Task<PagedResult<TemplateModuleConfiguratorViewModel>> ListConfiguratorsAsync(ClaimsPrincipal user, int templateModuleId, ListQuery query)
    {
        if (!HasPermission("Features.View", "Features.Write"))
            return new PagedResult<TemplateModuleConfiguratorViewModel>();

        return await factory.UseAsync(async db =>
        {
            var q = db.TemplateModuleConfigurators.AsNoTracking().Where(c => c.TemplateModuleId == templateModuleId);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(c => c.Name.Contains(s));
            }
            var total = await q.CountAsync();
            var items = await q.OrderBy(c => c.Name)
                .Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(c => new TemplateModuleConfiguratorViewModel
                {
                    TemplateModuleConfiguratorId = c.TemplateModuleConfiguratorId,
                    Name = c.Name,
                    CustomConfiguratorView = c.CustomConfiguratorView,
                    ConfiguratorTypeBack = c.ConfiguratorTypeBack,
                    DisplayName = c.DisplayName,
                    TemplateModuleId = c.TemplateModuleId
                }).ToListAsync();
            return new PagedResult<TemplateModuleConfiguratorViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<TemplateModuleConfiguratorViewModel?> CreateConfiguratorAsync(ClaimsPrincipal user, int templateModuleId, TemplateModuleConfiguratorViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<TemplateModuleConfiguratorViewModel?>(async db =>
        {
            var entity = new TemplateModuleConfigurator
            {
                Name = input.Name,
                CustomConfiguratorView = input.CustomConfiguratorView ?? string.Empty,
                ConfiguratorTypeBack = input.ConfiguratorTypeBack,
                DisplayName = input.DisplayName ?? string.Empty,
                TemplateModuleId = templateModuleId
            };
            db.TemplateModuleConfigurators.Add(entity);
            await db.SaveChangesAsync();
            input.TemplateModuleConfiguratorId = entity.TemplateModuleConfiguratorId;
            input.TemplateModuleId = templateModuleId;
            return input;
        });
    }

    public async Task<TemplateModuleConfiguratorViewModel?> UpdateConfiguratorAsync(ClaimsPrincipal user, TemplateModuleConfiguratorViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<TemplateModuleConfiguratorViewModel?>(async db =>
        {
            var entity = await db.TemplateModuleConfigurators.FirstOrDefaultAsync(c => c.TemplateModuleConfiguratorId == input.TemplateModuleConfiguratorId);
            if (entity == null) return null;
            entity.Name = input.Name;
            entity.CustomConfiguratorView = input.CustomConfiguratorView ?? string.Empty;
            entity.ConfiguratorTypeBack = input.ConfiguratorTypeBack;
            entity.DisplayName = input.DisplayName ?? string.Empty;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteConfiguratorAsync(ClaimsPrincipal user, int templateModuleConfiguratorId)
    {
        if (!HasPermission("Features.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.TemplateModuleConfigurators.FirstOrDefaultAsync(c => c.TemplateModuleConfiguratorId == templateModuleConfiguratorId);
            if (entity == null) return false;
            db.TemplateModuleConfigurators.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }

    public async Task<PagedResult<TemplateModuleScriptViewModel>> ListScriptsAsync(ClaimsPrincipal user, int templateModuleId, ListQuery query)
    {
        if (!HasPermission("Features.View", "Features.Write"))
            return new PagedResult<TemplateModuleScriptViewModel>();

        return await factory.UseAsync(async db =>
        {
            var q = db.TemplateModuleScripts.AsNoTracking().Where(s => s.TemplateModuleId == templateModuleId);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(x => x.ScriptFile.Contains(s));
            }
            var total = await q.CountAsync();
            var items = await q.OrderBy(s => s.ScriptFile)
                .Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(s => new TemplateModuleScriptViewModel
                {
                    TemplateModuleScriptId = s.TemplateModuleScriptId,
                    ScriptFile = s.ScriptFile,
                    TemplateModuleId = s.TemplateModuleId
                }).ToListAsync();
            return new PagedResult<TemplateModuleScriptViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<TemplateModuleScriptViewModel?> CreateScriptAsync(ClaimsPrincipal user, int templateModuleId, TemplateModuleScriptViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<TemplateModuleScriptViewModel?>(async db =>
        {
            var entity = new TemplateModuleScript
            {
                ScriptFile = input.ScriptFile,
                TemplateModuleId = templateModuleId
            };
            db.TemplateModuleScripts.Add(entity);
            await db.SaveChangesAsync();
            input.TemplateModuleScriptId = entity.TemplateModuleScriptId;
            input.TemplateModuleId = templateModuleId;
            return input;
        });
    }

    public async Task<TemplateModuleScriptViewModel?> UpdateScriptAsync(ClaimsPrincipal user, TemplateModuleScriptViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<TemplateModuleScriptViewModel?>(async db =>
        {
            var entity = await db.TemplateModuleScripts.FirstOrDefaultAsync(s => s.TemplateModuleScriptId == input.TemplateModuleScriptId);
            if (entity == null) return null;
            entity.ScriptFile = input.ScriptFile;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteScriptAsync(ClaimsPrincipal user, int templateModuleScriptId)
    {
        if (!HasPermission("Features.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.TemplateModuleScripts.FirstOrDefaultAsync(s => s.TemplateModuleScriptId == templateModuleScriptId);
            if (entity == null) return false;
            db.TemplateModuleScripts.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }

    public async Task<PagedResult<TemplateModuleConfiguratorParameterViewModel>> ListConfiguratorParametersAsync(ClaimsPrincipal user, int templateModuleConfiguratorId, ListQuery query)
    {
        if (!HasPermission("Features.View", "Features.Write"))
            return new PagedResult<TemplateModuleConfiguratorParameterViewModel>();

        return await factory.UseAsync(async db =>
        {
            var q = db.TemplateModuleConfiguratorParameters.AsNoTracking()
                .Where(p => p.TemplateModuleConfiguratorId == templateModuleConfiguratorId);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(p => p.ParameterName.Contains(s));
            }
            var total = await q.CountAsync();
            var items = await q.OrderBy(p => p.ParameterName)
                .Skip(query.Page * query.PageSize).Take(query.PageSize)
                .Select(p => new TemplateModuleConfiguratorParameterViewModel
                {
                    TemplateModuleCfgParameterId = p.TemplateModuleCfgParameterId,
                    ParameterName = p.ParameterName,
                    DisplayName = p.DisplayName,
                    ParameterValue = p.ParameterValue,
                    TemplateModuleConfiguratorId = p.TemplateModuleConfiguratorId
                }).ToListAsync();
            return new PagedResult<TemplateModuleConfiguratorParameterViewModel> { Items = items, TotalCount = total };
        });
    }

    public async Task<TemplateModuleConfiguratorParameterViewModel?> CreateConfiguratorParameterAsync(ClaimsPrincipal user, int templateModuleConfiguratorId, TemplateModuleConfiguratorParameterViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<TemplateModuleConfiguratorParameterViewModel?>(async db =>
        {
            var entity = new TemplateModuleConfiguratorParameter
            {
                ParameterName = input.ParameterName,
                DisplayName = input.DisplayName ?? string.Empty,
                ParameterValue = input.ParameterValue,
                TemplateModuleConfiguratorId = templateModuleConfiguratorId
            };
            db.TemplateModuleConfiguratorParameters.Add(entity);
            await db.SaveChangesAsync();
            input.TemplateModuleCfgParameterId = entity.TemplateModuleCfgParameterId;
            input.TemplateModuleConfiguratorId = templateModuleConfiguratorId;
            return input;
        });
    }

    public async Task<TemplateModuleConfiguratorParameterViewModel?> UpdateConfiguratorParameterAsync(ClaimsPrincipal user, TemplateModuleConfiguratorParameterViewModel input)
    {
        if (!HasPermission("Features.Write")) return null;

        return await factory.UseAsync<TemplateModuleConfiguratorParameterViewModel?>(async db =>
        {
            var entity = await db.TemplateModuleConfiguratorParameters.FirstOrDefaultAsync(p => p.TemplateModuleCfgParameterId == input.TemplateModuleCfgParameterId);
            if (entity == null) return null;
            entity.ParameterName = input.ParameterName;
            entity.DisplayName = input.DisplayName ?? string.Empty;
            entity.ParameterValue = input.ParameterValue;
            await db.SaveChangesAsync();
            return input;
        });
    }

    public async Task<bool> DeleteConfiguratorParameterAsync(ClaimsPrincipal user, int templateModuleCfgParameterId)
    {
        if (!HasPermission("Features.Write")) return false;

        return await factory.UseAsync(async db =>
        {
            var entity = await db.TemplateModuleConfiguratorParameters.FirstOrDefaultAsync(p => p.TemplateModuleCfgParameterId == templateModuleCfgParameterId);
            if (entity == null) return false;
            db.TemplateModuleConfiguratorParameters.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        });
    }
}
