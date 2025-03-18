using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model
{
    public class DiagnosticsQueryParameter: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.DiagnosticsQueryParameter<HierarchyTenant, string, User, Role,Permission,UserRole,RolePermission,HierarchyTenantUser, RoleRole, DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery>
    {
    }
}
