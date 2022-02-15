using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class UserRole: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.UserRole<string, User, Role,Permission,UserRole,RolePermission,TenantUser>
    {
    }
}
