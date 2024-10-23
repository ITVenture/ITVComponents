using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class TenantUser: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.TenantUser<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole>
    {
    }
}
