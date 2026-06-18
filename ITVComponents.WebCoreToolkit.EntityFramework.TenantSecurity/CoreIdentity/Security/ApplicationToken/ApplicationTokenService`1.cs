using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ApplicationToken;
using ITVComponents.WebCoreToolkit.Security.ApplicationToken;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Security.ApplicationToken
{
    internal class ApplicationTokenService<TContextImpl>: ApplicationTokenService<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig, TContextImpl> where TContextImpl:AspNetSecurityContext<TContextImpl>
    {
        public ApplicationTokenService(IToolkitContextFactory contextFactory):base(contextFactory)
        {
        }
    }
}
