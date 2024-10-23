using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class TenantNavigationMenu: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.TenantNavigationMenu<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, NavigationMenu,TenantNavigationMenu>
    {
    }
}
