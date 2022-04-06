using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Helpers
{
    public class TenantTemplateHelper<TContext>:TenantTemplateHelperBase<string, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, UserWidget, CustomUserProperty,TContext>
        where TContext : AspNetSecurityContext<TContext>
    {
        public TenantTemplateHelper(TContext db, ILogger<TenantTemplateHelper<TContext>> logger) : base(db, logger)
        {
        }
    }
}
