using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddMudBlazorTenantSecurityViews<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
        TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
        TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
        TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
        TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
        TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
        TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
        TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
        TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehaviorOptions)
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
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>, new()
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>, new()
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>, new()
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>, new()
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>, new()
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>, new()
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>, new()
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>, new()
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>, new()
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>, new()
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>, new()
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>, new()
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
        where TWebPluginConstant : WebPluginConstant<TTenant>, new()
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
        where TSequence : Sequence<TTenant>, new()
        where TTenantSetting : TenantSetting<TTenant>, new()
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>, new()
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    {
        partTypeLoadBehaviorOptions ??= new AssemblyPartTypeLoadBehaviorOptions
        {
            DefaultBehavior = TypeRegisterBehavior.Use
        };
        services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehaviorOptions);
        services.AddTSVCoreServices(partTypeLoadBehaviorOptions);

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(TenantAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<ITenantAdminHandler, TenantAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission
                    , TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(RoleAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IRoleAdminHandler, RoleAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission,
                    TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(PermissionAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IPermissionAdminHandler, PermissionAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(PermissionSetAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IPermissionSetAdminHandler, PermissionSetAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(AppTemplateAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IAppTemplateAdminHandler, AppTemplateAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(NavigationAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<INavigationAdminHandler, NavigationAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(DashboardWidgetAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IDashboardWidgetAdminHandler, DashboardWidgetAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(AssetTemplateAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IAssetTemplateAdminHandler, AssetTemplateAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(DiagnosticsQueryAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IDiagnosticsQueryAdminHandler, DiagnosticsQueryAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(ExternalServiceAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IExternalServiceAdminHandler, ExternalServiceAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(FeatureActivationAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IFeatureActivationAdminHandler, FeatureActivationAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(GlobalRoleAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IGlobalRoleAdminHandler, GlobalRoleAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(SequenceAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<ISequenceAdminHandler, SequenceAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(
                typeof(PlugInAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services
                .AddScoped<IPlugInAdminHandler, PlugInAdminHandler<TContext, TTenant, TUserId, TUser, TRole,
                    TPermission, TUserRole, TRolePermission,
                    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
                    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization
                    ,
                    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
                    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
                    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
                    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
                    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();
        }

        return services;
    }

    /// <summary>
    /// Registers handlers that don't depend on the full ISecurityContext generic-arg chain
    /// (AuthenticationType, GlobalSettings) plus a DI alias from TContext to ICoreSystemContext
    /// so those handlers can take ICoreSystemContext via constructor injection.
    /// Idempotent for the alias registration.
    /// </summary>
    public static IServiceCollection AddTSVCoreServices(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehaviorOptions)
    {
        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(AuthenticationTypeAdminHandler)))
        {
            services.AddScoped<IAuthenticationTypeAdminHandler, AuthenticationTypeAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(GlobalSettingsAdminHandler)))
        {
            services.AddScoped<IGlobalSettingsAdminHandler, GlobalSettingsAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(TenantTypeAdminHandler)))
        {
            services.AddScoped<ITenantTypeAdminHandler, TenantTypeAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(FeatureAdminHandler)))
        {
            services.AddScoped<IFeatureAdminHandler, FeatureAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(TenantTemplateAdminHandler)))
        {
            services.AddScoped<ITenantTemplateAdminHandler, TenantTemplateAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(TrustedComponentAdminHandler)))
        {
            services.AddScoped<ITrustedComponentAdminHandler, TrustedComponentAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(HealthScriptAdminHandler)))
        {
            services.AddScoped<IHealthScriptAdminHandler, HealthScriptAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(SystemLogAdminHandler)))
        {
            services.AddScoped<ISystemLogAdminHandler, SystemLogAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(DbResourceAdminHandler)))
        {
            services.AddScoped<IDbResourceAdminHandler, DbResourceAdminHandler>();
        }

        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(ModuleVideoAdminHandler)))
        {
            services.AddScoped<IModuleVideoAdminHandler, ModuleVideoAdminHandler>();
        }

        return services;
    }
}
