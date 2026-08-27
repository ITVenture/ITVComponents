using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class AssetTemplateAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IAssetTemplateAdminHandler
    where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
        TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery,
        TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty,
        TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset,
        TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet,
        TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin,
        TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation,
        TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
    where TTenant : Tenant
    where TUser : class
    where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TUserProperty : CustomUserProperty<TUserId, TUser>
    where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
    where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
    where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
    where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
    where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
    where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
    where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginConstant : WebPluginConstant<TTenant>
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : TenantSetting<TTenant>
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
{
    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly IServiceProvider services;

    public AssetTemplateAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services)
    {
        this.dbFactory = dbFactory;
        this.services = services;
    }

    private TContext CreateDb()
    {
        var db = dbFactory.CreateDbContext();
        db.ShowAllTenants = true;
        db.HideGlobals = false;
        return db;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { "Sysadmin" });

    public async Task<PagedResult<AssetTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetTemplateViewModel>();

        using var db = CreateDb();
        var q = db.AssetTemplates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.Name.Contains(s) || t.SystemKey.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(t => t.SystemKey) : q.OrderBy(t => t.SystemKey);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(t => new AssetTemplateViewModel
            {
                AssetTemplateId = t.AssetTemplateId,
                Name = t.Name,
                SystemKey = t.SystemKey,
                FeatureId = t.FeatureId,
                PermissionId = t.PermissionId,
                ArgumentEnforcement = t.ArgumentEnforcement,
                AllowAdHoc = t.AllowAdHoc,
                MaxAdHocMinutes = t.MaxAdHocMinutes,
                ValidityRuleKey = t.ValidityRuleKey
            }).ToListAsync();
        return new PagedResult<AssetTemplateViewModel> { Items = items, TotalCount = total };
    }

    public async Task<AssetTemplateViewModel?> CreateAsync(ClaimsPrincipal user, AssetTemplateViewModel input)
    {
        if (!IsSysAdmin()) return null;
        using var db = CreateDb();
        var entity = new TAssetTemplate
        {
            Name = input.Name,
            SystemKey = input.SystemKey,
            FeatureId = input.FeatureId,
            PermissionId = input.PermissionId,
            ArgumentEnforcement = input.ArgumentEnforcement,
            AllowAdHoc = input.AllowAdHoc,
            MaxAdHocMinutes = input.MaxAdHocMinutes,
            ValidityRuleKey = string.IsNullOrWhiteSpace(input.ValidityRuleKey) ? null : input.ValidityRuleKey
        };
        db.AssetTemplates.Add(entity);
        await db.SaveChangesAsync();
        input.AssetTemplateId = entity.AssetTemplateId;
        return input;
    }

    public async Task<AssetTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, AssetTemplateViewModel input)
    {
        if (!IsSysAdmin()) return null;
        using var db = CreateDb();
        var entity = await db.AssetTemplates.FirstOrDefaultAsync(t => t.AssetTemplateId == input.AssetTemplateId);
        if (entity == null) return null;
        entity.Name = input.Name;
        entity.SystemKey = input.SystemKey;
        entity.FeatureId = input.FeatureId;
        entity.PermissionId = input.PermissionId;
        entity.ArgumentEnforcement = input.ArgumentEnforcement;
        entity.AllowAdHoc = input.AllowAdHoc;
        entity.MaxAdHocMinutes = input.MaxAdHocMinutes;
        entity.ValidityRuleKey = string.IsNullOrWhiteSpace(input.ValidityRuleKey) ? null : input.ValidityRuleKey;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int assetTemplateId)
    {
        if (!IsSysAdmin()) return false;
        using var db = CreateDb();
        var entity = await db.AssetTemplates.FirstOrDefaultAsync(t => t.AssetTemplateId == assetTemplateId);
        if (entity == null) return false;
        // Argumente und Konsumenten verweisen logisch auf die Vorlage, ohne Fremdschluessel - die
        // Datenbank raeumt hier also nicht mit auf. Blieben sie stehen, faenden sie beim naechsten
        // Anlegen einer Vorlage mit derselben Id ploetzlich wieder einen Besitzer.
        db.AssetTemplateArguments.RemoveRange(
            db.AssetTemplateArguments.Where(n => n.AssetTemplateId == assetTemplateId));
        db.AssetTemplateConsumers.RemoveRange(
            db.AssetTemplateConsumers.Where(n => n.AssetTemplateId == assetTemplateId));
        db.AssetTemplates.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AssetTemplatePathViewModel>> ListPathsAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetTemplatePathViewModel>();
        using var db = CreateDb();
        var q = db.AssetTemplatePathFilters.AsNoTracking().Where(p => p.AssetTemplateId == assetTemplateId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(p => p.PathTemplate)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(p => new AssetTemplatePathViewModel
            {
                AssetTemplatePathId = p.AssetTemplatePathId,
                AssetTemplateId = p.AssetTemplateId,
                PathTemplate = p.PathTemplate
            }).ToListAsync();
        return new PagedResult<AssetTemplatePathViewModel> { Items = items, TotalCount = total };
    }

    public async Task<AssetTemplatePathViewModel?> CreatePathAsync(ClaimsPrincipal user, int assetTemplateId, AssetTemplatePathViewModel input)
    {
        if (!IsSysAdmin()) return null;
        using var db = CreateDb();
        var entity = new TAssetTemplatePath { AssetTemplateId = assetTemplateId, PathTemplate = input.PathTemplate };
        db.AssetTemplatePathFilters.Add(entity);
        await db.SaveChangesAsync();
        input.AssetTemplatePathId = entity.AssetTemplatePathId;
        input.AssetTemplateId = assetTemplateId;
        return input;
    }

    public async Task<AssetTemplatePathViewModel?> UpdatePathAsync(ClaimsPrincipal user, AssetTemplatePathViewModel input)
    {
        if (!IsSysAdmin()) return null;
        using var db = CreateDb();
        var entity = await db.AssetTemplatePathFilters.FirstOrDefaultAsync(p => p.AssetTemplatePathId == input.AssetTemplatePathId);
        if (entity == null) return null;
        entity.PathTemplate = input.PathTemplate;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeletePathAsync(ClaimsPrincipal user, int assetTemplatePathId)
    {
        if (!IsSysAdmin()) return false;
        using var db = CreateDb();
        var entity = await db.AssetTemplatePathFilters.FirstOrDefaultAsync(p => p.AssetTemplatePathId == assetTemplatePathId);
        if (entity == null) return false;
        db.AssetTemplatePathFilters.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AssetTemplatePermissionAssignmentViewModel>> ListPermissionsForTemplateAsync(
        ClaimsPrincipal user, int assetTemplateId, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetTemplatePermissionAssignmentViewModel>();

        using var db = CreateDb();
        var assignedIds = await db.AssetTemplateGrants
            .Where(g => g.AssetTemplateId == assetTemplateId)
            .Select(g => g.PermissionId)
            .ToListAsync();
        var assignedSet = new HashSet<int>(assignedIds);

        var q = db.Permissions.AsNoTracking().Where(p => p.TenantId == null);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => p.PermissionName.Contains(s));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(p => p.PermissionName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(p => new AssetTemplatePermissionAssignmentViewModel
            {
                PermissionId = p.PermissionId,
                PermissionName = p.PermissionName,
                Description = p.Description,
                AssetTemplateId = assetTemplateId,
                Assigned = false
            }).ToListAsync();
        foreach (var item in items) item.Assigned = assignedSet.Contains(item.PermissionId);
        return new PagedResult<AssetTemplatePermissionAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetPermissionForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, int permissionId, bool assigned)
    {
        if (!IsSysAdmin()) return false;
        using var db = CreateDb();
        var existing = await db.AssetTemplateGrants.FirstOrDefaultAsync(g =>
            g.AssetTemplateId == assetTemplateId && g.PermissionId == permissionId);

        if (assigned && existing == null)
        {
            db.AssetTemplateGrants.Add(new TAssetTemplateGrant { AssetTemplateId = assetTemplateId, PermissionId = permissionId });
            await db.SaveChangesAsync();
            return true;
        }
        if (!assigned && existing != null)
        {
            db.AssetTemplateGrants.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }
        return true;
    }

    public async Task<PagedResult<AssetTemplateFeatureAssignmentViewModel>> ListFeaturesForTemplateAsync(
        ClaimsPrincipal user, int assetTemplateId, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetTemplateFeatureAssignmentViewModel>();

        using var db = CreateDb();
        var assignedIds = await db.AssetTemplateFeatures
            .Where(f => f.AssetTemplateId == assetTemplateId)
            .Select(f => f.FeatureId)
            .ToListAsync();
        var assignedSet = new HashSet<int>(assignedIds);

        var q = db.Features.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(f => f.FeatureName.Contains(s));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(f => f.FeatureName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(f => new AssetTemplateFeatureAssignmentViewModel
            {
                FeatureId = f.FeatureId,
                FeatureName = f.FeatureName,
                Description = f.FeatureDescription,
                AssetTemplateId = assetTemplateId,
                Assigned = false
            }).ToListAsync();
        foreach (var item in items) item.Assigned = assignedSet.Contains(item.FeatureId);
        return new PagedResult<AssetTemplateFeatureAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetFeatureForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, int featureId, bool assigned)
    {
        if (!IsSysAdmin()) return false;
        using var db = CreateDb();
        var existing = await db.AssetTemplateFeatures.FirstOrDefaultAsync(f =>
            f.AssetTemplateId == assetTemplateId && f.FeatureId == featureId);

        if (assigned && existing == null)
        {
            db.AssetTemplateFeatures.Add(new TAssetTemplateFeature { AssetTemplateId = assetTemplateId, FeatureId = featureId });
            await db.SaveChangesAsync();
            return true;
        }
        if (!assigned && existing != null)
        {
            db.AssetTemplateFeatures.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }
        return true;
    }

    public async Task<PagedResult<AssetTemplateArgumentViewModel>> ListArgumentsAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetTemplateArgumentViewModel>();

        using var db = CreateDb();
        var q = db.AssetTemplateArguments.AsNoTracking().Where(n => n.AssetTemplateId == assetTemplateId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(n => n.SortOrder).Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(n => new AssetTemplateArgumentViewModel
            {
                AssetTemplateArgumentId = n.AssetTemplateArgumentId,
                AssetTemplateId = n.AssetTemplateId,
                ArgumentName = n.ArgumentName,
                ArgumentType = n.ArgumentType,
                Required = n.Required,
                SortOrder = n.SortOrder,
                ResolverKey = n.ResolverKey
            }).ToListAsync();
        return new PagedResult<AssetTemplateArgumentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<AssetTemplateArgumentViewModel?> CreateArgumentAsync(ClaimsPrincipal user, int assetTemplateId, AssetTemplateArgumentViewModel input)
    {
        if (!IsSysAdmin()) return null;
        using var db = CreateDb();
        var next = await db.AssetTemplateArguments.Where(n => n.AssetTemplateId == assetTemplateId)
            .Select(n => (int?)n.SortOrder).MaxAsync() ?? -1;
        var entity = new AssetTemplateArgument
        {
            AssetTemplateId = assetTemplateId,
            ArgumentName = input.ArgumentName,
            ArgumentType = input.ArgumentType,
            Required = input.Required,
            SortOrder = next + 1,
            ResolverKey = string.IsNullOrWhiteSpace(input.ResolverKey) ? null : input.ResolverKey
        };
        db.AssetTemplateArguments.Add(entity);
        await db.SaveChangesAsync();
        input.AssetTemplateArgumentId = entity.AssetTemplateArgumentId;
        input.AssetTemplateId = assetTemplateId;
        input.SortOrder = entity.SortOrder;
        return input;
    }

    public async Task<AssetTemplateArgumentViewModel?> UpdateArgumentAsync(ClaimsPrincipal user, AssetTemplateArgumentViewModel input)
    {
        if (!IsSysAdmin()) return null;
        using var db = CreateDb();
        var entity = await db.AssetTemplateArguments.FirstOrDefaultAsync(n => n.AssetTemplateArgumentId == input.AssetTemplateArgumentId);
        if (entity == null) return null;
        entity.ArgumentName = input.ArgumentName;
        entity.ArgumentType = input.ArgumentType;
        entity.Required = input.Required;
        entity.SortOrder = input.SortOrder;
        entity.ResolverKey = string.IsNullOrWhiteSpace(input.ResolverKey) ? null : input.ResolverKey;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteArgumentAsync(ClaimsPrincipal user, int assetTemplateArgumentId)
    {
        if (!IsSysAdmin()) return false;
        using var db = CreateDb();
        var entity = await db.AssetTemplateArguments.FirstOrDefaultAsync(n => n.AssetTemplateArgumentId == assetTemplateArgumentId);
        if (entity == null) return false;
        db.AssetTemplateArguments.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AssetTemplateConsumerViewModel>> ListConsumersAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetTemplateConsumerViewModel>();

        using var db = CreateDb();
        var q = db.AssetTemplateConsumers.AsNoTracking().Where(n => n.AssetTemplateId == assetTemplateId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(n => n.IsEntryPoint).ThenBy(n => n.DeclarationKey)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(n => new AssetTemplateConsumerViewModel
            {
                AssetTemplateConsumerId = n.AssetTemplateConsumerId,
                AssetTemplateId = n.AssetTemplateId,
                DeclarationKind = n.DeclarationKind,
                DeclarationKey = n.DeclarationKey,
                IsEntryPoint = n.IsEntryPoint,
                KnownToRegistry = db.AssetConsumers.Any(c =>
                    c.DeclarationKind == n.DeclarationKind && c.DeclarationKey == n.DeclarationKey)
            }).ToListAsync();
        return new PagedResult<AssetTemplateConsumerViewModel> { Items = items, TotalCount = total };
    }

    public async Task<AssetTemplateConsumerViewModel?> CreateConsumerAsync(ClaimsPrincipal user, int assetTemplateId, AssetTemplateConsumerViewModel input)
    {
        if (!IsSysAdmin()) return null;
        using var db = CreateDb();
        var entity = new AssetTemplateConsumer
        {
            AssetTemplateId = assetTemplateId,
            DeclarationKind = input.DeclarationKind,
            DeclarationKey = input.DeclarationKey,
            IsEntryPoint = input.IsEntryPoint
        };
        db.AssetTemplateConsumers.Add(entity);
        await db.SaveChangesAsync();
        input.AssetTemplateConsumerId = entity.AssetTemplateConsumerId;
        input.AssetTemplateId = assetTemplateId;
        return input;
    }

    public async Task<bool> DeleteConsumerAsync(ClaimsPrincipal user, int assetTemplateConsumerId)
    {
        if (!IsSysAdmin()) return false;
        using var db = CreateDb();
        var entity = await db.AssetTemplateConsumers.FirstOrDefaultAsync(n => n.AssetTemplateConsumerId == assetTemplateConsumerId);
        if (entity == null) return false;
        db.AssetTemplateConsumers.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AssetConsumerViewModel>> ListKnownConsumersAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetConsumerViewModel>();

        using var db = CreateDb();
        var q = db.AssetConsumers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(n => n.DeclarationKey.Contains(search));
        }

        var total = await q.CountAsync();
        var items = await q.OrderBy(n => n.DeclarationKey).Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(n => new AssetConsumerViewModel
            {
                AssetConsumerId = n.AssetConsumerId,
                DeclarationKind = n.DeclarationKind,
                DeclarationKey = n.DeclarationKey,
                FirstSeenUtc = n.FirstSeenUtc,
                LastSeenUtc = n.LastSeenUtc,
                ArgumentCount = n.Arguments.Count
            }).ToListAsync();
        return new PagedResult<AssetConsumerViewModel> { Items = items, TotalCount = total };
    }

    public async Task<string[]> CheckAsync(ClaimsPrincipal user, int assetTemplateId)
    {
        if (!IsSysAdmin()) return Array.Empty<string>();

        using var db = CreateDb();
        var arguments = await db.AssetTemplateArguments.AsNoTracking()
            .Where(n => n.AssetTemplateId == assetTemplateId).ToArrayAsync();
        if (arguments.Length == 0)
        {
            // Ohne Argumente ist die Vorlage eine reine Pfadfreigabe - so wie der ganze Altbestand.
            return Array.Empty<string>();
        }

        var consumers = await db.AssetTemplateConsumers.AsNoTracking()
            .Where(n => n.AssetTemplateId == assetTemplateId).ToArrayAsync();
        var warnings = new List<string>();
        if (consumers.Length == 0)
        {
            warnings.Add("This template carries arguments but names no endpoint that consumes them. Nothing would confirm them, so a share created from it protects no more than its path patterns.");
            return warnings.ToArray();
        }

        if (!consumers.Any(n => n.IsEntryPoint))
        {
            warnings.Add("No endpoint is marked as the entry point - the sharing dialog will not know which URL to build.");
        }

        foreach (var consumer in consumers)
        {
            var known = await db.AssetConsumers.AsNoTracking().Include(n => n.Arguments)
                .FirstOrDefaultAsync(n => n.DeclarationKind == consumer.DeclarationKind
                                          && n.DeclarationKey == consumer.DeclarationKey);
            if (known == null)
            {
                // Ausdruecklich ein Hinweis und kein Fehler: die Registry kennt nur, was sich schon
                // einmal gemeldet hat. Nach einem Deployment ist das zunaechst wenig.
                warnings.Add($"'{consumer.DeclarationKey}' has never declared itself. That is not necessarily wrong - it may simply be a page nobody has visited since the last restart.");
                continue;
            }

            var missing = arguments.Where(a => a.Required)
                .Where(a => !known.Arguments.Any(k => string.Equals(k.ArgumentName, a.ArgumentName, StringComparison.OrdinalIgnoreCase)))
                .Select(a => a.ArgumentName).ToArray();
            if (missing.Length != 0)
            {
                warnings.Add($"'{consumer.DeclarationKey}' does not understand: {string.Join(", ", missing)}. A share would enter there without ever being confirmed.");
            }
        }

        return warnings.ToArray();
    }
}
