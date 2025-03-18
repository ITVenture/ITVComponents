using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class NavigationMenu: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.NavigationMenu<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, NavigationMenu,TenantNavigationMenu>
    {
    }
}
