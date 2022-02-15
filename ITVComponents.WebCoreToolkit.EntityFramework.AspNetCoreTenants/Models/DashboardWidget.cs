using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class DashboardWidget: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.DashboardWidget<string, User, Role,Permission,UserRole,RolePermission,TenantUser,DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam>
    {
    }
}
