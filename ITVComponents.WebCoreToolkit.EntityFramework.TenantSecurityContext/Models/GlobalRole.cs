using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class GlobalRole:GlobalRole<Tenant,int,User,Role,Permission,UserRole,RolePermission,TenantUser,RoleRole,GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
