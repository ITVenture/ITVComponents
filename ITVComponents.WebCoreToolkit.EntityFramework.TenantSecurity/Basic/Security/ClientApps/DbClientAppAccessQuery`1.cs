using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Security.ClientApps
{
    /// <summary>
    /// Die Basic-Auspraegung der Zugangs-Abfrage. Schliesst nur die Typparameter - die Arbeit steht in
    /// der Basisklasse.
    /// </summary>
    internal class FlatClientAppAccessQuery<TImpl> : DbClientAppAccessQuery<TImpl, Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppAccess, ClientAppTemplate>
        where TImpl : DbContext
    {
        public FlatClientAppAccessQuery(IDbContextFactory<TImpl> dbFactory, ILoggerFactory loggerFactory)
            : base(dbFactory, loggerFactory)
        {
        }
    }
}
