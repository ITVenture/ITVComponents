using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Security
{
    /// <summary>
    /// Permission driven Security Validator for service objects
    /// </summary>
    public class ServiceSecurityValidator:ICustomServerSecurity, IPlugin
    {
        private readonly PluginFactory factory;
        private readonly ISecurityRepository securityRepo;
        private readonly IUserNameMapper nameMapper;

        /// <summary>
        /// Initializes a new instance of the ServiceSecurityValidator class
        /// </summary>
        /// <param name="factory">the pluginfactory that provides objects that may be accessed by clients</param>
        /// <param name="securityRepo">a WebCoreToolkit-Security Repository holding users and roles</param>
        /// <param name="nameMapper">a User-Mapper that is used to extract the permissions of the provided user</param>
        public ServiceSecurityValidator(PluginFactory factory, ISecurityRepository securityRepo, IUserNameMapper nameMapper)
        {
            this.factory = factory;
            this.securityRepo = securityRepo;
            this.nameMapper = nameMapper;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Verifies access to a plugin for the specified identity
        /// </summary>
        /// <param name="objectName">the object for which to verify the access</param>
        /// <param name="userIdentity">the identity for which to check the access</param>
        /// <param name="reason">returns a reason, why the access was denied</param>
        /// <returns>a value indicating whether the provided user has access to the requested object</returns>
        public bool VerifyAccess(string objectName, IIdentity userIdentity, out string reason)
        {
            bool retVal = false;
            reason = "the object is unknown";
            if (factory.Contains(objectName) && userIdentity != null)
            {
                retVal = true;
                reason = string.Empty;
                var obj = factory[objectName];
                var type = obj.GetType();
                HasPermissionAttribute[] securityAttributes = type.GetCustomAttributes(typeof(HasPermissionAttribute)).Cast<HasPermissionAttribute>().ToArray();
                if (securityAttributes.Length != 0)
                {
                    retVal = VerifyPermissions(userIdentity, securityAttributes, out reason);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Verfies the read/write access for a specific property
        /// </summary>
        /// <param name="property">the property that is being accessed</param>
        /// <param name="objectName">the object name for which to check the permission</param>
        /// <param name="arguments">the arguments that are provided with the index</param>
        /// <param name="userIdentity">the identity that is requesting access to the provided property</param>
        /// <param name="write">indicates whether the caller has requested write-access</param>
        /// <param name="reason">provides a reason when the access was denied</param>
        /// <returns>a value indicating whether to allow access to the provided property</returns>
        public bool VerifyAccess(PropertyInfo property, string objectName, object[] arguments, IIdentity userIdentity, bool write, out string reason)
        {
            bool retVal = VerifyAccess(objectName, userIdentity, out reason);
            if (retVal)
            {
                reason = string.Empty;
                HasPermissionAttribute[] securityAttributes = property.GetCustomAttributes(typeof(HasPermissionAttribute)).Cast<HasPermissionAttribute>().Where(n => (write && n.AllowWrite) || (!write && n.AllowRead)).ToArray();
                if (securityAttributes.Length != 0)
                {
                    retVal = VerifyPermissions(userIdentity, securityAttributes, out reason);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Verifies execution access for a specific method
        /// </summary>
        /// <param name="method">the method that was requested to execute by a caller</param>
        /// <param name="objectName">the object name for which to check the permission</param>
        /// <param name="arguments">the arguments for the called method</param>
        /// <param name="userIdentity">the authenticated user that wishes to execute the provided method</param>
        /// <param name="reason">provides a reason when the access was denied</param>
        /// <returns>a value indicating whether to allow the provided method-call</returns>
        public bool VerifyAccess(MethodInfo method, string objectName, object[] arguments, IIdentity userIdentity, out string reason)
        {
            bool retVal = VerifyAccess(objectName, userIdentity, out reason);
            if (retVal)
            {
                reason = string.Empty;
                HasPermissionAttribute[] securityAttributes = method.GetCustomAttributes(typeof(HasPermissionAttribute)).Cast<HasPermissionAttribute>().ToArray();
                if (securityAttributes.Length != 0)
                {
                    retVal = VerifyPermissions(userIdentity, securityAttributes, out reason);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Verfies access to a specific event
        /// </summary>
        /// <param name="objectEvent">the event that is requested for subscription</param>
        /// <param name="objectName">the object name for which to check the permission</param>
        /// <param name="userIdentity">the authenticated user that requests access to the provided event</param>
        /// <param name="reason">provides a reason when the access was denied</param>
        /// <returns>a value indicating whether to allow the provieded event-access</returns>
        public bool VerifyAccess(EventInfo objectEvent, string objectName, IIdentity userIdentity, out string reason)
        {
            bool retVal = VerifyAccess(objectName, userIdentity, out reason);
            if (retVal)
            {
                reason = string.Empty;
                HasPermissionAttribute[] securityAttributes = objectEvent.GetCustomAttributes(typeof(HasPermissionAttribute)).Cast<HasPermissionAttribute>().ToArray();
                if (securityAttributes.Length != 0)
                {
                    retVal = VerifyPermissions(userIdentity, securityAttributes, out reason);
                }
            }

            return retVal;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Verifies the permissions of a user for a set of assigned HasPermission attributes
        /// </summary>
        /// <param name="userIdentity">the identity for which to check the permissions</param>
        /// <param name="securityAttributes">the securityattributes found on the desired class or member</param>
        /// <param name="reason">provides a reason, why access was denied</param>
        /// <returns>a value whether the access can be granted</returns>
        private bool VerifyPermissions(IIdentity userIdentity, HasPermissionAttribute[] securityAttributes, out string reason)
        {
            bool retVal = true;
            reason = string.Empty;
            var requiredPermissions = securityAttributes.SelectMany(n => n.RequiredPermissions).ToArray();
            if (requiredPermissions.Length != 0)
            {
                string[] labels = nameMapper.GetUserLabels(userIdentity);
                var permissions = securityRepo.GetPermissions(labels, userIdentity.AuthenticationType).ToArray();
                retVal = (from t in requiredPermissions join p in permissions on t equals p.PermissionName select t).Any();
                if (!retVal)
                {
                    reason = $"Access Denied! Checked Permissions: {string.Join(", ", requiredPermissions)}. Effective Permissions on User (Authentication-Mode: {userIdentity.AuthenticationType}): {string.Join(", ", from t in permissions select t.PermissionName)}.";
                }
            }

            return retVal;

        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
