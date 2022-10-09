using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ConfigurationHandler;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.ConfigurationHandler
{
    public class TenantSecurityConfigurationHandler<TContext>: SysConfigurationHandler<TContext,int,User,Role,Permission,UserRole,RolePermission,TenantUser,NavigationMenu,TenantNavigationMenu,DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam,UserWidget,CustomUserProperty,AssetTemplate,AssetTemplatePath, AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser>
        where TContext:SecurityContext<TContext>
    {
        public TenantSecurityConfigurationHandler(TContext db) : base(db)
        {
        }
    }
}
