using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class GlobalRole:GlobalRole<Tenant,string,User,Role,Permission,UserRole,RolePermission,TenantUser,RoleRole,GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
