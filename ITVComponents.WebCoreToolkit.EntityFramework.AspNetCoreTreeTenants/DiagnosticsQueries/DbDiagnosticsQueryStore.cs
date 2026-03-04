using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class AspNetDbDiagnosticsQueryStore<TImpl>: DbDiagnosticsQueryStore<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission,GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig>
    where TImpl:AspNetTreeSecurityContext<TImpl>
    {
        private readonly TImpl dbContext;
        private readonly ISecurityAccessProvider securityAccessProvider;

        public AspNetDbDiagnosticsQueryStore(TImpl dbContext, ISecurityAccessProvider securityAccessProvider):base(dbContext)
        {
            this.dbContext = dbContext;
            this.securityAccessProvider = securityAccessProvider;
        }

        public override DashboardWidgetDefinition GetDashboard(string dashboardName, string targetCulture, int? userDashboardId = null)
        {
            using var acl = securityAccessProvider.CreateForCaller(dbContext, new HierarchyTenantContextSecurityTrustConfig() { HideGlobals =false, ShowAllTenants=false, IncludeParentTree = true});
            return base.GetDashboard(dashboardName, targetCulture, userDashboardId);
        }

        public override DiagnosticsQueryDefinition GetQuery(string queryName)
        {
            using var acl = securityAccessProvider.CreateForCaller(dbContext, new HierarchyTenantContextSecurityTrustConfig { HideGlobals = false, ShowAllTenants = false, IncludeParentTree = true });
            return base.GetQuery(queryName);
        }

        public override DashboardWidgetDefinition[] GetUserWidgets(string userName, string targetCulture)
        {
            using var acl = securityAccessProvider.CreateForCaller(dbContext, new HierarchyTenantContextSecurityTrustConfig { HideGlobals = false, ShowAllTenants = false, IncludeParentTree = true });
            return base.GetUserWidgets(userName, targetCulture);
        }

        public override DashboardWidgetDefinition[] GetWidgetTemplates(string targetCulture)
        {
            using var acl = securityAccessProvider.CreateForCaller(dbContext, new HierarchyTenantContextSecurityTrustConfig { HideGlobals = false, ShowAllTenants = false, IncludeParentTree = true });
            return base.GetWidgetTemplates(targetCulture);
        }

        public override Task<DashboardWidgetDefinition[]> SetUserWidgets(DashboardWidgetDefinition[] widgets, string userName)
        {
            using var acl = securityAccessProvider.CreateForCaller(dbContext, new HierarchyTenantContextSecurityTrustConfig { HideGlobals = false, ShowAllTenants = false, IncludeParentTree = true });
            return base.SetUserWidgets(widgets, userName);
        }
    }
}
