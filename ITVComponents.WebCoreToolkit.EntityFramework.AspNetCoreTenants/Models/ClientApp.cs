using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class ClientApp: ClientApp<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppUser>
    {
    }
}
