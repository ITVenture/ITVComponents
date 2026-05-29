using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model
{
    public class TenantNavigationMenu : WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.TenantNavigationMenu<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu>
    {
    }
}
