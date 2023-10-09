using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ConfigurationHandler;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.ConfigurationHandler
{
    public class AspNetCoreSecurityConfigurationHandler<TContext>: SysConfigurationHandler<TContext,string,User,Role,Permission,UserRole,RolePermission,TenantUser,NavigationMenu,TenantNavigationMenu,DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam, DashboardWidgetLocalization, UserWidget,CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser>
        where TContext:AspNetSecurityContext<TContext>
    {
        public AspNetCoreSecurityConfigurationHandler(string name, TContext db) : base(name, db)
        {
        }
    }
}
