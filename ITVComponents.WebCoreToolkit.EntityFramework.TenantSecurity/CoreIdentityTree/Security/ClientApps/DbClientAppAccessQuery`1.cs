using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security.ClientApps
{
    /// <summary>
    /// Die CoreIdentityTree-Auspraegung der Zugangs-Abfrage. Schliesst nur die Typparameter - die Arbeit steht in
    /// der Basisklasse.
    /// </summary>
    internal class AspNetTreeClientAppAccessQuery<TImpl> : DbClientAppAccessQuery<TImpl, HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppAccess>
        where TImpl : DbContext
    {
        public AspNetTreeClientAppAccessQuery(IDbContextFactory<TImpl> dbFactory, ILoggerFactory loggerFactory)
            : base(dbFactory, loggerFactory)
        {
        }
    }
}
