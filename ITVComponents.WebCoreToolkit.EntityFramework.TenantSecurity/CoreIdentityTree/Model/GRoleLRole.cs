using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using Tenant =ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model
{
    public class GRoleLRole: Shared.Models.Base.GRoleLRole<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
