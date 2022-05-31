using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Options;
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
    internal class ToolkitPolicyProvider:IAuthorizationPolicyProvider
    {
        private readonly IOptions<ToolkitPolicyOptions> toolPolicyOptions;
        private readonly ILogger<ToolkitPolicyProvider> logger;
        private DefaultAuthorizationPolicyProvider backupBuilder;

        /// <summary>
        /// The Prefix for each HasPermission requirement
        /// </summary>
        private Regex rx = new Regex("^(,?(?<permissionBlock>(HasPermission|HasFeature)\\((,?[^,)]+)+\\)))+$");

        /// <summary>
        /// The detail regex for each specific requirement block
        /// </summary>
        private Regex detailRx = new Regex("^(?<requirementType>\\w+)\\((?<arguments>(,?[^,]+)+)\\)$");

        /// <summary>
        /// Initializes a new instance of the AssignedPermissionsPolicyProvider class
        /// </summary>
        /// <param name="authenticationSchemeProvider">an authenticaionSchemeProvider allowing this PolicyProvider to decide for which autenticationschemes the requirements need to be defined</param>
        /// <param name="options">the configuration for the role-based authorization</param>
        /// <param name="logger">a logger instance enabling this component to dump some debug information</param>
        public ToolkitPolicyProvider(IOptions<AuthorizationOptions> authOptions, IOptions<ToolkitPolicyOptions> toolPolicyOptions, ILogger<ToolkitPolicyProvider> logger)
        {
            backupBuilder = new DefaultAuthorizationPolicyProvider(authOptions);
            this.toolPolicyOptions = toolPolicyOptions;
            this.logger = logger;
        }

        /// <summary>Gets the default authorization policy.</summary>
        /// <returns>The default authorization policy.</returns>
        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => backupBuilder.GetDefaultPolicyAsync();

        /// <summary>Gets the fallback authorization policy.</summary>
        /// <returns>The fallback authorization policy.</returns>
        public Task<AuthorizationPolicy> GetFallbackPolicyAsync() => backupBuilder.GetFallbackPolicyAsync();

        /// <summary>
        /// Gets a <see cref="T:Microsoft.AspNetCore.Authorization.AuthorizationPolicy" /> from the given <paramref name="policyName" />
        /// </summary>
        /// <param name="policyName">The policy name to retrieve.</param>
        /// <returns>The named <see cref="T:Microsoft.AspNetCore.Authorization.AuthorizationPolicy" />.</returns>
        public async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            var pol = rx.Match(policyName.Trim());
            //logger.LogDebug(pol);
             //if (pol.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase) && pol.EndsWith(")"))
             if (pol.Success && pol.Groups["permissionBlock"].Captures.Count >= 1)
            {
                var policy = new AuthorizationPolicyBuilder(await GetDefaultPolicyAsync());
                var top = toolPolicyOptions.Value;
                if (top.SignInSchemes.Count != 0)
                {
                    top.SignInSchemes.ForEach(policy.AuthenticationSchemes.Add);
                }

                foreach (var cap in pol.Groups["permissionBlock"].Captures)
                {
                    if (cap != null)
                    {
                        var detailMatch = detailRx.Match(cap.ToString());
                        if (detailMatch.Success)
                        {
                            logger.LogDebug("OK...");
                            var tp = detailMatch.Groups["requirementType"].Value;
                            string requirementRaw = detailMatch.Groups["arguments"].Value;
                            //permissionsRaw = permissionsRaw.Substring(0, permissionsRaw.Length - 1);
                            string[] requirementList = (from t in requirementRaw.Split(',') select t.Trim()).ToArray();
                            if (tp == "HasPermission" && top.CheckPermissions)
                            {
                                policy.AddRequirements(new AssignedPermissionRequirement(requirementList));
                            }
                            else if (tp == "HasFeature" && top.CheckFeatures)
                            {
                                policy.AddRequirements(new FeatureActivatedRequirement(requirementList));
                            }
                        }
                    }
                }

                return policy.Build();
            }

            return null;
        }
    }
}
