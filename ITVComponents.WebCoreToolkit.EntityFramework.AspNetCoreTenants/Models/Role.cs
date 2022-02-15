using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class Role: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.Role<string, User, Role,Permission,UserRole,RolePermission,TenantUser>
    {
    }
}
