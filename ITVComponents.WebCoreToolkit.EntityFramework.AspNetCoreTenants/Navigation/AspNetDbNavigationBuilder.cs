using System;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Role;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Navigation
{
    internal class AspNetDbNavigationBuilder: TenantSecurityShared.Navigation.DbNavigationBuilder<string, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, UserWidget, CustomUserProperty>
    {
        public AspNetDbNavigationBuilder(AspNetSecurityContext securityContext, IServiceProvider services, IPermissionScope permissionScope):
            base(securityContext,services, permissionScope)
        {
        }
    }
}
