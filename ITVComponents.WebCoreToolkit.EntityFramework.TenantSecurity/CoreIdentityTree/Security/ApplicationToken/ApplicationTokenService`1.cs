using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ApplicationToken;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security.ApplicationToken
{
    internal class ApplicationTokenService<TContextImpl>: ApplicationTokenService<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig, TContextImpl> where TContextImpl:AspNetTreeSecurityContext<TContextImpl>
    {
        public ApplicationTokenService(TContextImpl dbContext):base(dbContext)
        {
        }
    }
}
