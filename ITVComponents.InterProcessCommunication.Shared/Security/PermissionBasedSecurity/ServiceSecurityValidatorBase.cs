using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.Security.PermissionBasedSecurity
{
    /// <summary>
    /// Permission driven Security Validator for service objects
    /// </summary>
    public abstract class ServiceSecurityValidatorBase:ICustomServerSecurity, IPlugin
    {
        protected readonly PluginFactory factory;

        /// <summary>
        /// Initializes a new instance of the ServiceSecurityValidator class
        /// </summary>
        /// <param name="factory">the pluginfactory that provides objects that may be accessed by clients</param>
        public ServiceSecurityValidatorBase(PluginFactory factory)
        {
            this.factory = factory;
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

        /// <summary>
        /// Gets the custom properties for a specific identity
        /// </summary>
        /// <param name="identity">the identity for which to get the custom properties</param>
        /// <returns>an enuerable containing all custom properties for the given identity</returns>
        public IEnumerable<KeyValuePair<string,string>> GetCustomProperties(IIdentity identity)
        {
            return SelectCustomProperties(identity);
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
        protected virtual bool VerifyPermissions(IIdentity userIdentity, HasPermissionAttribute[] securityAttributes, out string reason)
        {
            var requiredPermissions = securityAttributes.SelectMany(n => n.RequiredPermissions).ToArray();
            reason = string.Empty;
            var retVal = VerifyPermissionLabels(userIdentity, requiredPermissions, out var effectivePermissions);
            effectivePermissions ??= new string[0];
            if (!retVal)
            {
                reason = $"Access Denied! Checked Permissions: {string.Join(", ", requiredPermissions)}. Effective Permissions on User (Authentication-Mode: {userIdentity.AuthenticationType}): {string.Join(", ", from t in effectivePermissions select t)}.";
            }

            return retVal;
        }

        /// <summary>
        /// Gets the custom properties for a specific identity
        /// </summary>
        /// <param name="identity">the identity for which to get the custom properties</param>
        /// <returns>an enuerable containing all custom properties for the given identity</returns>
        protected abstract IEnumerable<KeyValuePair<string,string>> SelectCustomProperties(IIdentity identity);

        /// <summary>
        /// Verifies the permissions of a user for a set of assigned HasPermission attributes
        /// </summary>
        /// <param name="userIdentity">the identity for which to check the permissions</param>
        /// <param name="requiredPermissions">a list of permissions that any should match the set of granted permissions for the current user</param>
        /// <param name="effectivePermissions">provides a list of permissions that are granted to the authenticated user</param>
        /// <returns>a value whether the access can be granted</returns>
        protected abstract bool VerifyPermissionLabels(IIdentity userIdentity, string[] requiredPermissions, out string[] effectivePermissions);

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
