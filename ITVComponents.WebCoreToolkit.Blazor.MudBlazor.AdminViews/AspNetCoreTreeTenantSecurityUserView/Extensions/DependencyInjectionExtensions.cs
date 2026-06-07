using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.Components.Tenants;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTreeTenantSecurityUserView.Extensions;

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
        // The tree security context satisfies every constraint of the generic flat registration
        // (IHierarchySecurityContext : ISecurityContext, and every Hierarchy* model derives from the
        // matching flat base model). So we reuse it verbatim with the Hierarchy* type arguments instead
        // of duplicating handler registrations here. This also pulls in AddBlazorRoutingAssembly +
        // AddTSVCoreServices and every admin handler (Asset/Navigation/Widget/Plugin/...), keeping the
        // tree configuration in sync with the flat one automatically.
        services.AddMudBlazorTenantSecurityViews<
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
            HierarchyTenantContextSecurityTrustConfig>(partTypeLoadBehavior);

        // Hierarchy-specific tenant behavior layered on top of the generic registration above
        // (the flat method registers the handler but not the hierarchy tenant options).
        if (partTypeLoadBehavior.ShouldLoadType(
                typeof(TenantAdminHandler<,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,>)))
        {
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

        return services;
    }
}
