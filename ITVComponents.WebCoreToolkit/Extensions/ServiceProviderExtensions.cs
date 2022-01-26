using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Models;
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
            var logger = provider.GetService<ILogger<GenericLogTarget>>();//("ITVComponents.WebCoreToolkit.Extensions.ServiceProviderExtensions");
            var userPerms = provider.GetUserPermissions(out securityRepository, out var isAuthenticated);
            if (isAuthenticated)
            {
                var permitter = securityRepository;
                var extendedPerms =
                    (from t in requiredPermissions
                        where permissionScope?.PermissionPrefix != null &&
                              !t.StartsWith(permissionScope.PermissionPrefix, StringComparison.OrdinalIgnoreCase)
                        select $"{permissionScope.PermissionPrefix}{t}").Union(requiredPermissions)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Where(n =>
                        !checkOnlyForKnownPermissions || permitter.Permissions.Any(p =>
                            p.PermissionName.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                            $"{permissionScope?.PermissionPrefix}{p.PermissionName}".Equals(n,
                                StringComparison.OrdinalIgnoreCase))).ToArray();
                logger.LogDebug($"Found {extendedPerms.Length} permissions to check.");
                Array.ForEach(extendedPerms, s => logger.LogDebug(s));
                return extendedPerms.Length == 0 ||
                       extendedPerms.Any(t => userPerms.Contains(t, StringComparer.OrdinalIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Verifies whether the current user is in a legal context 
        /// </summary>
        /// <param name="services">the service-provider that holds all services for the current request</param>
        /// <returns>a value indicating whether the user is valid in the current context</returns>
        public static bool VerifyCurrentUser(this IServiceProvider services)
        {
            services.GetUserPermissions(out _, out var isAuthenticated);
            return isAuthenticated;
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
        public static string[] GetUserPermissions(this IServiceProvider provider, out ISecurityRepository securityRepository, out bool isAuthenticated)
        {
            securityRepository = provider.GetService<ISecurityRepository>();
            var userProvider = provider.GetService<IContextUserProvider>();
            var userMapper = provider.GetService<IUserNameMapper>();
            var currentUser = userProvider.User;
            var labels = userMapper.GetUserLabels(currentUser);
            var authType = ((ClaimsIdentity)currentUser.Identity).AuthenticationType;
            isAuthenticated = securityRepository.IsAuthenticated(labels, authType);
            var permissions = isAuthenticated?securityRepository.GetPermissions(labels, authType).Select(n => n.PermissionName).Distinct().ToArray():Array.Empty<string>();
            return permissions;
        }

        /// <summary>
        /// Gets the assigned permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <returns>a list of assigned permissions</returns>
        public static string[] GetUserPermissions(this IServiceProvider provider, out bool isAuthenticated)
        {
            return provider.GetUserPermissions(out _, out isAuthenticated);
        }

        /// <summary>
        /// Stores the relevant data of the current request and puts it into an object. The data can be restored later in a different context.
        /// </summary>
        /// <param name="provider">the services that are available in the current context</param>
        /// <returns>an object that contains relevant data of the current context</returns>
        public static object ConserveRequestData(this IServiceProvider provider)
        {
            var requestData = new ConservedRequestData();
            var userProvider = provider.GetService<IContextUserProvider>();
            var permissionScope = provider.GetService<IPermissionScope>();
            if (userProvider != null)
            {
                requestData.User = userProvider.User;
                requestData.RouteData = new Dictionary<string, object>(userProvider.RouteData);
                requestData.RequestPath = userProvider.RequestPath;
            }

            if (permissionScope != null)
            {
                requestData.CurrentScope = permissionScope.PermissionPrefix;
            }

            return requestData;
        }

        /// <summary>
        /// Prepares the current scope to values that were conserved before from a different context
        /// </summary>
        /// <param name="provider">the service-provider that holds all dependencies</param>
        /// <param name="conservedRequestData">the previously conserved context data</param>
        public static void PrepareContext(this IServiceProvider provider, object conservedRequestData)
        {
            if (conservedRequestData is not ConservedRequestData crd)
            {
                throw new InvalidOperationException(
                    "An object that was generated using the ConserveRequestData method is required");
            }

            var userProvider = provider.GetService<IContextUserProvider>();
            var permissionScope = provider.GetService<IPermissionScope>();
            if (userProvider is DefaultContextUserProvider dcup)
            {
                dcup.SetDefaults(crd.User, crd.RouteData, crd.RequestPath);
            }

            if (permissionScope is PermissionScopeBase psb)
            {
                psb.SetFixedScope(crd.CurrentScope);
            }
        }
    }
}
