using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class AppPermission:AppPermission<int, User, Role, Permission, UserRole, RolePermission, TenantUser, AppPermission, AppPermissionSet>
    {
    }
}
