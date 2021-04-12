using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Extensions
{

    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <param name="checkOnlyForKnownPermissions">indicates whether to check, if the requested permission is explicitly known. unknown permission requests are ignored</param>
        /// <param name="permissionEstimator">provides the selected permission-estimator back outside</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions, bool checkOnlyForKnownPermissions, out ISecurityRepository securityRepository)
        {
            var permissionScope = provider.GetService<IPermissionScope>();
            var logger = provider.GetService<ILoggerFactory>().CreateLogger("ITVComponents.WebCoreToolkit.Extensions.ServiceProviderExtensions");
            var userPerms = provider.GetUserPermissions(out securityRepository);
            var permitter = securityRepository;
            var extendedPerms = (from t in requiredPermissions where permissionScope?.PermissionPrefix != null && !t.StartsWith(permissionScope.PermissionPrefix, StringComparison.OrdinalIgnoreCase) select $"{permissionScope.PermissionPrefix}{t}").Union(requiredPermissions).Distinct(StringComparer.OrdinalIgnoreCase).Where(n => !checkOnlyForKnownPermissions || permitter.Permissions.Any(p => p.PermissionName.Equals(n, StringComparison.OrdinalIgnoreCase) || $"{permissionScope?.PermissionPrefix}{p.PermissionName}".Equals(n, StringComparison.OrdinalIgnoreCase))).ToArray();
            logger.LogDebug($"Found {extendedPerms.Length} permissions to check.");
            Array.ForEach(extendedPerms, s => logger.LogDebug(s));
            return extendedPerms.Length == 0 || extendedPerms.Any(t => userPerms.Contains(t, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <param name="permissionEstimator">provides the selected permission-estimator back outside</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions, out ISecurityRepository securityRepository)
        {
            return VerifyUserPermissions(provider, requiredPermissions, false, out securityRepository);
        }

        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions)
        {
            return VerifyUserPermissions(provider, requiredPermissions, false, out _);
        }

        /// <summary>
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredPermissions">a list of permissions that are requested for a specific action</param>
        /// <param name="checkOnlyForKnownPermissions">indicates whether to check, if the requested permission is explicitly known. unknown permission requests are ignored</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyUserPermissions(this IServiceProvider provider, string[] requiredPermissions, bool checkOnlyForKnownPermissions)
        {
            return VerifyUserPermissions(provider, requiredPermissions, checkOnlyForKnownPermissions, out _);
        }

        /// <summary>
        /// Gets the assigned permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="permissionEstimator">provides the selected permission-estimator back outside</param>
        /// <returns>a list of assigned permissions</returns>
        public static string[] GetUserPermissions(this IServiceProvider provider, out ISecurityRepository securityRepository)
        {
            securityRepository = provider.GetService<ISecurityRepository>();
            var httpContextAccess = provider.GetService<IHttpContextAccessor>();
            var userMapper = provider.GetService<IUserNameMapper>();
            var currentUser = httpContextAccess.HttpContext.User;
            var labels = userMapper.GetUserLabels(currentUser);
            var permissions = securityRepository.GetPermissions(labels, ((ClaimsIdentity)currentUser.Identity).AuthenticationType).Select(n => n.PermissionName).Distinct().ToArray();
            return permissions;
        }

        /// <summary>
        /// Gets the assigned permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <returns>a list of assigned permissions</returns>
        public static string[] GetUserPermissions(this IServiceProvider provider)
        {
            return provider.GetUserPermissions(out _);
        }
    }
}
