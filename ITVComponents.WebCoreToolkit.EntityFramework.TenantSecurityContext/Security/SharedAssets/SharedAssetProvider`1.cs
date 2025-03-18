using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Security.SharedAssets
{
    public class SharedAssetProvider<TContext>:SharedAssetInfoProvider<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, BaseTenantContextSecurityTrustConfig, TContext>
    where TContext: SecurityContext<TContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, TContext database, IServiceProvider services) : base(userNameMapper, securityRepo, database, services)
        {
        }

        protected override BaseTenantContextSecurityTrustConfig ConfigureTrustConfig(BaseTenantContextSecurityTrustConfig trustConfig)
        {
            return trustConfig;
        }
    }
}
