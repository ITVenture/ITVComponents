using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models
{
    public class TenantDiagnosticsQuery: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.TenantDiagnosticsQuery<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, DiagnosticsQuery,DiagnosticsQueryParameter, TenantDiagnosticsQuery>
    {
    }
}
