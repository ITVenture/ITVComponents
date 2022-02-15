using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class NavigationMenu: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.NavigationMenu<string, User, Role,Permission,UserRole,RolePermission,TenantUser,NavigationMenu,TenantNavigationMenu>
    {
    }
}
