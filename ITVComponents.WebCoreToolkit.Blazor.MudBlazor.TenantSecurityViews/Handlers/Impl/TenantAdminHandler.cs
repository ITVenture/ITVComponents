using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class TenantAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : ITenantAdminHandler
    where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
        TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery,
        TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty,
        TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset,
        TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet,
        TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin,
        TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation,
        TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
    where TTenant : Tenant, new()
    where TUser : class
    where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
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
    where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
{
    private readonly TContext db;
    private readonly IServiceProvider services;

    public TenantAdminHandler(TContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public AdminContext GetContext(ClaimsPrincipal user)
    {
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(sysAdmin);
        return new AdminContext
        {
            IsSysAdmin = sysAdmin,
            CurrentTenantId = sysAdmin ? null : db.CurrentTenantId
        };
    }

    public async Task<PagedResult<TenantViewModel>> ListTenantsAsync(ClaimsPrincipal user, ListQuery query)
    {
        ApplyContextScope(IsSysAdmin());
        var q = db.Tenants.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.TenantName.Contains(s) || (t.DisplayName != null && t.DisplayName.Contains(s)));
        }
        var total = await q.CountAsync();
        q = (query.SortColumn?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("displayname", true) => q.OrderByDescending(t => t.DisplayName),
            ("displayname", false) => q.OrderBy(t => t.DisplayName),
            (_, true) => q.OrderByDescending(t => t.TenantName),
            _ => q.OrderBy(t => t.TenantName)
        };
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(t => new TenantViewModel
            {
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                DisplayName = t.DisplayName,
                TimeZone = t.TimeZone,
                TenantTypeId = t.TenantTypeId
            }).ToListAsync();
        return new PagedResult<TenantViewModel> { Items = items, TotalCount = total };
    }

    public async Task<TenantViewModel?> CreateTenantAsync(ClaimsPrincipal user, TenantViewModel input)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.Write" })) return null;
        ApplyContextScope(IsSysAdmin());
        var entity = new TTenant
        {
            TenantName = input.TenantName,
            DisplayName = input.DisplayName,
            TimeZone = input.TimeZone,
            TenantTypeId = input.TenantTypeId
        };
        db.Tenants.Add(entity);
        await db.SaveChangesAsync();
        input.TenantId = entity.TenantId;
        return input;
    }

    public async Task<TenantViewModel?> UpdateTenantAsync(ClaimsPrincipal user, TenantViewModel input)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.Write" })) return null;
        ApplyContextScope(IsSysAdmin());
        var entity = await db.Tenants.FirstOrDefaultAsync(n => n.TenantId == input.TenantId);
        if (entity == null) return null;
        entity.TenantName = input.TenantName;
        entity.DisplayName = input.DisplayName;
        entity.TimeZone = input.TimeZone;
        entity.TenantTypeId = input.TenantTypeId;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteTenantAsync(ClaimsPrincipal user, int tenantId)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.Write" })) return false;
        ApplyContextScope(IsSysAdmin());
        var entity = await db.Tenants.FirstOrDefaultAsync(n => n.TenantId == tenantId);
        if (entity == null) return false;
        db.Tenants.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<TenantAssignmentViewModel>> ListTenantAssignmentsForUserAsync(
        ClaimsPrincipal user, string userId, ListQuery query)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.AssignUser", "Tenants.View" }))
            return new PagedResult<TenantAssignmentViewModel>();

        ApplyContextScope(IsSysAdmin());

        var assigned = await db.TenantUsers
            .Where(r => r.UserId.ToString() == userId)
            .Select(r => r.TenantId)
            .ToListAsync();
        var assignedSet = new HashSet<int>(assigned);

        var total = await db.Tenants.CountAsync();
        var items = await db.Tenants.AsNoTracking()
            .OrderBy(t => t.TenantName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(t => new TenantAssignmentViewModel
            {
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                DisplayName = t.DisplayName,
                Assigned = false
            }).ToListAsync();
        foreach (var item in items)
        {
            item.Assigned = assignedSet.Contains(item.TenantId);
        }
        return new PagedResult<TenantAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetUserTenantAssignmentAsync(
        ClaimsPrincipal user, string userId, int tenantId, bool assigned)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.AssignUser" })) return false;
        ApplyContextScope(IsSysAdmin());

        var existing = await db.TenantUsers
            .FirstOrDefaultAsync(n => n.UserId.ToString() == userId && n.TenantId == tenantId);

        if (assigned && existing == null)
        {
            var tu = new TTenantUser { TenantId = tenantId, UserId = ParseUserId(userId) };
            db.TenantUsers.Add(tu);
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.TenantUsers.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }

    private static TUserId ParseUserId(string userId)
    {
        if (typeof(TUserId) == typeof(string))
            return (TUserId)(object)userId;
        if (typeof(TUserId) == typeof(int))
            return (TUserId)(object)int.Parse(userId);
        if (typeof(TUserId) == typeof(Guid))
            return (TUserId)(object)Guid.Parse(userId);
        throw new InvalidOperationException($"Unsupported user-id type {typeof(TUserId).Name}");
    }

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });

    private void ApplyContextScope(bool sysAdmin)
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
