using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class DiagnosticsQueryParameter: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.DiagnosticsQueryParameter<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery>
    {
    }
}
