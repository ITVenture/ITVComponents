using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Security
{
    public static class ActiveDirectoryHelper
    {
        /// <summary>
        /// Gets the UserGroups for a specific AD-User
        /// </summary>
        /// <param name="domain">the domain for which to resolve the users</param>
        /// <param name="userName">the sam-username for the target-user</param>
        /// <returns>a list of groups for the given username</returns>
        public static IEnumerable<GroupPrincipal> GetUserGroupsFor(string domain, string userName)
        {
            using (PrincipalContext context = new PrincipalContext(ContextType.Domain, domain))
            {
                using (UserPrincipal user =
                    UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, userName))
                {
                    var groups = user.GetAuthorizationGroups().Cast<GroupPrincipal>().ToArray();
                    return from g in groups select g;
                }
            }
        }

        /// <summary>
        /// Normalizes the name of the given user or group
        /// </summary>
        /// <param name="userName">the user- ir groupname to normalize</param>
        /// <param name="nameRaw">the user- or gropuname without domain-information</param>
        /// <returns>the user- or groupname with domain information in a standard-format that can be queried in the database</returns>
        public static string NormalizeUserName(string userName, out string nameRaw)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                nameRaw = userName;
                int bkIndex = nameRaw.IndexOf(@"\");
                int atIndex = nameRaw.IndexOf("@");
                string domain = "";
                if (bkIndex != -1 && atIndex == -1)
                {
                    domain = nameRaw.Substring(0, bkIndex);
                    nameRaw = nameRaw.Substring(bkIndex + 1);
                }
                else if (atIndex != -1 && bkIndex == -1)
                {
                    domain = nameRaw.Substring(atIndex + 1);
                    int firstDot = domain.IndexOf(".");
                    if (firstDot != -1)
                    {
                        domain = domain.Substring(0, firstDot);
                    }

                    nameRaw = nameRaw.Substring(0, atIndex);
                }

                return $@"{(!string.IsNullOrEmpty(domain) ? $@"{domain}\" : "")}{nameRaw}";
            }

            nameRaw = "";
            return "";
        }
    }
}
