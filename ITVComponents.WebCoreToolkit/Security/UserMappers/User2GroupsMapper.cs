using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Security.UserMappers
{
    /// <summary>
    /// Maps the user to the names in the group-collection in the claim
    /// </summary>
    internal class User2GroupsMapper : IUserNameMapper
    {
        private readonly UserMappingOptions userMappingOptions;
        private readonly User2GroupsMappingOptions options;
        private readonly string groupClaim;

        public User2GroupsMapper(IOptions<User2GroupsMappingOptions> options, IOptions<UserMappingOptions> userMappingOptions)
        {
            this.userMappingOptions = userMappingOptions.Value;
            this.options = options.Value;
        }

        public User2GroupsMapper(string groupClaim)
        {
            this.groupClaim = groupClaim;
            userMappingOptions = new();
        }

        /// <summary>
        /// Gets all labels for the given Identity
        /// </summary>
        /// <param name="user">the user for which to get all labels</param>
        /// <returns>a list of labels that are assigned to the given user</returns>
        public string[] GetUserLabels(IIdentity user)
        {
            List<string> retVal = new List<string>();
            if (!string.IsNullOrEmpty(user?.Name))
            {
                retVal.Add(user.Name);
            }

            if (user is ClaimsIdentity identity)
            {
                if (identity.Claims != null)
                {
                    var userClaim = options?.GetDefaultClaimFor(identity.AuthenticationType) ?? groupClaim;
                    foreach (var group in identity.Claims.Where(c => c.Type == userClaim))
                    {
                        retVal.Add(group.Value);
                    }

                    if (userMappingOptions.MapApplicationId)
                    {
                        if (userMappingOptions.MapApplicationId)
                        {
                            retVal.AddRange(from t in identity.Claims.Where(n => n.Type == ClaimTypes.ClientAppUser) select string.Format(Global.AppUserKeyIndicatorFormat, t.Value));
                        }
                    }
                }
            }

            return retVal.ToArray();
        }
    }
}
