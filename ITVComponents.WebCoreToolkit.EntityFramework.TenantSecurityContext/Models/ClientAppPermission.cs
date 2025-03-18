using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class ClientAppPermission: ClientAppPermission<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppUser>
    {
    }
}
