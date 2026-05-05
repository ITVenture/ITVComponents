using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AspNetCoreTreeTenantSecurityUserView.Blazor.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddMudBlazorTreeTenantSecurityUserView(this IServiceCollection services)
        => services.AddMudBlazorTreeTenantSecurityUserView<AspNetTreeSecurityContext>();

    public static IServiceCollection AddMudBlazorTreeTenantSecurityUserView<TContext>(this IServiceCollection services)
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly);
        services.AddScoped<IUserAdminHandler, UserAdminHandler<
            TContext, HierarchyTenant, User, Role, Permission, UserRole, RolePermission,
            HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,
            TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery,
            DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,
            AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature,
            SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter,
            ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission,
            ClientApp, ClientAppPermission, ClientAppUser,
            HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence,
            HierarchyTenantSetting, HierarchyTenantFeatureActivation,
            HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin,
            HierarchyTenantContextSecurityTrustConfig>>();
        return services;
    }

    public static IServiceCollection AddMudBlazorTreeTenantSecurityViews(this IServiceCollection services)
        => services.AddMudBlazorTreeTenantSecurityViews<AspNetTreeSecurityContext>();

    public static IServiceCollection AddMudBlazorTreeTenantSecurityViews<TContext>(this IServiceCollection services)
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        services.AddBlazorRoutingAssembly(typeof(ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Extensions.DependencyInjectionExtensions).Assembly);
        services.AddScoped<ITenantAdminHandler, TenantAdminHandler<
            TContext, HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission,
            HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,
            TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery,
            DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,
            AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature,
            SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter,
            ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission,
            ClientApp, ClientAppPermission, ClientAppUser,
            HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence,
            HierarchyTenantSetting, HierarchyTenantFeatureActivation,
            HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin,
            HierarchyTenantContextSecurityTrustConfig>>();
        services.AddScoped<IRoleAdminHandler, RoleAdminHandler<
            TContext, HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission,
            HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,
            TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery,
            DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,
            AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature,
            SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter,
            ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission,
            ClientApp, ClientAppPermission, ClientAppUser,
            HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence,
            HierarchyTenantSetting, HierarchyTenantFeatureActivation,
            HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin,
            HierarchyTenantContextSecurityTrustConfig>>();
        services.AddScoped<IPermissionAdminHandler, PermissionAdminHandler<
            TContext, HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission,
            HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,
            TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery,
            DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,
            AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature,
            SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter,
            ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission,
            ClientApp, ClientAppPermission, ClientAppUser,
            HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence,
            HierarchyTenantSetting, HierarchyTenantFeatureActivation,
            HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin,
            HierarchyTenantContextSecurityTrustConfig>>();
        return services;
    }
}
