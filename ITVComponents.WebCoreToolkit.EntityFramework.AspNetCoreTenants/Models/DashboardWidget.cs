using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class DashboardWidget: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.DashboardWidget<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam, DashboardWidgetLocalization>
    {
    }
}
