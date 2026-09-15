using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.DevicePairing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security.ClientApps
{
    /// <summary>
    /// Die CoreIdentityTree-Auspraegung des Kopplungs-Ablaufs. Schliesst nur die Typparameter.
    /// </summary>
    internal class AspNetTreeDevicePairingService<TImpl> : DbDevicePairingService<TImpl, HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, AppPermission, AppPermissionSet, ClientAppPermission, ClientApp, ClientAppAccess, DevicePairing, ClientAppTemplate>
        where TImpl : DbContext
    {
        public AspNetTreeDevicePairingService(IDbContextFactory<TImpl> dbFactory, IGlobalSettings<DevicePairingOptions> settings,
            IPermissionScope permissionScope, IContextUserProvider userProvider,
            ISecurityRepository securityRepository, ILoggerFactory loggerFactory)
            : base(dbFactory, settings, permissionScope, userProvider, securityRepository, loggerFactory)
        {
        }
    }
}
