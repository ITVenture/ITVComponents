using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class RoleAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IRoleAdminHandler
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
    where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
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
    where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
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
    private readonly IOptions<TenantOptions<TTenant>> tenantOptions;

    public RoleAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services, IOptions<TenantOptions<TTenant>> tenantOptions)
    {
        this.dbFactory = dbFactory;
        this.services = services;
        this.tenantOptions = tenantOptions;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public AdminContext GetContext(ClaimsPrincipal user)
    {
        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        return new AdminContext
        {
            IsSysAdmin = sysAdmin,
            CurrentTenantId = db.CurrentTenantId
        };
    }

    public async Task<PagedResult<RoleViewModel>> ListRolesAsync(ClaimsPrincipal user, int tenantId, ListQuery query)
    {
        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        var effectiveTenantId = sysAdmin ? tenantId : db.CurrentTenantId ?? tenantId;

        var q = db.SecurityRoles.AsNoTracking().Where(r => r.TenantId == effectiveTenantId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(r => r.RoleName.Contains(s));
        }

        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(r => r.RoleName) : q.OrderBy(r => r.RoleName);
        var items = await sorted.Page(r => r.RoleId, query)
            .Select(r => new RoleViewModel
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                TenantId = r.TenantId,
                IsSystemRole = r.IsSystemRole,
                Editable = !r.IsSystemRole || sysAdmin
            }).ToListAsync();
        return new PagedResult<RoleViewModel> { Items = items, TotalCount = total };
    }

    public async Task<RoleViewModel?> CreateRoleAsync(ClaimsPrincipal user, int tenantId, RoleViewModel input)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.Write" })) return null;
        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        var effectiveTenantId = sysAdmin ? tenantId : db.CurrentTenantId ?? tenantId;
        if (input.IsSystemRole && !sysAdmin) return null;

        var entity = new TRole
        {
            RoleName = input.RoleName,
            TenantId = effectiveTenantId,
            IsSystemRole = input.IsSystemRole
        };
        db.SecurityRoles.Add(entity);
        await db.SaveChangesAsync();
        input.RoleId = entity.RoleId;
        input.TenantId = effectiveTenantId;
        return input;
    }

    public async Task<RoleViewModel?> UpdateRoleAsync(ClaimsPrincipal user, RoleViewModel input)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.Write" })) return null;
        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        var entity = await db.SecurityRoles.FirstOrDefaultAsync(r =>
            r.RoleId == input.RoleId && r.TenantId == input.TenantId);
        if (entity == null) return null;
        if (entity.IsSystemRole && !sysAdmin) return null;

        entity.RoleName = input.RoleName;
        if (sysAdmin) entity.IsSystemRole = input.IsSystemRole;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteRoleAsync(ClaimsPrincipal user, int roleId)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.Write" })) return false;
        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        var entity = await db.SecurityRoles.FirstOrDefaultAsync(r => r.RoleId == roleId);
        if (entity == null) return false;
        if (entity.IsSystemRole && !sysAdmin) return false;

        db.SecurityRoles.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<RoleAssignmentViewModel>> ListRoleAssignmentsForTenantUserAsync(
        ClaimsPrincipal user, int tenantUserId, int tenantId, ListQuery query)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.AssignUser", "Roles.View" }))
            return new PagedResult<RoleAssignmentViewModel>();

        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        var effectiveTenantId = sysAdmin ? tenantId : db.CurrentTenantId ?? tenantId;

        var assignedRoleIds = await db.TenantUserRoles
            .Where(ur => ur.TenantUserId == tenantUserId && ur.RoleId != null)
            .Select(ur => ur.RoleId!.Value)
            .ToListAsync();
        var assignedSet = new HashSet<int>(assignedRoleIds);

        var q = db.SecurityRoles.AsNoTracking()
            .Where(r => r.TenantId == effectiveTenantId)
            .OrderBy(r => r.RoleName)
            .Page(r => r.RoleId, query)
            .Select(r => new RoleAssignmentViewModel
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                TenantId = r.TenantId,
                TenantUserId = tenantUserId,
                Assigned = false,
                IsSystemRole = r.IsSystemRole
            });
        var items = await q.ToListAsync();
        foreach (var item in items)
        {
            item.Assigned = assignedSet.Contains(item.RoleId);
        }

        var total = await db.SecurityRoles.CountAsync(r => r.TenantId == effectiveTenantId);
        return new PagedResult<RoleAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetRoleAssignmentForTenantUserAsync(
        ClaimsPrincipal user, int tenantUserId, int roleId, int tenantId, bool assigned)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.AssignUser" })) return false;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var existing = await db.TenantUserRoles
            .FirstOrDefaultAsync(n => n.TenantUserId == tenantUserId && n.RoleId == roleId);

        if (assigned && existing == null)
        {
            db.TenantUserRoles.Add(new TUserRole { TenantUserId = tenantUserId, RoleId = roleId });
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.TenantUserRoles.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }

    public async Task<PagedResult<PermissionAssignmentViewModel>> ListPermissionAssignmentsForRoleAsync(
        ClaimsPrincipal user, int roleId, int tenantId, ListQuery query)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.AssignPermission", "Permissions.View" }))
            return new PagedResult<PermissionAssignmentViewModel>();

        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        var effectiveTenantId = sysAdmin ? tenantId : db.CurrentTenantId ?? tenantId;

        var role = await db.SecurityRoles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleId == roleId);
        if (role == null) return new PagedResult<PermissionAssignmentViewModel>();
        if (role.IsSystemRole && !sysAdmin) return new PagedResult<PermissionAssignmentViewModel>();

        var assignedPermissionIds = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId && rp.TenantId == effectiveTenantId && rp.RoleRoleId == null && rp.OriginId == null)
            .Select(rp => rp.PermissionId)
            .ToListAsync();
        var assignedSet = new HashSet<int>(assignedPermissionIds);

        var q = db.Permissions.AsNoTracking()
            .Where(p => p.TenantId == null || p.TenantId == effectiveTenantId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => p.PermissionName.Contains(s));
        }

        var total = await q.CountAsync();
        var items = await q.OrderBy(p => p.PermissionName).Page(p => p.PermissionId, query)
            .Select(p => new PermissionAssignmentViewModel
            {
                PermissionId = p.PermissionId,
                PermissionName = p.PermissionName,
                Description = p.Description,
                RoleId = roleId,
                TenantId = effectiveTenantId,
                IsGlobal = p.TenantId == null,
                Assigned = false
            }).ToListAsync();
        foreach (var item in items)
        {
            item.Assigned = assignedSet.Contains(item.PermissionId);
        }
        return new PagedResult<PermissionAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetPermissionAssignmentForRoleAsync(
        ClaimsPrincipal user, int roleId, int permissionId, int tenantId, bool assigned)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.AssignPermission" })) return false;
        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);

        var role = await db.SecurityRoles.FirstOrDefaultAsync(r => r.RoleId == roleId && r.TenantId == tenantId);
        if (role == null) return false;
        if (role.IsSystemRole && !sysAdmin) return false;

        var existing = await db.RolePermissions.FirstOrDefaultAsync(n =>
            n.PermissionId == permissionId && n.RoleId == roleId && n.TenantId == tenantId && n.OriginId == null && n.RoleRoleId == null);

        if (assigned && existing == null)
        {
            db.RolePermissions.Add(new TRolePermission
            {
                PermissionId = permissionId,
                RoleId = roleId,
                TenantId = tenantId
            });
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.RolePermissions.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }

    public async Task<PagedResult<RoleRoleAssignmentViewModel>> ListPermittedRolesForRoleAsync(
        ClaimsPrincipal user, int permissiveRoleId, int tenantId, ListQuery query)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.AssignRole", "Roles.View" }))
            return new PagedResult<RoleRoleAssignmentViewModel>();

        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        var effectiveTenantId = new[] { sysAdmin ? tenantId : db.CurrentTenantId ?? tenantId };
        IDisposable treeAccessCfg = null;
        var ov = tenantOptions.Value;

        if (ov.UseHierarchy && ov.AddDirectParent != null)
        {
            if (!sysAdmin && ov.ConfigureTree != null)
            {
                var acs = services.GetService<ISecurityAccessProvider>();
                treeAccessCfg = ov.ConfigureTree(db, acs);
            }
            effectiveTenantId = ov.AddDirectParent(db, effectiveTenantId);   // läuft auch für Sysadmin
        }

        try
        {
            var assignedIds = await db.RoleRoles
                .Where(rr => rr.PermissiveRoleId == permissiveRoleId)
                .Select(rr => rr.PermittedRoleId!.Value)
                .ToListAsync();
            var assignedSet = new HashSet<int>(assignedIds);

            var q = db.SecurityRoles.AsNoTracking()
                .Where(r => effectiveTenantId.AsEnumerable().Contains(r.TenantId));
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(r => r.RoleName.Contains(s));
            }

            var total = await q.CountAsync();
            var defaultTn = effectiveTenantId[0];
            var items = await q.OrderBy(r => r.RoleName).Page(r => r.RoleId, query)
                .Select(r => new RoleRoleAssignmentViewModel
                {
                    PermissiveRoleId = permissiveRoleId,
                    PermittedRoleId = r.RoleId,
                    RoleName = r.TenantId == defaultTn? r.RoleName: $"Parent_{r.RoleName}",
                    TenantId = r.TenantId,
                    IsSystemRole = r.IsSystemRole,
                    Assigned = false
                })
                .ToListAsync();

            var filtered = items
                .Where(i => !db.IsCyclicRoleInheritance(permissiveRoleId, i.PermittedRoleId))
                .ToList();
            foreach (var item in filtered)
            {
                item.Assigned = assignedSet.Contains(item.PermittedRoleId);
            }

            return new PagedResult<RoleRoleAssignmentViewModel> { Items = filtered, TotalCount = total };
        }
        finally
        {
            treeAccessCfg?.Dispose();
        }
    }

    public async Task<bool> SetPermittedRoleForRoleAsync(
        ClaimsPrincipal user, int permissiveRoleId, int permittedRoleId, int tenantId, bool assigned)
    {
        if (!services.VerifyUserPermissions(new[] { "Roles.AssignRole" })) return false;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        if (assigned && db.IsCyclicRoleInheritance(permissiveRoleId, permittedRoleId)) return false;

        var existing = await db.RoleRoles.FirstOrDefaultAsync(rr =>
            rr.PermissiveRoleId == permissiveRoleId && rr.PermittedRoleId == permittedRoleId);

        if (assigned && existing == null)
        {
            db.RoleRoles.Add(new TRoleRole
            {
                PermissiveRoleId = permissiveRoleId,
                PermittedRoleId = permittedRoleId
            });
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.RoleRoles.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });

    private void ApplyContextScope(TContext db, bool sysAdmin)
    {
        if (sysAdmin)
        {
            db.HideGlobals = false;
            db.ShowAllTenants = true;
        }
        else
        {
            db.HideGlobals = true;
            db.ShowAllTenants = false;
        }
    }
}
