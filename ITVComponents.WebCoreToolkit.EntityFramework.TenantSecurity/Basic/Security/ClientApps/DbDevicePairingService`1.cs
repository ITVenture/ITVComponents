using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.DevicePairing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Security.ClientApps
{
    /// <summary>
    /// Die Basic-Auspraegung des Kopplungs-Ablaufs. Schliesst nur die Typparameter.
    /// </summary>
    internal class FlatDevicePairingService<TImpl> : DbDevicePairingService<TImpl, Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppAccess, DevicePairing>
        where TImpl : DbContext
    {
        public FlatDevicePairingService(IDbContextFactory<TImpl> dbFactory, IGlobalSettings<DevicePairingOptions> settings,
            IPermissionScope permissionScope, IContextUserProvider userProvider,
            ISecurityRepository securityRepository, ILoggerFactory loggerFactory)
            : base(dbFactory, settings, permissionScope, userProvider, securityRepository, loggerFactory)
        {
        }
    }
}
