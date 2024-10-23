using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class RolePermission: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.RolePermission<Tenant,string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole>
    {
    }
}
