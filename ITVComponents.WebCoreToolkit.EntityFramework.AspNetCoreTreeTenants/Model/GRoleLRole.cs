using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using Tenant =ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Tenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model
{
    public class GRoleLRole: TenantSecurityShared.Models.Base.GRoleLRole<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
