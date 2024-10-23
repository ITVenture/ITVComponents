using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;


namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class DashboardWidgetLocalization: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.DashboardWidgetLocalization<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization>
    {
    }
}
