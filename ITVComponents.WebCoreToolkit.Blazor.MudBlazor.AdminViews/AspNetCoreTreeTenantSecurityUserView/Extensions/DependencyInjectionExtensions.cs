using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Components.Tenants;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Options;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AspNetCoreTreeTenantSecurityUserView.Blazor.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddMudBlazorTreeTenantSecurityUserView<TContext>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions
        {
            DefaultBehavior = TypeRegisterBehavior.Use
        };
        services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly);
        if (partTypeLoadBehavior.ShouldLoadType(
                typeof(UserAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
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
                HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState,
                HierarchyExternalOAuthServiceTenantLogin,
                HierarchyTenantContextSecurityTrustConfig>>();
        }

        if (partTypeLoadBehavior.ShouldLoadType(typeof(TenantUsersGrid)))
        {
            services.AddTenantUsersGrid<TenantUsersGrid>();
        }

        return services;
    }

    public static IServiceCollection AddMudBlazorTreeTenantSecurityViews<TContext>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions
        {
            DefaultBehavior = TypeRegisterBehavior.Use
        };
        services.AddBlazorRoutingAssembly(typeof(ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Extensions.DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);
        if (partTypeLoadBehavior.ShouldLoadType(
                typeof(TenantAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
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
                HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState,
                HierarchyExternalOAuthServiceTenantLogin,
                HierarchyTenantContextSecurityTrustConfig>>();
            services.Configure<TenantOptions<HierarchyTenant>>(o =>
            {
                o.UseHierarchy = true;
                o.SelectTenant = tenant => new TenantViewModel
                {
                    TenantTypeId = tenant.TenantTypeId,
                    DisplayName = tenant.DisplayName,
                    ParentTenantId = tenant.ParentTenantId,
                    TenantId = tenant.TenantId,
                    TenantName = tenant.TenantName,
                    TimeZone = tenant.TimeZone
                };

                o.UpdateTenant = (tenant, model) =>
                {
                    tenant.ParentTenantId = model.ParentTenantId;
                    tenant.TenantTypeId = model.TenantTypeId;
                    tenant.DisplayName = model.DisplayName;
                    tenant.TenantName = model.TenantName;
                    tenant.TimeZone = model.TimeZone;
                };
                o.ConfigureTree = (ctx, acs) =>
                {
                    if (ctx is TContext tcx)
                    {
                        return acs.CreateForCaller(tcx, new HierarchyTenantContextSecurityTrustConfig
                        {
                            IncludeParentTree = true
                        });
                    }

                    return null;
                };

                o.AddDirectParent = (ctx, id) =>
                {
                    if (ctx is TContext tcx)
                    {
                        var tn = tcx.Tenants.First(n => id.AsEnumerable().Contains(n.TenantId));
                        if (tn.ParentTenantId != null)
                        {
                            return [tn.TenantId, tn.ParentTenantId.Value];
                        }
                    }

                    return id;
                };
            });
        }

        if (partTypeLoadBehavior.ShouldLoadType(typeof(RoleAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
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
        }

        if (partTypeLoadBehavior.ShouldLoadType(
                typeof(PermissionAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
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
                HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState,
                HierarchyExternalOAuthServiceTenantLogin,
                HierarchyTenantContextSecurityTrustConfig>>();
        }

        return services;
    }
}
