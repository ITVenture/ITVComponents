using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class TenantNavigationMenu: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.TenantNavigationMenu<string, User, Role,Permission,UserRole,RolePermission,TenantUser,NavigationMenu,TenantNavigationMenu>
    {
    }
}
