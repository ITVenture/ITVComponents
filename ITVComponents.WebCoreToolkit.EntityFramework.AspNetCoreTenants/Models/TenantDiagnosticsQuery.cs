using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class TenantDiagnosticsQuery: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.TenantDiagnosticsQuery<string, User, Role,Permission,UserRole,RolePermission,TenantUser,DiagnosticsQuery,DiagnosticsQueryParameter, TenantDiagnosticsQuery>
    {
    }
}
