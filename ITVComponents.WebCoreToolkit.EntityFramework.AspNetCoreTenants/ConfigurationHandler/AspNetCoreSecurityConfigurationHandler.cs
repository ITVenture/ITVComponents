using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ConfigurationHandler;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.ConfigurationHandler
{
    public class AspNetCoreSecurityConfigurationHandler<TContext>: SysConfigurationHandler<TContext, Tenant ,string,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole,NavigationMenu,TenantNavigationMenu,DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam, DashboardWidgetLocalization, UserWidget,CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation>
        where TContext:AspNetSecurityContext<TContext>
    {
        public AspNetCoreSecurityConfigurationHandler(TContext db) : base(db)
        {
        }
    }
}
