using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.Options;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.Role;
using User = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.User;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Navigation
{
    internal class DbNavigationBuilder<TImpl>: TenantSecurityShared.Navigation.DbNavigationBuilder<int, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, UserWidget, CustomUserProperty, AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter>
    where TImpl:SecurityContext<TImpl>
    {
        public DbNavigationBuilder(TImpl securityContext, IServiceProvider services, IPermissionScope permissionScope, IOptions<ToolkitPolicyOptions> options):
            base(securityContext,services, permissionScope, options)
        {
        }
    }
}
