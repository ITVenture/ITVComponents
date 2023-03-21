using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class DashboardParam: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.DashboardParam<string, User, Role,Permission,UserRole,RolePermission,TenantUser,DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam, DashboardWidgetLocalization>
    {
    }
}
