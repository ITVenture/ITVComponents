using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models
{
    public class Permission: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.Permission<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
