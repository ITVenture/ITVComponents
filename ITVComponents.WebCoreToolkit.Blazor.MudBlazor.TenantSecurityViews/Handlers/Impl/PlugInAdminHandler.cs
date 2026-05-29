using System.Security.Claims;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class PlugInAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IPlugInAdminHandler
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
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
    where TWebPluginConstant : WebPluginConstant<TTenant>, new()
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
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

    public PlugInAdminHandler(TContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });

    private void ApplyScope()
    {
        if (IsSysAdmin()) { db.ShowAllTenants = true; db.HideGlobals = false; }
        else { db.HideGlobals = true; }
    }

    private int? ResolveTenant(int? requested) => IsSysAdmin() ? requested : db.CurrentTenantId;

    // ---- Plugins ----

    public async Task<PagedResult<WebPluginViewModel>> ListPluginsAsync(ClaimsPrincipal user, int? tenantId, ListQuery query)
    {
        if (!HasPermission(user, "PlugIns.View", "PlugIns.Write")) return new PagedResult<WebPluginViewModel>();
        ApplyScope();
        var effective = ResolveTenant(tenantId);

        var q = db.WebPlugins.AsNoTracking().Where(n => n.TenantId == effective);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.UniqueName.Contains(s));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(n => n.UniqueName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(n => new WebPluginViewModel
            {
                WebPluginId = n.WebPluginId,
                TenantId = n.TenantId,
                UniqueName = n.UniqueName,
                Constructor = n.Constructor,
                AutoLoad = n.AutoLoad,
                Transient = n.Transient,
                StartupRegistrationConstructor = n.StartupRegistrationConstructor
            })
            .ToListAsync();
        return new PagedResult<WebPluginViewModel> { Items = items, TotalCount = total };
    }

    public async Task<WebPluginViewModel?> CreatePluginAsync(ClaimsPrincipal user, int? tenantId, WebPluginViewModel input)
    {
        if (!HasPermission(user, "PlugIns.Write")) return null;
        ApplyScope();
        var effective = ResolveTenant(tenantId);

        var entity = new TWebPlugin
        {
            TenantId = effective,
            UniqueName = input.UniqueName,
            Constructor = input.Constructor ?? string.Empty,
            AutoLoad = input.AutoLoad,
            Transient = input.Transient,
            StartupRegistrationConstructor = input.StartupRegistrationConstructor ?? string.Empty
        };
        db.WebPlugins.Add(entity);
        await db.SaveChangesAsync();
        input.WebPluginId = entity.WebPluginId;
        input.TenantId = effective;
        return input;
    }

    public async Task<WebPluginViewModel?> UpdatePluginAsync(ClaimsPrincipal user, WebPluginViewModel input)
    {
        if (!HasPermission(user, "PlugIns.Write")) return null;
        ApplyScope();
        var entity = await db.WebPlugins.FirstOrDefaultAsync(n => n.WebPluginId == input.WebPluginId);
        if (entity == null) return null;
        entity.UniqueName = input.UniqueName;
        entity.Constructor = input.Constructor ?? string.Empty;
        entity.AutoLoad = input.AutoLoad;
        entity.Transient = input.Transient;
        entity.StartupRegistrationConstructor = input.StartupRegistrationConstructor ?? string.Empty;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeletePluginAsync(ClaimsPrincipal user, int webPluginId)
    {
        if (!HasPermission(user, "PlugIns.Write")) return false;
        ApplyScope();
        var entity = await db.WebPlugins.FirstOrDefaultAsync(n => n.WebPluginId == webPluginId);
        if (entity == null) return false;
        db.WebPlugins.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    // ---- Plugin Generic Parameters ----

    public async Task<PagedResult<WebPluginGenericParameterViewModel>> ListPluginParametersAsync(ClaimsPrincipal user, int webPluginId, ListQuery query)
    {
        if (!HasPermission(user, "PlugIns.View", "PlugIns.Write")) return new PagedResult<WebPluginGenericParameterViewModel>();
        ApplyScope();

        var q = db.GenericPluginParams.AsNoTracking().Where(n => n.WebPluginId == webPluginId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(n => n.GenericTypeName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(n => new WebPluginGenericParameterViewModel
            {
                WebPluginGenericParameterId = n.WebPluginGenericParameterId,
                WebPluginId = n.WebPluginId,
                GenericTypeName = n.GenericTypeName,
                TypeExpression = n.TypeExpression
            })
            .ToListAsync();
        return new PagedResult<WebPluginGenericParameterViewModel> { Items = items, TotalCount = total };
    }

    public async Task<WebPluginGenericParameterViewModel?> CreatePluginParameterAsync(ClaimsPrincipal user, int webPluginId, WebPluginGenericParameterViewModel input)
    {
        if (!HasPermission(user, "PlugIns.Write")) return null;
        ApplyScope();
        var entity = new TWebPluginGenericParameter
        {
            WebPluginId = webPluginId,
            GenericTypeName = input.GenericTypeName ?? string.Empty,
            TypeExpression = input.TypeExpression ?? string.Empty
        };
        db.GenericPluginParams.Add(entity);
        await db.SaveChangesAsync();
        input.WebPluginGenericParameterId = entity.WebPluginGenericParameterId;
        input.WebPluginId = webPluginId;
        return input;
    }

    public async Task<WebPluginGenericParameterViewModel?> UpdatePluginParameterAsync(ClaimsPrincipal user, WebPluginGenericParameterViewModel input)
    {
        if (!HasPermission(user, "PlugIns.Write")) return null;
        ApplyScope();
        var entity = await db.GenericPluginParams.FirstOrDefaultAsync(n => n.WebPluginGenericParameterId == input.WebPluginGenericParameterId);
        if (entity == null) return null;
        entity.GenericTypeName = input.GenericTypeName ?? string.Empty;
        entity.TypeExpression = input.TypeExpression ?? string.Empty;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeletePluginParameterAsync(ClaimsPrincipal user, int webPluginGenericParameterId)
    {
        if (!HasPermission(user, "PlugIns.Write")) return false;
        ApplyScope();
        var entity = await db.GenericPluginParams.FirstOrDefaultAsync(n => n.WebPluginGenericParameterId == webPluginGenericParameterId);
        if (entity == null) return false;
        db.GenericPluginParams.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    // ---- Plugin Constants ----

    public async Task<PagedResult<WebPluginConstantViewModel>> ListConstantsAsync(ClaimsPrincipal user, int? tenantId, ListQuery query)
    {
        if (!HasPermission(user, "PlugInConstants.View", "PlugInConstants.Write")) return new PagedResult<WebPluginConstantViewModel>();
        ApplyScope();
        var effective = ResolveTenant(tenantId);

        var q = db.WebPluginConstants.AsNoTracking().Where(n => n.TenantId == effective);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.Name.Contains(s));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(n => n.Name)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(n => new WebPluginConstantViewModel
            {
                WebPluginConstantId = n.WebPluginConstantId,
                TenantId = n.TenantId,
                Name = n.Name,
                Value = n.Value
            })
            .ToListAsync();
        return new PagedResult<WebPluginConstantViewModel> { Items = items, TotalCount = total };
    }

    public async Task<WebPluginConstantViewModel?> CreateConstantAsync(ClaimsPrincipal user, int? tenantId, WebPluginConstantViewModel input)
    {
        if (!HasPermission(user, "PlugInConstants.Write")) return null;
        ApplyScope();
        var effective = ResolveTenant(tenantId);

        var value = MaybeEncrypt(input.Value);
        var entity = new TWebPluginConstant
        {
            TenantId = effective,
            Name = input.Name,
            Value = value
        };
        db.WebPluginConstants.Add(entity);
        await db.SaveChangesAsync();
        input.WebPluginConstantId = entity.WebPluginConstantId;
        input.TenantId = effective;
        input.Value = value;
        return input;
    }

    public async Task<WebPluginConstantViewModel?> UpdateConstantAsync(ClaimsPrincipal user, WebPluginConstantViewModel input)
    {
        if (!HasPermission(user, "PlugInConstants.Write")) return null;
        ApplyScope();
        var entity = await db.WebPluginConstants.FirstOrDefaultAsync(n => n.WebPluginConstantId == input.WebPluginConstantId);
        if (entity == null) return null;
        entity.Name = input.Name;
        entity.Value = MaybeEncrypt(input.Value);
        await db.SaveChangesAsync();
        input.Value = entity.Value;
        return input;
    }

    public async Task<bool> DeleteConstantAsync(ClaimsPrincipal user, int webPluginConstantId)
    {
        if (!HasPermission(user, "PlugInConstants.Write")) return false;
        ApplyScope();
        var entity = await db.WebPluginConstants.FirstOrDefaultAsync(n => n.WebPluginConstantId == webPluginConstantId);
        if (entity == null) return false;
        db.WebPluginConstants.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private static string MaybeEncrypt(string raw)
    {
        if (!string.IsNullOrEmpty(raw) && raw.StartsWith("encrypt:"))
        {
            return PasswordSecurity.Encrypt(raw.Substring(8));
        }
        return raw;
    }
}
