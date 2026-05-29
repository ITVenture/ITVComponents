using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using Microsoft.AspNetCore.Identity;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.CustomUserProperty;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.DashboardWidget;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.Role;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class AspNetDbDiagnosticsQueryStore<TImpl>: DbDiagnosticsQueryStore<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser,RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig>
    where TImpl:AspNetSecurityContext<TImpl>
    {
        public AspNetDbDiagnosticsQueryStore(TImpl dbContext):base(dbContext)
        {
        }
    }
}
