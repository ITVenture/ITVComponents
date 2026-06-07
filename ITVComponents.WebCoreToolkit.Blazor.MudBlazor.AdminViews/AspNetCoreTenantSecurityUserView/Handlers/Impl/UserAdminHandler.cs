using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.Handlers.Impl;

public class UserAdminHandler<TContext, TTenant, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IUserAdminHandler
    where TContext : DbContext,
        ISecurityContext<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser,
            TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery,
            TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty,
            TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset,
            TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet,
            TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin,
            TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation,
            TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
    where TTenant : Tenant
    where TUser : IdentityUser<string>, new()
    where TRole : Role<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TPermission : Permission<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TUserRole : UserRole<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TRolePermission : RolePermission<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTenantUser : TenantUser<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    where TNavigationMenu : NavigationMenu<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TTenantNavigation : TenantNavigationMenu<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TQuery : DiagnosticsQuery<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TTenantQuery : TenantDiagnosticsQuery<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TQueryParameter : DiagnosticsQueryParameter<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TWidget : DashboardWidget<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TWidgetParam : DashboardParam<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TWidgetLocalization : DashboardWidgetLocalization<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TUserWidget : UserWidget<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TUserProperty : CustomUserProperty<string, TUser>, new()
    where TAssetTemplate : AssetTemplate<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplatePath : AssetTemplatePath<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplateGrant : AssetTemplateGrant<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplateFeature : AssetTemplateFeature<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TSharedAsset : SharedAsset<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TAppPermission : AppPermission<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
    where TAppPermissionSet : AppPermissionSet<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
    where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    where TClientAppTemplate : ClientAppTemplate<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    where TClientAppPermission : ClientAppPermission<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TClientApp : ClientApp<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TClientAppUser : ClientAppUser<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginConstant : WebPluginConstant<TTenant>
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : TenantSetting<TTenant>
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    where TRoleRole : RoleRole<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    where TGlobalRole : GlobalRole<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGlobalRolePermission : GlobalRolePermission<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGRoleLRole : GRoleLRole<TTenant, string, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
{
    private readonly TContext db;
    private readonly IServiceProvider services;

    public UserAdminHandler(TContext db, IServiceProvider services)
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
                q = q.Where(u => u.UserName!.Contains(s) || (u.Email != null && u.Email.Contains(s)));
            }

            q = (query.SortColumn?.ToLowerInvariant(), query.SortDescending) switch
            {
                ("email", false) => q.OrderBy(u => u.Email),
                ("email", true) => q.OrderByDescending(u => u.Email),
                ("username", true) => q.OrderByDescending(u => u.UserName),
                _ => q.OrderBy(u => u.UserName)
            };

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
                          join tu in db.TenantUsers on u.Id equals tu.UserId
                          where tu.TenantId == tenantId
                          select new { u, tu };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            tenantQuery = tenantQuery.Where(x => x.u.UserName!.Contains(s) || (x.u.Email != null && x.u.Email.Contains(s)));
        }

        var totalTenant = await tenantQuery.CountAsync();
        var paged = await tenantQuery
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(x => new UserViewModel
            {
                Id = x.tu.TenantUserId.ToString(),
                UserName = x.u.UserName!,
                TenantId = tenantId,
                Enabled = x.tu.Enabled ?? true,
                Email = x.u.Email,
                EmailConfirmed = x.u.EmailConfirmed,
                NormalizedUserName = x.u.NormalizedUserName,
                NormalizedEmail = x.u.NormalizedEmail,
                PhoneNumber = x.u.PhoneNumber,
                PhoneNumberConfirmed = x.u.PhoneNumberConfirmed,
                TwoFactorEnabled = x.u.TwoFactorEnabled,
                LockoutEnd = x.u.LockoutEnd,
                LockoutEnabled = x.u.LockoutEnabled,
                AccessFailedCount = x.u.AccessFailedCount
            })
            .ToListAsync();

        return new PagedResult<UserViewModel> { Items = paged, TotalCount = totalTenant };
    }

    public async Task<UserViewModel?> CreateUserAsync(ClaimsPrincipal user, UserViewModel input)
    {
        if (!IsSysAdmin()) return null;
        ApplyContextScope(true);

        var entity = new TUser
        {
            UserName = input.UserName,
            Email = input.Email,
            EmailConfirmed = input.EmailConfirmed,
            PhoneNumber = input.PhoneNumber,
            PhoneNumberConfirmed = input.PhoneNumberConfirmed,
            TwoFactorEnabled = input.TwoFactorEnabled,
            LockoutEnd = input.LockoutEnd,
            LockoutEnabled = input.LockoutEnabled,
            AccessFailedCount = input.AccessFailedCount
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
            var entity = await db.Users.FirstOrDefaultAsync(n => n.Id == input.Id);
            if (entity == null) return null;

            entity.UserName = input.UserName;
            entity.Email = input.Email;
            entity.EmailConfirmed = input.EmailConfirmed;
            entity.PhoneNumber = input.PhoneNumber;
            entity.PhoneNumberConfirmed = input.PhoneNumberConfirmed;
            entity.TwoFactorEnabled = input.TwoFactorEnabled;
            entity.LockoutEnd = input.LockoutEnd;
            entity.LockoutEnabled = input.LockoutEnabled;
            entity.AccessFailedCount = input.AccessFailedCount;
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
            var entity = await db.Users.FirstOrDefaultAsync(n => n.Id == userOrTenantUserId);
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
        if (!IsSysAdmin()) return Empty<CustomUserPropertyViewModel>();
        ApplyContextScope(true);

        var q = db.UserProperties.AsNoTracking().Where(p => p.UserId == userId);
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
        if (!IsSysAdmin()) return null;
        ApplyContextScope(true);
        var entity = new TUserProperty
        {
            UserId = userId,
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

    public async Task<PagedResult<UserLoginViewModel>> ListLoginsAsync(ClaimsPrincipal user, string userId, UserListQuery query)
    {
        if (!IsSysAdmin()) return Empty<UserLoginViewModel>();
        ApplyContextScope(true);
        var q = db.Set<IdentityUserLogin<string>>().AsNoTracking().Where(l => l.UserId == userId);
        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(l => l.ProviderDisplayName) : q.OrderBy(l => l.ProviderDisplayName);
        var page = await sorted.Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
        return new PagedResult<UserLoginViewModel>
        {
            Items = page.Select(l => new UserLoginViewModel
            {
                LoginProvider = l.LoginProvider,
                ProviderKey = l.ProviderKey,
                ProviderDisplayName = l.ProviderDisplayName,
                UserId = l.UserId
            }).ToList(),
            TotalCount = total
        };
    }

    public async Task<bool> DeleteLoginAsync(ClaimsPrincipal user, UserLoginViewModel input)
    {
        if (!IsSysAdmin()) return false;
        ApplyContextScope(true);
        var entity = await db.Set<IdentityUserLogin<string>>().FirstOrDefaultAsync(l =>
            l.UserId == input.UserId && l.LoginProvider == input.LoginProvider && l.ProviderKey == input.ProviderKey);
        if (entity == null) return false;
        db.Set<IdentityUserLogin<string>>().Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<UserTokenViewModel>> ListTokensAsync(ClaimsPrincipal user, string userId, UserListQuery query)
    {
        if (!IsSysAdmin()) return Empty<UserTokenViewModel>();
        ApplyContextScope(true);
        var q = db.Set<IdentityUserToken<string>>().AsNoTracking().Where(t => t.UserId == userId);
        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(t => t.Name) : q.OrderBy(t => t.Name);
        var page = await sorted.Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
        return new PagedResult<UserTokenViewModel>
        {
            Items = page.Select(t => new UserTokenViewModel
            {
                UserId = t.UserId,
                LoginProvider = t.LoginProvider,
                Name = t.Name,
                Value = t.Value
            }).ToList(),
            TotalCount = total
        };
    }

    public async Task<bool> DeleteTokenAsync(ClaimsPrincipal user, UserTokenViewModel input)
    {
        if (!IsSysAdmin()) return false;
        ApplyContextScope(true);
        var entity = await db.Set<IdentityUserToken<string>>().FirstOrDefaultAsync(t =>
            t.UserId == input.UserId && t.LoginProvider == input.LoginProvider && t.Name == input.Name);
        if (entity == null) return false;
        db.Set<IdentityUserToken<string>>().Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<UserClaimViewModel>> ListClaimsAsync(ClaimsPrincipal user, string userId, UserListQuery query)
    {
        if (!IsSysAdmin()) return Empty<UserClaimViewModel>();
        ApplyContextScope(true);
        var q = db.Set<IdentityUserClaim<string>>().AsNoTracking().Where(c => c.UserId == userId);
        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(c => c.ClaimType) : q.OrderBy(c => c.ClaimType);
        var page = await sorted.Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
        return new PagedResult<UserClaimViewModel>
        {
            Items = page.Select(c => new UserClaimViewModel
            {
                Id = c.Id,
                UserId = c.UserId,
                ClaimType = c.ClaimType ?? "",
                ClaimValue = c.ClaimValue
            }).ToList(),
            TotalCount = total
        };
    }

    public async Task<UserClaimViewModel?> CreateClaimAsync(ClaimsPrincipal user, string userId, UserClaimViewModel input)
    {
        if (!IsSysAdmin()) return null;
        ApplyContextScope(true);
        var entity = new IdentityUserClaim<string>
        {
            UserId = userId,
            ClaimType = input.ClaimType,
            ClaimValue = input.ClaimValue
        };
        db.Set<IdentityUserClaim<string>>().Add(entity);
        await db.SaveChangesAsync();
        input.Id = entity.Id;
        input.UserId = userId;
        return input;
    }

    public async Task<UserClaimViewModel?> UpdateClaimAsync(ClaimsPrincipal user, UserClaimViewModel input)
    {
        if (!IsSysAdmin()) return null;
        ApplyContextScope(true);
        var entity = await db.Set<IdentityUserClaim<string>>().FirstOrDefaultAsync(c => c.Id == input.Id);
        if (entity == null) return null;
        entity.ClaimType = input.ClaimType;
        entity.ClaimValue = input.ClaimValue;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteClaimAsync(ClaimsPrincipal user, int claimId)
    {
        if (!IsSysAdmin()) return false;
        ApplyContextScope(true);
        var entity = await db.Set<IdentityUserClaim<string>>().FirstOrDefaultAsync(c => c.Id == claimId);
        if (entity == null) return false;
        db.Set<IdentityUserClaim<string>>().Remove(entity);
        await db.SaveChangesAsync();
        return true;
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

    private static UserViewModel MapUser(TUser u) => new()
    {
        Id = u.Id,
        UserName = u.UserName ?? "",
        Email = u.Email,
        EmailConfirmed = u.EmailConfirmed,
        NormalizedUserName = u.NormalizedUserName,
        NormalizedEmail = u.NormalizedEmail,
        PhoneNumber = u.PhoneNumber,
        PhoneNumberConfirmed = u.PhoneNumberConfirmed,
        TwoFactorEnabled = u.TwoFactorEnabled,
        LockoutEnd = u.LockoutEnd,
        LockoutEnabled = u.LockoutEnabled,
        AccessFailedCount = u.AccessFailedCount,
        Enabled = true
    };

    private static PagedResult<T> Empty<T>() => new() { Items = Array.Empty<T>(), TotalCount = 0 };
}
