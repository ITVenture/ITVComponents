using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.Role;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Navigation
{
    internal class AspNetDbNavigationBuilder<TImpl>: Shared.Navigation.DbNavigationBuilder<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig>
    where TImpl:AspNetSecurityContext<TImpl>
    {
        public AspNetDbNavigationBuilder(TImpl securityContext, IServiceProvider services, IPermissionScope permissionScope, IContextUserProvider contextUser, IOptions<ToolkitPolicyOptions> options):
            base(securityContext,services, permissionScope, contextUser, options)
        {
        }
    }
}
