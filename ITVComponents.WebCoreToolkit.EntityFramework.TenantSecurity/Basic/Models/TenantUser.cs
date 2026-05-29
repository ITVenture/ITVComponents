using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models
{
    public class TenantUser: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.TenantUser<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
