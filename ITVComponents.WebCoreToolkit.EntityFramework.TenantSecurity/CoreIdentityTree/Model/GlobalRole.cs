
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model
{
    public class GlobalRole:GlobalRole<HierarchyTenant,string,User,Role,Permission,UserRole,RolePermission,HierarchyTenantUser, RoleRole,GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
