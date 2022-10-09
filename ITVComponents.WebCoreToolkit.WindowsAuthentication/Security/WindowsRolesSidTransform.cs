using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security.ClaimsTransformation;
using ITVComponents.WebCoreToolkit.WindowsAuthentication.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.WindowsAuthentication.Security
{
    internal class WindowsRolesSidTransform:ICollectedClaimsProvider
    {
        private readonly IOptions<RolesTransformationOptions> options;

        /// <summary>
        /// Initializes a new instance of the WindowsRolesSidTransform class
        /// </summary>
        /// <param name="options">the options defining how the group-claims have to be transalted</param>
        public WindowsRolesSidTransform(IOptions<RolesTransformationOptions> options)
        {
            this.options = options;
        }

        /// <summary>
        /// Provides a central transformation point to change the specified principal.
        /// Note: this will be run on each AuthenticateAsync call, so its safer to
        /// return a new ClaimsPrincipal if your transformation is not idempotent.
        /// </summary>
        /// <param name="principal">The <see cref="T:System.Security.Claims.ClaimsPrincipal" /> to transform.</param>
        /// <returns>The transformed principal.</returns>
        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            var opt = options.Value;
            var ci = (ClaimsIdentity) principal.Identity;
            var roles = ci.Claims.Where(q => q.Type == System.Security.Claims.ClaimTypes.GroupSid).Select(q => q.Value).ToArray();
            foreach (var role in roles)
            {
                var name = new System.Security.Principal.SecurityIdentifier(role).Translate(typeof(System.Security.Principal.NTAccount)).ToString();
                if (opt.GroupFilter == null || opt.GroupFilter(name))
                {
                    if (opt.NormalizeGroupName)
                    {
                        name = NormalizeName(name);
                    }
                    
                    ci.AddClaim(new Claim(opt.TargetClaimType, name,ClaimValueTypes.String, opt.TargetClaimIssuer));
                }
            }

            return principal;
        }
        
        /// <summary>
        /// Removes domain-information from the group-name
        /// </summary>
        /// <param name="groupName">the group-name</param>
        /// <returns>the group name without domain-specific information</returns>
        private string NormalizeName(string groupName)
        {
            var id = groupName.IndexOf(@"\");
            var retVal = groupName.Substring(id + 1);
            id = groupName.IndexOf("@");
            if (id != -1)
            {
                retVal = groupName.Substring(0, id);
            }

            return retVal;
        }
    }
}
