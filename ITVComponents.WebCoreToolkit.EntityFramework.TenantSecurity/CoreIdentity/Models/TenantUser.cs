using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models
{
    public class TenantUser: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.TenantUser<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
