using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model
{
    public class TenantDiagnosticsQuery: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.TenantDiagnosticsQuery<HierarchyTenant, string, User, Role,Permission,UserRole,RolePermission,HierarchyTenantUser, RoleRole, DiagnosticsQuery,DiagnosticsQueryParameter, TenantDiagnosticsQuery>
    {
    }
}
