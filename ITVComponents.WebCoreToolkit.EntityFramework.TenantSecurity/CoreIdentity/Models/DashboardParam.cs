using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models
{
    public class DashboardParam: WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.DashboardParam<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam, DashboardWidgetLocalization>
    {
    }
}
