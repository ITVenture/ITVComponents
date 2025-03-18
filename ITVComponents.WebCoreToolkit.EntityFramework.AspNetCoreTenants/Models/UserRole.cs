using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class UserRole: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.UserRole<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole>
    {
    }
}
