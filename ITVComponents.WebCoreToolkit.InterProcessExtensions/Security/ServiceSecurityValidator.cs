using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.InterProcessCommunication.Shared.Security.PermissionBasedSecurity;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Security
{
    /// <summary>
    /// Permission driven Security Validator for service objects
    /// </summary>
    public class ServiceSecurityValidator:ServiceSecurityValidatorBase
    {
        private readonly ISecurityRepository securityRepo;
        private readonly IUserNameMapper nameMapper;

        /// <summary>
        /// Initializes a new instance of the ServiceSecurityValidator class
        /// </summary>
        /// <param name="factory">the pluginfactory that provides objects that may be accessed by clients</param>
        /// <param name="securityRepo">a WebCoreToolkit-Security Repository holding users and roles</param>
        /// <param name="nameMapper">a User-Mapper that is used to extract the permissions of the provided user</param>
        public ServiceSecurityValidator(PluginFactory factory, ISecurityRepository securityRepo, IUserNameMapper nameMapper):base(factory)
        {
            this.securityRepo = securityRepo;
            this.nameMapper = nameMapper;
        }

        protected override IEnumerable<KeyValuePair<string, string>> SelectCustomProperties(IIdentity identity)
        {
            return securityRepo.GetCustomProperties(nameMapper.GetUserLabels(identity), identity.AuthenticationType).Select(m => new KeyValuePair<string, string>(m.PropertyName, m.Value));
        }

        /// <summary>
        /// Verifies the permissions of a user for a set of assigned HasPermission attributes
        /// </summary>
        /// <param name="userIdentity">the identity for which to check the permissions</param>
        /// <param name="securityAttributes">the securityattributes found on the desired class or member</param>
        /// <param name="reason">provides a reason, why access was denied</param>
        /// <returns>a value whether the access can be granted</returns>
        protected override bool VerifyPermissionLabels(IIdentity userIdentity, string[] requiredPermissions, out string[] effectivePermissions)
        {
            bool retVal = true;
            effectivePermissions = null;
            if (requiredPermissions.Length != 0)
            {
                string[] labels = nameMapper.GetUserLabels(userIdentity);
                effectivePermissions = securityRepo.GetPermissions(labels, userIdentity.AuthenticationType).Select(n => n.PermissionName).Distinct().ToArray();
                retVal = (from t in requiredPermissions join p in effectivePermissions on t equals p select t).Any();
            }

            return retVal;

        }
    }
}
