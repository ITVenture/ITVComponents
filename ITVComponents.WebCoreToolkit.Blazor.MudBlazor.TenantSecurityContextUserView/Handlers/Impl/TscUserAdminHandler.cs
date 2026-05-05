using System.Security.Claims;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;
using TscModels = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;

namespace ITVComponents.WebCoreToolkit.TenantSecurityContextUserView.Blazor.Handlers.Impl;

public class TscUserAdminHandler<TContext, TTenant, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IUserAdminHandler
    where TContext : DbContext, ISecurityContext<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission,
        TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery,
        TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty,
        TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset,
        TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet,
        TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin,
        TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation,
        TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
    where TTenant : Tenant
    where TRole : Role<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TPermission : Permission<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TUserRole : UserRole<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TRolePermission : RolePermission<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTenantUser : TenantUser<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    where TNavigationMenu : NavigationMenu<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TTenantNavigation : TenantNavigationMenu<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TQuery : DiagnosticsQuery<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TTenantQuery : TenantDiagnosticsQuery<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TQueryParameter : DiagnosticsQueryParameter<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TWidget : DashboardWidget<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TWidgetParam : DashboardParam<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TWidgetLocalization : DashboardWidgetLocalization<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TUserWidget : UserWidget<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TUserProperty : CustomUserProperty< int, TscModels.User>, new()
    where TAssetTemplate : AssetTemplate<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplatePath : AssetTemplatePath<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplateGrant : AssetTemplateGrant<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplateFeature : AssetTemplateFeature<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TSharedAsset : SharedAsset<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TAppPermission : AppPermission<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
    where TAppPermissionSet : AppPermissionSet<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
    where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    where TClientAppTemplate : ClientAppTemplate<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    where TClientAppPermission : ClientAppPermission<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TClientApp : ClientApp<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TClientAppUser : ClientAppUser<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginConstant : WebPluginConstant<TTenant>
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : TenantSetting<TTenant>
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    where TRoleRole : RoleRole<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    where TGlobalRole : GlobalRole<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGlobalRolePermission : GlobalRolePermission<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGRoleLRole : GRoleLRole<TTenant, int, TscModels.User, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    //where TUser: class
{
    private readonly TContext db;
    private readonly IServiceProvider services;

    public TscUserAdminHandler(TContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public UserListContext GetContext(ClaimsPrincipal user)
    {
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(sysAdmin);
        return new UserListContext
        {
            IsSysAdmin = sysAdmin,
            CurrentTenantId = sysAdmin ? null : db.CurrentTenantId
        };
    }

    public async Task<PagedResult<UserViewModel>> ListUsersAsync(ClaimsPrincipal user, UserListQuery query)
    {
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(sysAdmin);
        var tenantId = sysAdmin ? query.TenantId : db.CurrentTenantId;

        if (tenantId == null && sysAdmin)
        {
            var q = db.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(u => u.UserName.Contains(s));
            }
            q = query.SortDescending ? q.OrderByDescending(u => u.UserName) : q.OrderBy(u => u.UserName);
            var total = await q.CountAsync();
            var page = await q.Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
            return new PagedResult<UserViewModel>
            {
                Items = page.Select(MapUser).ToList(),
                TotalCount = total
            };
        }

        db.HideDisabledUsers = false;
        var tenantQuery = from u in db.Users
                          join tu in db.TenantUsers on u.UserId equals tu.UserId
                          where tu.TenantId == tenantId
                          select new { u, tu };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            tenantQuery = tenantQuery.Where(x => x.u.UserName.Contains(s));
        }

        var totalTenant = await tenantQuery.CountAsync();
        var paged = await tenantQuery
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(x => new UserViewModel
            {
                Id = x.tu.TenantUserId.ToString(),
                UserName = x.u.UserName,
                TenantId = tenantId,
                Enabled = x.tu.Enabled ?? true
            })
            .ToListAsync();

        return new PagedResult<UserViewModel> { Items = paged, TotalCount = totalTenant };
    }

    public async Task<UserViewModel?> CreateUserAsync(ClaimsPrincipal user, UserViewModel input)
    {
        if (!IsSysAdmin()) return null;
        ApplyContextScope(true);
        var entity = new TscModels.User
        {
            UserName = input.UserName
        };
        db.Users.Add(entity);
        await db.SaveChangesAsync();
        return MapUser(entity);
    }

    public async Task<UserViewModel?> UpdateUserAsync(ClaimsPrincipal user, UserViewModel input)
    {
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(sysAdmin);

        var isTenantUser = int.TryParse(input.Id, out var tuid);

        if (input.TenantId == null && sysAdmin && services.VerifyUserPermissions(new[] { "Users.Write" }))
        {
            if (!int.TryParse(input.Id, out var uid)) return null;
            var entity = await db.Users.FirstOrDefaultAsync(n => n.UserId == uid);
            if (entity == null) return null;
            entity.UserName = input.UserName;
            await db.SaveChangesAsync();
            return MapUser(entity);
        }

        if (isTenantUser && services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin }))
        {
            db.HideDisabledUsers = false;
            var tu = await db.TenantUsers.FirstOrDefaultAsync(n => n.TenantUserId == tuid);
            if (tu == null) return null;
            tu.Enabled = input.Enabled;
            await db.SaveChangesAsync();
            return input;
        }

        return null;
    }

    public async Task<bool> DeleteUserAsync(ClaimsPrincipal user, string userOrTenantUserId, int? tenantId)
    {
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(sysAdmin);
        var effectiveTenantId = sysAdmin ? tenantId : db.CurrentTenantId;

        if (effectiveTenantId == null && sysAdmin)
        {
            if (!int.TryParse(userOrTenantUserId, out var uid)) return false;
            var entity = await db.Users.FirstOrDefaultAsync(n => n.UserId == uid);
            if (entity == null) return false;
            db.Users.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        }

        if (int.TryParse(userOrTenantUserId, out var tuid))
        {
            db.HideDisabledUsers = false;
            var tu = await db.TenantUsers.FirstOrDefaultAsync(n => n.TenantUserId == tuid && n.TenantId == effectiveTenantId);
            if (tu == null) return false;
            db.TenantUsers.Remove(tu);
            await db.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<PagedResult<CustomUserPropertyViewModel>> ListPropertiesAsync(ClaimsPrincipal user, string userId, UserListQuery query)
    {
        if (!IsSysAdmin() || !int.TryParse(userId, out var uid)) return Empty<CustomUserPropertyViewModel>();
        ApplyContextScope(true);

        var q = db.UserProperties.AsNoTracking().Where(p => p.UserId == uid);
        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(p => p.PropertyName) : q.OrderBy(p => p.PropertyName);
        var page = await sorted.Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
        return new PagedResult<CustomUserPropertyViewModel>
        {
            Items = page.Select(p => new CustomUserPropertyViewModel
            {
                CustomUserPropertyId = p.CustomUserPropertyId,
                PropertyName = p.PropertyName,
                Value = p.Value,
                PropertyType = p.PropertyType
            }).ToList(),
            TotalCount = total
        };
    }

    public async Task<CustomUserPropertyViewModel?> CreatePropertyAsync(ClaimsPrincipal user, string userId, CustomUserPropertyViewModel input)
    {
        if (!IsSysAdmin() || !int.TryParse(userId, out var uid)) return null;
        ApplyContextScope(true);
        var entity = new TUserProperty
        {
            UserId = uid,
            PropertyName = input.PropertyName,
            Value = input.Value,
            PropertyType = input.PropertyType
        };
        db.UserProperties.Add(entity);
        await db.SaveChangesAsync();
        input.CustomUserPropertyId = entity.CustomUserPropertyId;
        return input;
    }

    public async Task<CustomUserPropertyViewModel?> UpdatePropertyAsync(ClaimsPrincipal user, CustomUserPropertyViewModel input)
    {
        if (!IsSysAdmin()) return null;
        ApplyContextScope(true);
        var entity = await db.UserProperties.FirstOrDefaultAsync(p => p.CustomUserPropertyId == input.CustomUserPropertyId);
        if (entity == null) return null;
        entity.PropertyName = input.PropertyName;
        entity.Value = input.Value;
        entity.PropertyType = input.PropertyType;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeletePropertyAsync(ClaimsPrincipal user, int customUserPropertyId)
    {
        if (!IsSysAdmin()) return false;
        ApplyContextScope(true);
        var entity = await db.UserProperties.FirstOrDefaultAsync(p => p.CustomUserPropertyId == customUserPropertyId);
        if (entity == null) return false;
        db.UserProperties.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public Task<PagedResult<UserLoginViewModel>> ListLoginsAsync(ClaimsPrincipal user, string userId, UserListQuery query)
        => Task.FromResult(Empty<UserLoginViewModel>());

    public Task<bool> DeleteLoginAsync(ClaimsPrincipal user, UserLoginViewModel input) => Task.FromResult(false);

    public Task<PagedResult<UserTokenViewModel>> ListTokensAsync(ClaimsPrincipal user, string userId, UserListQuery query)
        => Task.FromResult(Empty<UserTokenViewModel>());

    public Task<bool> DeleteTokenAsync(ClaimsPrincipal user, UserTokenViewModel input) => Task.FromResult(false);

    public Task<PagedResult<UserClaimViewModel>> ListClaimsAsync(ClaimsPrincipal user, string userId, UserListQuery query)
        => Task.FromResult(Empty<UserClaimViewModel>());

    public Task<UserClaimViewModel?> CreateClaimAsync(ClaimsPrincipal user, string userId, UserClaimViewModel input)
        => Task.FromResult<UserClaimViewModel?>(null);

    public Task<UserClaimViewModel?> UpdateClaimAsync(ClaimsPrincipal user, UserClaimViewModel input)
        => Task.FromResult<UserClaimViewModel?>(null);

    public Task<bool> DeleteClaimAsync(ClaimsPrincipal user, int claimId) => Task.FromResult(false);

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

    private static UserViewModel MapUser(TscModels.User u) => new()
    {
        Id = u.UserId.ToString(),
        UserName = u.UserName,
        AuthenticationTypeId = u.AuthenticationTypeId,
        Enabled = true
    };

    private static PagedResult<T> Empty<T>() => new() { Items = Array.Empty<T>(), TotalCount = 0 };
}
