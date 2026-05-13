using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.Extensions.DependencyInjection;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.CustomUserProperty;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DashboardWidget;
using DiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DiagnosticsQuery;
using DiagnosticsQueryParameter = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DiagnosticsQueryParameter;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Role;
using RolePermission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.RolePermission;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Tenant;
using TenantDiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.TenantDiagnosticsQuery;
using TenantNavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.TenantNavigationMenu;

namespace ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddMudBlazorTenantSecurityUserView<TContext>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
        where TContext : AspNetSecurityContext<TContext>
    {
        partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions
        {
            DefaultBehavior = TypeRegisterBehavior.Use
        };

        services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);
        if (partTypeLoadBehavior.ShouldLoadType(
                typeof(UserAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
            services.AddScoped<IUserAdminHandler, UserAdminHandler<
                TContext, Tenant, User, Role, Permission, UserRole, RolePermission,
                TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,
                TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery,
                DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,
                AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature,
                SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter,
                ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission,
                ClientApp, ClientAppPermission, ClientAppUser,
                FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence,
                FlatTenantSetting, FlatTenantFeatureActivation,
                FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin,
                BaseTenantContextSecurityTrustConfig>>();
        }

        return services;
    }
}
