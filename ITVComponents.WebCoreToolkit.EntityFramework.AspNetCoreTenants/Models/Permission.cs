using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class Permission: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.Permission<string, User, Role,Permission,UserRole,RolePermission,TenantUser>
    {
    }
}
