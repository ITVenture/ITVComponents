using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model
{
    public class Permission : WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.Permission<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole>
    {
    }
}
