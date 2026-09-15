using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Security.ClientApps
{
    /// <summary>
    /// Die CoreIdentity-Auspraegung der Zugangs-Abfrage. Schliesst nur die Typparameter - die Arbeit steht in
    /// der Basisklasse.
    /// </summary>
    internal class AspNetClientAppAccessQuery<TImpl> : DbClientAppAccessQuery<TImpl, Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppAccess, ClientAppTemplate>
        where TImpl : DbContext
    {
        public AspNetClientAppAccessQuery(IDbContextFactory<TImpl> dbFactory, ILoggerFactory loggerFactory)
            : base(dbFactory, loggerFactory)
        {
        }
    }
}
