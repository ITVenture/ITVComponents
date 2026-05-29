using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models
{
    public class TenantDiagnosticsQuery: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.TenantDiagnosticsQuery<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, DiagnosticsQuery,DiagnosticsQueryParameter, TenantDiagnosticsQuery>
    {
    }
}
