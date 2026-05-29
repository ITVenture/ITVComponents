using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models
{
    public class TenantNavigationMenu: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.TenantNavigationMenu<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,TenantNavigationMenu>
    {
    }
}
