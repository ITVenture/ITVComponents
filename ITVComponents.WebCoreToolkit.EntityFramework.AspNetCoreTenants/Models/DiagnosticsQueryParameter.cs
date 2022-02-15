using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class DiagnosticsQueryParameter: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.DiagnosticsQueryParameter<string, User, Role,Permission,UserRole,RolePermission,TenantUser,DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery>
    {
    }
}
