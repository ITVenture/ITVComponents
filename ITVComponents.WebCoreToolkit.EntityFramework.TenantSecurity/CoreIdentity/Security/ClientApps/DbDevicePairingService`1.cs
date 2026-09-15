using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.DevicePairing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Security.ClientApps
{
    /// <summary>
    /// Die CoreIdentity-Auspraegung des Kopplungs-Ablaufs. Schliesst nur die Typparameter.
    /// </summary>
    internal class AspNetDevicePairingService<TImpl> : DbDevicePairingService<TImpl, Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppAccess, DevicePairing>
        where TImpl : DbContext
    {
        public AspNetDevicePairingService(IDbContextFactory<TImpl> dbFactory, IGlobalSettings<DevicePairingOptions> settings,
            IPermissionScope permissionScope, IContextUserProvider userProvider,
            ISecurityRepository securityRepository, ILoggerFactory loggerFactory)
            : base(dbFactory, settings, permissionScope, userProvider, securityRepository, loggerFactory)
        {
        }
    }
}
