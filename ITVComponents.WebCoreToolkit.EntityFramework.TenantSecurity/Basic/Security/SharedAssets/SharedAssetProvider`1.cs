using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Security.SharedAssets
{
    public class SharedAssetProvider<TContext>:SharedAssetInfoProvider<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig, TContext>
    where TContext: SecurityContext<TContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, IToolkitContextFactory contextFactory, ISecurityAccessProvider accessProvider, IServiceProvider services) : base(userNameMapper, securityRepo, contextFactory, accessProvider, services)
        {
        }

        protected override BaseTenantContextSecurityTrustConfig ConfigureTrustConfig(BaseTenantContextSecurityTrustConfig trustConfig)
        {
            return trustConfig;
        }
    }
}
