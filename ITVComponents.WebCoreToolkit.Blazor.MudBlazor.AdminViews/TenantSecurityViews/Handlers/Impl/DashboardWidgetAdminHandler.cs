using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class DashboardWidgetAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IDashboardWidgetAdminHandler
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
    where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>, new()
    where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>, new()
    where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>, new()
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

    public DashboardWidgetAdminHandler(TContext db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        this.db.ShowAllTenants = true;
        this.db.HideGlobals = false;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<DashboardWidgetViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "DashboardWidgets.View", "DashboardWidgets.Write"))
            return new PagedResult<DashboardWidgetViewModel>();

        var q = db.Widgets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(w => w.SystemName.Contains(s) || (w.DisplayName != null && w.DisplayName.Contains(s)));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(w => w.SystemName) : q.OrderBy(w => w.SystemName);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(w => new DashboardWidgetViewModel
            {
                DashboardWidgetId = w.DashboardWidgetId,
                DisplayName = w.DisplayName,
                TitleTemplate = w.TitleTemplate,
                SystemName = w.SystemName,
                DiagnosticsQueryId = w.DiagnosticsQueryId,
                Area = w.Area,
                CustomQueryString = w.CustomQueryString,
                Template = w.Template
            }).ToListAsync();
        return new PagedResult<DashboardWidgetViewModel> { Items = items, TotalCount = total };
    }

    public async Task<DashboardWidgetViewModel?> CreateAsync(ClaimsPrincipal user, DashboardWidgetViewModel input)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return null;
        var entity = new TWidget
        {
            DisplayName = input.DisplayName ?? string.Empty,
            TitleTemplate = input.TitleTemplate ?? string.Empty,
            SystemName = input.SystemName,
            DiagnosticsQueryId = input.DiagnosticsQueryId,
            Area = input.Area ?? string.Empty,
            CustomQueryString = input.CustomQueryString ?? string.Empty,
            Template = input.Template ?? string.Empty
        };
        db.Widgets.Add(entity);
        await db.SaveChangesAsync();
        input.DashboardWidgetId = entity.DashboardWidgetId;
        return input;
    }

    public async Task<DashboardWidgetViewModel?> UpdateAsync(ClaimsPrincipal user, DashboardWidgetViewModel input)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return null;
        var entity = await db.Widgets.FirstOrDefaultAsync(w => w.DashboardWidgetId == input.DashboardWidgetId);
        if (entity == null) return null;
        entity.DisplayName = input.DisplayName ?? string.Empty;
        entity.TitleTemplate = input.TitleTemplate ?? string.Empty;
        entity.SystemName = input.SystemName;
        entity.DiagnosticsQueryId = input.DiagnosticsQueryId;
        entity.Area = input.Area ?? string.Empty;
        entity.CustomQueryString = input.CustomQueryString ?? string.Empty;
        entity.Template = input.Template ?? string.Empty;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int dashboardWidgetId)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return false;
        var entity = await db.Widgets.FirstOrDefaultAsync(w => w.DashboardWidgetId == dashboardWidgetId);
        if (entity == null) return false;
        db.Widgets.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<DashboardParamViewModel>> ListParamsAsync(ClaimsPrincipal user, int dashboardWidgetId, ListQuery query)
    {
        if (!HasPermission(user, "DashboardWidgets.View", "DashboardWidgets.Write"))
            return new PagedResult<DashboardParamViewModel>();

        var q = db.WidgetParams.AsNoTracking().Where(p => p.DashboardWidgetId == dashboardWidgetId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(p => p.ParameterName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(p => new DashboardParamViewModel
            {
                DashboardParamId = p.DashboardParamId,
                DashboardWidgetId = p.DashboardWidgetId,
                ParameterName = p.ParameterName,
                InputType = p.InputType,
                InputConfig = p.InputConfig
            }).ToListAsync();
        return new PagedResult<DashboardParamViewModel> { Items = items, TotalCount = total };
    }

    public async Task<DashboardParamViewModel?> CreateParamAsync(ClaimsPrincipal user, int dashboardWidgetId, DashboardParamViewModel input)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return null;
        var entity = new TWidgetParam
        {
            DashboardWidgetId = dashboardWidgetId,
            ParameterName = input.ParameterName,
            InputType = input.InputType,
            InputConfig = input.InputConfig ?? string.Empty
        };
        db.WidgetParams.Add(entity);
        await db.SaveChangesAsync();
        input.DashboardParamId = entity.DashboardParamId;
        input.DashboardWidgetId = dashboardWidgetId;
        return input;
    }

    public async Task<DashboardParamViewModel?> UpdateParamAsync(ClaimsPrincipal user, DashboardParamViewModel input)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return null;
        var entity = await db.WidgetParams.FirstOrDefaultAsync(p => p.DashboardParamId == input.DashboardParamId);
        if (entity == null) return null;
        entity.ParameterName = input.ParameterName;
        entity.InputType = input.InputType;
        entity.InputConfig = input.InputConfig ?? string.Empty;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteParamAsync(ClaimsPrincipal user, int dashboardParamId)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return false;
        var entity = await db.WidgetParams.FirstOrDefaultAsync(p => p.DashboardParamId == dashboardParamId);
        if (entity == null) return false;
        db.WidgetParams.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<DashboardWidgetLocalizationViewModel>> ListLocalesAsync(ClaimsPrincipal user, int dashboardWidgetId, ListQuery query)
    {
        if (!HasPermission(user, "DashboardWidgets.View", "DashboardWidgets.Write"))
            return new PagedResult<DashboardWidgetLocalizationViewModel>();

        var q = db.WidgetLocales.AsNoTracking().Where(l => l.DashboardWidgetId == dashboardWidgetId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(l => l.LocaleName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(l => new DashboardWidgetLocalizationViewModel
            {
                DashboardWidgetLocalizationId = l.DashboardWidgetLocalizationId,
                DashboardWidgetId = l.DashboardWidgetId,
                LocaleName = l.LocaleName,
                DisplayName = l.DisplayName,
                TitleTemplate = l.TitleTemplate,
                Template = l.Template
            }).ToListAsync();
        return new PagedResult<DashboardWidgetLocalizationViewModel> { Items = items, TotalCount = total };
    }

    public async Task<DashboardWidgetLocalizationViewModel?> CreateLocaleAsync(ClaimsPrincipal user, int dashboardWidgetId, DashboardWidgetLocalizationViewModel input)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return null;
        var entity = new TWidgetLocalization
        {
            DashboardWidgetId = dashboardWidgetId,
            LocaleName = input.LocaleName,
            DisplayName = input.DisplayName,
            TitleTemplate = input.TitleTemplate,
            Template = input.Template
        };
        db.WidgetLocales.Add(entity);
        await db.SaveChangesAsync();
        input.DashboardWidgetLocalizationId = entity.DashboardWidgetLocalizationId;
        input.DashboardWidgetId = dashboardWidgetId;
        return input;
    }

    public async Task<DashboardWidgetLocalizationViewModel?> UpdateLocaleAsync(ClaimsPrincipal user, DashboardWidgetLocalizationViewModel input)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return null;
        var entity = await db.WidgetLocales.FirstOrDefaultAsync(l => l.DashboardWidgetLocalizationId == input.DashboardWidgetLocalizationId);
        if (entity == null) return null;
        entity.LocaleName = input.LocaleName;
        entity.DisplayName = input.DisplayName;
        entity.TitleTemplate = input.TitleTemplate;
        entity.Template = input.Template;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteLocaleAsync(ClaimsPrincipal user, int dashboardWidgetLocalizationId)
    {
        if (!HasPermission(user, "DashboardWidgets.Write")) return false;
        var entity = await db.WidgetLocales.FirstOrDefaultAsync(l => l.DashboardWidgetLocalizationId == dashboardWidgetLocalizationId);
        if (entity == null) return false;
        db.WidgetLocales.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
