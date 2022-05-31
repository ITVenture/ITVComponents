using System;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Role;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Navigation
{
    internal class AspNetDbNavigationBuilder<TImpl>: TenantSecurityShared.Navigation.DbNavigationBuilder<string, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, UserWidget, CustomUserProperty, AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter>
    where TImpl:AspNetSecurityContext<TImpl>
    {
        public AspNetDbNavigationBuilder(TImpl securityContext, IServiceProvider services, IPermissionScope permissionScope, IOptions<ToolkitPolicyOptions> options):
            base(securityContext,services, permissionScope, options)
        {
        }
    }
}
