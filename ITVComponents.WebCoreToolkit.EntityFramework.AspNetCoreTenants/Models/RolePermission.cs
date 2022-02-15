using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class RolePermission: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.RolePermission<string, User, Role,Permission,UserRole,RolePermission,TenantUser>
    {
    }
}
