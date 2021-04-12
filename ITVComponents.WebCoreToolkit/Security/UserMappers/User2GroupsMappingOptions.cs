using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.UserMappers
{
    public class User2GroupsMappingOptions
    {
        private Dictionary<string ,string> authTypeRoleClaims = new Dictionary<string, string>();
        public string DefaultUserGroupMappingClaim { get; set; } = "http://schemas.xmlsoap.org/claims/Group";

        public User2GroupsMappingOptions()
        {
            AuthenticationTypeRoleClaims = new ReadOnlyDictionary<string, string>(authTypeRoleClaims);
        }

        public IReadOnlyDictionary<string, string> AuthenticationTypeRoleClaims { get; }

        public User2GroupsMappingOptions SetGroupClaim(string authenticationType, string userGroupMappingClaim)
        {
            authTypeRoleClaims[authenticationType] = userGroupMappingClaim;
            return this;
        }

        public string GetDefaultClaimFor(string authenticationType)
        {
            string retVal = DefaultUserGroupMappingClaim;
            if (!string.IsNullOrEmpty(authenticationType))
            {
                if (AuthenticationTypeRoleClaims.ContainsKey(authenticationType))
                {
                    retVal = AuthenticationTypeRoleClaims[authenticationType];
                }
            }

            return retVal;
        }
    }
}
