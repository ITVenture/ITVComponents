using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

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
    private readonly TContext db;
    private readonly IServiceProvider services;

    public AssetTemplateAdminHandler(TContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        this.db.ShowAllTenants = true;
        this.db.HideGlobals = false;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { "Sysadmin" });

    public async Task<PagedResult<AssetTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetTemplateViewModel>();

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
                PermissionId = t.PermissionId
            }).ToListAsync();
        return new PagedResult<AssetTemplateViewModel> { Items = items, TotalCount = total };
    }

    public async Task<AssetTemplateViewModel?> CreateAsync(ClaimsPrincipal user, AssetTemplateViewModel input)
    {
        if (!IsSysAdmin()) return null;
        var entity = new TAssetTemplate
        {
            Name = input.Name,
            SystemKey = input.SystemKey,
            FeatureId = input.FeatureId,
            PermissionId = input.PermissionId
        };
        db.AssetTemplates.Add(entity);
        await db.SaveChangesAsync();
        input.AssetTemplateId = entity.AssetTemplateId;
        return input;
    }

    public async Task<AssetTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, AssetTemplateViewModel input)
    {
        if (!IsSysAdmin()) return null;
        var entity = await db.AssetTemplates.FirstOrDefaultAsync(t => t.AssetTemplateId == input.AssetTemplateId);
        if (entity == null) return null;
        entity.Name = input.Name;
        entity.SystemKey = input.SystemKey;
        entity.FeatureId = input.FeatureId;
        entity.PermissionId = input.PermissionId;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int assetTemplateId)
    {
        if (!IsSysAdmin()) return false;
        var entity = await db.AssetTemplates.FirstOrDefaultAsync(t => t.AssetTemplateId == assetTemplateId);
        if (entity == null) return false;
        db.AssetTemplates.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AssetTemplatePathViewModel>> ListPathsAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query)
    {
        if (!IsSysAdmin()) return new PagedResult<AssetTemplatePathViewModel>();
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
        var entity = await db.AssetTemplatePathFilters.FirstOrDefaultAsync(p => p.AssetTemplatePathId == input.AssetTemplatePathId);
        if (entity == null) return null;
        entity.PathTemplate = input.PathTemplate;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeletePathAsync(ClaimsPrincipal user, int assetTemplatePathId)
    {
        if (!IsSysAdmin()) return false;
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
}
