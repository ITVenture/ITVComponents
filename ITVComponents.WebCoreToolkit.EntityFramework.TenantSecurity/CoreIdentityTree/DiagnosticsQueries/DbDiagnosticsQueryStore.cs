using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class AspNetDbDiagnosticsQueryStore<TImpl>: DbDiagnosticsQueryStore<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission,GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig>
    where TImpl:AspNetTreeSecurityContext<TImpl>
    {
        private readonly ISecurityAccessProvider securityAccessProvider;

        public AspNetDbDiagnosticsQueryStore(IToolkitContextFactory contextFactory, ISecurityAccessProvider securityAccessProvider):base(contextFactory)
        {
            this.securityAccessProvider = securityAccessProvider;
        }

        /// <summary>
        /// Applies the hierarchy trust-configuration (parent-tree included, globals visible, no all-tenants elevation)
        /// to the freshly-leased per-operation context the base will read from. Previously each public method wrapped
        /// its <c>base.*</c> call with a <c>CreateForCaller</c> scope on the shared context; with the per-operation
        /// factory the trust is applied here, on the exact leased instance, for every operation alike.
        /// </summary>
        protected override IDisposable AcquireScope(ISecurityContext<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig> dbContext)
        {
            return securityAccessProvider.CreateForCaller(dbContext, new HierarchyTenantContextSecurityTrustConfig { HideGlobals = false, ShowAllTenants = false, IncludeParentTree = true });
        }
    }
}
