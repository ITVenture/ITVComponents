using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class AppPermission:AppPermission<string, User, Role, Permission, UserRole, RolePermission, TenantUser, AppPermission, AppPermissionSet>
    {
    }
}
