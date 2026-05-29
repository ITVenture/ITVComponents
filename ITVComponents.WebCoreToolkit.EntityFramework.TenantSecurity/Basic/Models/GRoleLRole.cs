using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tenant =ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models
{
    public class GRoleLRole: Shared.Models.Base.GRoleLRole<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
