using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models
{
    public class NavigationMenu: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.NavigationMenu<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,TenantNavigationMenu>
    {
    }
}
