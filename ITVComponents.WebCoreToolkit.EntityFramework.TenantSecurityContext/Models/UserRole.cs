using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class UserRole: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.UserRole<int,User,Role,Permission,UserRole,RolePermission,TenantUser>
    {
    }
}
