using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ConfigurationHandler;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.ConfigurationHandler
{
    public class AspNetCoreTreeSecurityConfigurationHandler<TContext> : SysConfigurationHandler<TContext,
        HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission,
        HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,
        TenantNavigationMenu, DiagnosticsQuery,
        DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam,
        DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath,
        AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter,
        ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp,
        ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant,
        HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting,
        HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig>
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        public AspNetCoreTreeSecurityConfigurationHandler(TContext db, IServiceProvider services = null) : base(db, services)
        {
        }
    }
}
