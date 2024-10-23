using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class TenantUser: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.TenantUser<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole>
    {
    }
}
