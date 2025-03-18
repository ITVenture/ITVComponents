using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class UserWidget: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.UserWidget<Tenant, string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam, DashboardWidgetLocalization>
    {
    }
}
