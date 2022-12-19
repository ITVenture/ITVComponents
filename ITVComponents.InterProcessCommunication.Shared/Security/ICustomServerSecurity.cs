using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    public interface ICustomServerSecurity
    {
        /// <summary>
        /// Verifies access to a plugin for the specified identity
        /// </summary>
        /// <param name="objectName">the object for which to verify the access</param>
        /// <param name="userIdentity">the identity for which to check the access</param>
        /// <param name="reason">returns a reason, why the access was denied</param>
        /// <returns>a value indicating whether the provided user has access to the requested object</returns>
        bool VerifyAccess(string objectName, IIdentity userIdentity, out string reason);

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
        bool VerifyAccess(PropertyInfo property, string objectName, object[] arguments, IIdentity userIdentity, bool write, out string reason);

        /// <summary>
        /// Verifies execution access for a specific method
        /// </summary>
        /// <param name="method">the method that was requested to execute by a caller</param>
        /// <param name="objectName">the object name for which to check the permission</param>
        /// <param name="arguments">the arguments for the called method</param>
        /// <param name="userIdentity">the authenticated user that wishes to execute the provided method</param>
        /// <param name="reason">provides a reason when the access was denied</param>
        /// <returns>a value indicating whether to allow the provided method-call</returns>
        bool VerifyAccess(MethodInfo method, string objectName, object[] arguments, IIdentity userIdentity, out string reason);

        /// <summary>
        /// Verfies access to a specific event
        /// </summary>
        /// <param name="objectEvent">the event that is requested for subscription</param>
        /// <param name="objectName">the object name for which to check the permission</param>
        /// <param name="userIdentity">the authenticated user that requests access to the provided event</param>
        /// <param name="reason">provides a reason when the access was denied</param>
        /// <returns>a value indicating whether to allow the provieded event-access</returns>
        bool VerifyAccess(EventInfo objectEvent, string objectName, IIdentity userIdentity, out string reason);

        /// <summary>
        /// Gets the custom properties for a specific identity object
        /// </summary>
        /// <param name="identity">the identity for which to get the custom properties</param>
        /// <returns>an enumerable containing all custom properties for the given identity</returns>
        IEnumerable<KeyValuePair<string,string>> GetCustomProperties(IIdentity identity);

        /// <summary>
        /// Attaches a Factory-Wrapper that can be used to receive or check for plugins
        /// </summary>
        /// <param name="factory">the factory-wrapper to attach</param>
        void Attach(IFactoryWrapper factory);
    }
}