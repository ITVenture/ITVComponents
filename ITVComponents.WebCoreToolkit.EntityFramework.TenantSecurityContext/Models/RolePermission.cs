using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class RolePermission: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.RolePermission<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole>
    {
    }
}
