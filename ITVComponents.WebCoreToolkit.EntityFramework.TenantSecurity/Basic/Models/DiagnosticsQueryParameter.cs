using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models
{
    public class DiagnosticsQueryParameter: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.DiagnosticsQueryParameter<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery>
    {
    }
}
