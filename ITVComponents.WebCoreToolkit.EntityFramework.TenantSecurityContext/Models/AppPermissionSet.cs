using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class AppPermissionSet: AppPermissionSet<int, User, Role, Permission, UserRole, RolePermission, TenantUser, AppPermission, AppPermissionSet>
    {
    }
}
