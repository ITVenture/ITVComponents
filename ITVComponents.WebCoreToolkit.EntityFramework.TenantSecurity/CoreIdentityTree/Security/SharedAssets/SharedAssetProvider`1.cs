using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security.SharedAssets
{
    public class SharedAssetProvider<TContext>:SharedAssetInfoProvider<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig, TContext>
    where TContext: AspNetTreeSecurityContext<TContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, IToolkitContextFactory contextFactory, ISecurityAccessProvider securityAccessProvider, IServiceProvider services) : base(userNameMapper, securityRepo, contextFactory, securityAccessProvider, services)
        {
        }

        protected override HierarchyTenantContextSecurityTrustConfig ConfigureTrustConfig(HierarchyTenantContextSecurityTrustConfig trustConfig)
        {
            return trustConfig;
        }
    }
}
