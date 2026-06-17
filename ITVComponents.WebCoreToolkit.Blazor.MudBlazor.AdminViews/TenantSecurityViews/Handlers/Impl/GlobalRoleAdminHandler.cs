using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class GlobalRoleAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IGlobalRoleAdminHandler
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
    where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
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
    where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
{
    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly IServiceProvider services;

    public GlobalRoleAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services)
    {
        this.dbFactory = dbFactory;
        this.services = services;
    }

    private TContext CreateDb()
    {
        var db = dbFactory.CreateDbContext();
        db.HideGlobals = false;
        db.ShowAllTenants = true;
        return db;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });
    private bool CanWrite() => services.VerifyUserPermissions(new[] { "GlobalRoles.Write", ToolkitPermission.Sysadmin });
    private bool CanAssignPermission() => services.VerifyUserPermissions(new[] { "GlobalRoles.AssignPermission", ToolkitPermission.Sysadmin });
    private bool CanAssignRole() => services.VerifyUserPermissions(new[] { "GlobalRoles.AssignRole", ToolkitPermission.Sysadmin });

    public async Task<PagedResult<GlobalRoleViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "GlobalRoles.View", "GlobalRoles.Write", ToolkitPermission.Sysadmin))
            return new PagedResult<GlobalRoleViewModel>();

        using var db = CreateDb();
        var q = db.GlobalRoles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(r => r.RoleName.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(r => r.RoleName) : q.OrderBy(r => r.RoleName);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(r => new GlobalRoleViewModel
            {
                GlobalRoleId = r.GlobalRoleId,
                RoleName = r.RoleName,
                RoleDescription = r.RoleDescription
            }).ToListAsync();
        return new PagedResult<GlobalRoleViewModel> { Items = items, TotalCount = total };
    }

    public async Task<GlobalRoleViewModel?> CreateAsync(ClaimsPrincipal user, GlobalRoleViewModel input)
    {
        if (!CanWrite()) return null;
        using var db = CreateDb();
        var entity = new TGlobalRole
        {
            RoleName = input.RoleName,
            RoleDescription = input.RoleDescription
        };
        db.GlobalRoles.Add(entity);
        await db.SaveChangesAsync();
        input.GlobalRoleId = entity.GlobalRoleId;
        return input;
    }

    public async Task<GlobalRoleViewModel?> UpdateAsync(ClaimsPrincipal user, GlobalRoleViewModel input)
    {
        if (!CanWrite()) return null;
        using var db = CreateDb();
        var entity = await db.GlobalRoles.FirstOrDefaultAsync(r => r.GlobalRoleId == input.GlobalRoleId);
        if (entity == null) return null;
        entity.RoleName = input.RoleName;
        entity.RoleDescription = input.RoleDescription;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int globalRoleId)
    {
        if (!CanWrite()) return false;
        using var db = CreateDb();
        var entity = await db.GlobalRoles.FirstOrDefaultAsync(r => r.GlobalRoleId == globalRoleId);
        if (entity == null) return false;
        db.GlobalRoles.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<GlobalPermissionAssignmentViewModel>> ListPermissionsForGlobalRoleAsync(
        ClaimsPrincipal user, int globalRoleId, ListQuery query)
    {
        if (!HasPermission(user, "GlobalRoles.View", "GlobalRoles.AssignPermission", ToolkitPermission.Sysadmin))
            return new PagedResult<GlobalPermissionAssignmentViewModel>();

        using var db = CreateDb();
        var assignedIds = await db.GlobalRolePermissions
            .Where(p => p.GlobalRoleId == globalRoleId)
            .Select(p => p.PermissionId)
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
            .Select(p => new GlobalPermissionAssignmentViewModel
            {
                GlobalRoleId = globalRoleId,
                PermissionId = p.PermissionId,
                PermissionName = p.PermissionName,
                Description = p.Description,
                Assigned = false
            })
            .ToListAsync();
        foreach (var item in items) item.Assigned = assignedSet.Contains(item.PermissionId);
        return new PagedResult<GlobalPermissionAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetPermissionAssignmentForGlobalRoleAsync(
        ClaimsPrincipal user, int globalRoleId, int permissionId, bool assigned)
    {
        if (!CanAssignPermission()) return false;
        using var db = CreateDb();
        var existing = await db.GlobalRolePermissions.FirstOrDefaultAsync(p =>
            p.GlobalRoleId == globalRoleId && p.PermissionId == permissionId);

        if (assigned && existing == null)
        {
            db.GlobalRolePermissions.Add(new TGlobalRolePermission
            {
                GlobalRoleId = globalRoleId,
                PermissionId = permissionId
            });
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.GlobalRolePermissions.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }

    public async Task<PagedResult<GlobalRoleForLocalRoleAssignmentViewModel>> ListGlobalRolesForLocalRoleAsync(
        ClaimsPrincipal user, int localRoleId, ListQuery query)
    {
        if (!HasPermission(user, "GlobalRoles.View", "GlobalRoles.AssignRole", ToolkitPermission.Sysadmin))
            return new PagedResult<GlobalRoleForLocalRoleAssignmentViewModel>();

        using var db = CreateDb();
        var assignedIds = await db.GlobalToLocalRoles
            .Where(rr => rr.LocalRoleId == localRoleId && rr.OriginId == null && rr.RoleRoleId == null)
            .Select(rr => rr.GlobalRoleId)
            .ToListAsync();
        var assignedSet = new HashSet<int>(assignedIds);

        var q = db.GlobalRoles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(g => g.RoleName.Contains(s));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(g => g.RoleName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(g => new GlobalRoleForLocalRoleAssignmentViewModel
            {
                GlobalRoleId = g.GlobalRoleId,
                LocalRoleId = localRoleId,
                RoleName = g.RoleName,
                Assigned = false
            })
            .ToListAsync();
        foreach (var item in items) item.Assigned = assignedSet.Contains(item.GlobalRoleId);
        return new PagedResult<GlobalRoleForLocalRoleAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetGlobalRoleForLocalRoleAsync(
        ClaimsPrincipal user, int globalRoleId, int localRoleId, bool assigned)
    {
        if (!CanAssignRole()) return false;
        using var db = CreateDb();
        var existing = await db.GlobalToLocalRoles.FirstOrDefaultAsync(rr =>
            rr.GlobalRoleId == globalRoleId && rr.LocalRoleId == localRoleId
            && rr.OriginId == null && rr.RoleRoleId == null);

        if (assigned && existing == null)
        {
            db.GlobalToLocalRoles.Add(new TGRoleLRole
            {
                GlobalRoleId = globalRoleId,
                LocalRoleId = localRoleId
            });
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.GlobalToLocalRoles.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }
}
