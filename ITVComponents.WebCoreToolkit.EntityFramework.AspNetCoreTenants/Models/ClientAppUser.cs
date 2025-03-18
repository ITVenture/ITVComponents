using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class ClientAppUser: ClientAppUser<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppUser>
    {
    }
}
