using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.Role;
using User = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.User;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Navigation
{
    internal class DbNavigationBuilder<TImpl>: Shared.Navigation.DbNavigationBuilder<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser,RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig>
    where TImpl:SecurityContext<TImpl>
    {
        public DbNavigationBuilder(TImpl securityContext, IServiceProvider services, IPermissionScope permissionScope, IContextUserProvider contextUser, IOptions<ToolkitPolicyOptions> options):
            base(securityContext,services, permissionScope, contextUser, options)
        {
        }
    }
}
