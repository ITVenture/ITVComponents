using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Security.PermissionHandling
{
    /// <summary>
    /// Creates requirements for policies starting with 'HasPermission('
    /// </summary>
    internal class AssignedPermissionsPolicyProvider:IAuthorizationPolicyProvider
    {
        /// <summary>
        /// an authenticationSchemeProvider allowing this PolicyProvider to decide for which authenticationschemes the requirements need to be defined
        /// </summary>
        private readonly IAuthenticationSchemeProvider authenticationSchemeProvider;

        private readonly ILogger<AssignedPermissionsPolicyProvider> logger;

        /// <summary>
        /// the options for this policyProvider
        /// </summary>
        private PermissionPolicyOptions options;

        /// <summary>
        /// The Prefix for each HasPermission requirement
        /// </summary>
        private const string PolicyPrefix = "HasPermission(";

        /// <summary>
        /// Initializes a new instance of the AssignedPermissionsPolicyProvider class
        /// </summary>
        /// <param name="authenticationSchemeProvider">an authenticaionSchemeProvider allowing this PolicyProvider to decide for which autenticationschemes the requirements need to be defined</param>
        /// <param name="options">the configuration for the role-based authorization</param>
        /// <param name="logger">a logger instance enabling this component to dump some debug information</param>
        public AssignedPermissionsPolicyProvider(IAuthenticationSchemeProvider authenticationSchemeProvider, IOptions<PermissionPolicyOptions> options, ILogger<AssignedPermissionsPolicyProvider> logger)
        {
            this.authenticationSchemeProvider = authenticationSchemeProvider;
            this.logger = logger;
            this.options = options.Value;
        }

        /// <summary>Gets the default authorization policy.</summary>
        /// <returns>The default authorization policy.</returns>
        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(new AuthorizationPolicyBuilder(options.SupportedAuthenticationSchemes.ToArray()).RequireAuthenticatedUser().Build());

        /// <summary>Gets the fallback authorization policy.</summary>
        /// <returns>The fallback authorization policy.</returns>
        public Task<AuthorizationPolicy> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy>(null);

        /// <summary>
        /// Gets a <see cref="T:Microsoft.AspNetCore.Authorization.AuthorizationPolicy" /> from the given <paramref name="policyName" />
        /// </summary>
        /// <param name="policyName">The policy name to retrieve.</param>
        /// <returns>The named <see cref="T:Microsoft.AspNetCore.Authorization.AuthorizationPolicy" />.</returns>
        public async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            string pol = policyName.Trim();
            logger.LogDebug(pol);
            if (pol.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase) && pol.EndsWith(")"))
            {
                logger.LogDebug("OK...");
                string permissionsRaw = policyName.Substring(PolicyPrefix.Length);
                permissionsRaw = permissionsRaw.Substring(0, permissionsRaw.Length - 1);
                string[] perms = (from t in permissionsRaw.Split(',') select t.Trim()).ToArray();
                var policy = new AuthorizationPolicyBuilder(options.SupportedAuthenticationSchemes.ToArray());
                policy.AddRequirements(new AssignedPermissionRequirement(perms));
                logger.LogDebug(string.Join(";", perms));
                return policy.Build();
            }

            return null;
        }
    }
}
