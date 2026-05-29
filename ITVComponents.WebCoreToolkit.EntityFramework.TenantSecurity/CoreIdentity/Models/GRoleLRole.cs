using Tenant =ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models
{
    public class GRoleLRole: Shared.Models.Base.GRoleLRole<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole>
    {
    }
}
