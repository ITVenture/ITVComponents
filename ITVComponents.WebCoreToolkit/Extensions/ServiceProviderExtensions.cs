using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
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
        /// Verifies the User-Permissions for the current user
        /// </summary>
        /// <param name="provider">the service-provider for the current scope</param>
        /// <param name="requiredFeatures">a list of permissions that are requested for a specific action</param>
        /// <param name="securityRepository">the security-repository that can be used to perform further security-checks</param>
        /// <returns>a value indicating whether the current request is legit</returns>
        public static bool VerifyActivatedFeatures(this IServiceProvider provider, string[] requiredFeatures, out ISecurityRepository securityRepository)
        {
            var permissionScope = provider.GetService<IPermissionScope>();
            var logger = provider.GetService<ILogger<GenericLogTarget>>();//("ITVComponents.WebCoreToolkit.Extensions.ServiceProviderExtensions");
            var isAuthenticated = (provider.IsLegitSharedAssetPath(out securityRepository, out _, out var denied) || provider.IsUserAuthenticated(out securityRepository, out _)) && !denied;
            if (isAuthenticated)
            {
                string[] features = securityRepository.GetFeatures(permissionScope.PermissionPrefix)
                    .Where(n => n.Enabled).Select(n => n.FeatureName).ToArray();
                return requiredFeatures.Any(f => features.Contains(f));
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
        public static string[] GetUserPermissions(this IServiceProvider provider,
            out ISecurityRepository securityRepository, out bool isAuthenticated)
        {
            string[] permissions = null;
            IdentityInfo[] identities;
            isAuthenticated = (provider.IsLegitSharedAssetPath(out securityRepository, out identities, out var denied) ||
                              provider.IsUserAuthenticated(out securityRepository, out identities)) && !denied;

            if (isAuthenticated)
            {
                var rp = securityRepository;
                permissions = identities.SelectMany(i => rp.GetPermissions(i.Labels, i.AuthenticationType)).Select(n => n.PermissionName)
                    .Distinct()
                    .ToArray();
            }

            return permissions ?? Array.Empty<string>();
        }

        public static ISecurityRepository GetAssetSecurityRepository(this IServiceProvider services, ISecurityRepository decorated)
        {
            var httpContext = services.GetService<IHttpContextAccessor>();
            var authUser = httpContext.HttpContext.User.Identities.FirstOrDefault(n => n.IsAuthenticated);
            var decorator = new SecurityRepository();
            decorator.PushRepo(decorated);
            if (authUser != null && 
                authUser.HasClaim(n => n.Type == ClaimTypes.FixedUserScope) &&
                authUser.HasClaim(n => n.Type == ClaimTypes.FixedAssetFeature) &&
                authUser.HasClaim(n => n.Type == ClaimTypes.FixedAssetPermission))
            {
                var repo = new AssetSecurityRepository(httpContext.HttpContext.User, decorated);
                decorator.PushRepo(repo);
            }

            return decorator;
        }

        public static bool IsLegitSharedAssetPath(this IServiceProvider provider,
            out ISecurityRepository securityRepository, out IdentityInfo[] identities, out bool denied)
        {
            denied = false;
            var assetProvider = provider.GetService<ISharedAssetAdapter>();
            var userProvider = provider.GetService<IContextUserProvider>();
            IQueryCollection refQ;
            if (((refQ=userProvider.HttpContext.Request.Query).ContainsKey(Global.FixedAssetRequestQueryParameter)
                || (refQ = userProvider.HttpContext.Request.GetRefererQuery()) != null && refQ.ContainsKey(Global.FixedAssetRequestQueryParameter)) && 
                userProvider.User.HasClaim(n => n.Type == ClaimTypes.FixedUserScope))
            {
                var requestPath = userProvider.HttpContext.Request.Path;
                var assetKey = refQ[Global.FixedAssetRequestQueryParameter];
                var userScope = userProvider.HttpContext.User.Claims.First(n => n.Type == ClaimTypes.FixedUserScope)
                    .Value;
                if (assetProvider != null && !(denied = !assetProvider.VerifyRequestLocation(requestPath,assetKey, userScope, userProvider.User)))
                {
                    identities= (from t in userProvider.User.Identities where t.IsAuthenticated select new IdentityInfo{Labels=new []{t.Name}, AuthenticationType=t.AuthenticationType}).ToArray();//new string[] { userProvider.User.Identity.Name };
                    var tmp = provider.GetService<ISecurityRepository>();
                    if (tmp is not SecurityRepository seco)
                    {
                        throw new InvalidOperationException(
                            "SecurityRepository is required to make this work! Use GetAssetSecurityRepository in your ISecurityRepository dependency injection call.");
                    }

                    if (seco.Current is not AssetSecurityRepository)
                    {
                        seco.PushRepo(new AssetSecurityRepository(userProvider.User, seco.Current, assetProvider.GetAssetInfo(assetKey, userProvider.User)));
                    }
                    securityRepository = seco;
                    return true;
                }
            }

            securityRepository = null;
            identities = null;
            return false;
        }

        /// <summary>
        /// Indicates whether the current logged-in user is considered authenticated
        /// </summary>
        /// <param name="provider">the service-provider for the current http-context</param>
        /// <param name="securityRepository">the security-context responsible for all authorization-tasks</param>
        /// <param name="labels">the user-labels of the current user</param>
        /// <param name="authType">the authentication-type that was used to log this user in</param>
        /// <returns>a value indicating whether the current user is correlctly authenticated</returns>
        public static bool IsUserAuthenticated(this IServiceProvider provider, out ISecurityRepository securityRepository, out IdentityInfo[] identities)
        {
            var userProvider = provider.GetService<IContextUserProvider>();
            var userMapper = provider.GetService<IUserNameMapper>();
            var currentUser = userProvider.User;
            identities = (from t in currentUser.Identities
                where t.IsAuthenticated
                select new IdentityInfo
                    { AuthenticationType = t.AuthenticationType, Labels = userMapper.GetUserLabels(t) }).ToArray();
            var rp= securityRepository = provider.GetService<ISecurityRepository>();
            return identities.Any(a => rp.IsAuthenticated(a.Labels, a.AuthenticationType));
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
