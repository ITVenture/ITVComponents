using System;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.Base
{
    public interface IBaseClient:IOperationalProvider
    {
        /// <summary>
        /// Gets the target address, this client connects to
        /// </summary>
        string Target { get; }

        /// <summary>
        /// Gets an object that provides locking functionality for this Consumer object
        /// </summary>
        object Sync { get; }

        /// <summary>
        /// Checks on the service whether a specific object exists and is ready to be used
        /// </summary>
        /// <param name="uniqueObjectName">the unique object name of the demanded object</param>
        /// <returns>a value indicating whether the remote service has committed the existance of the demanded object</returns>
        ObjectAvailabilityResult CheckRemoteObjectAvailability(string uniqueObjectName);

        /// <summary>
        /// Removes an object on the Server for the given objectName. Works only for extended Proxy-objects
        /// </summary>
        /// <param name="uniqueObjectName">the objectName to remove from the list of extension.proxies</param>
        /// <returns>a value indicating whether the object could be removed successfully</returns>
        bool AbandonObject(string uniqueObjectName);

        /// <summary>
        /// Calls a remote method on the server
        /// </summary>
        /// <param name="objectName">the name of the executing object</param>
        /// <param name="methodName">the method that must be executed</param>
        /// <param name="arguments">arguments for the method call</param>
        /// <returns>a call-wrapper that will be triggered back, when the execution has finished</returns>
        object CallRemoteMethod(string objectName, string methodName, object[] arguments);

        /// <summary>
        /// Calls a remote method on the server
        /// </summary>
        /// <param name="objectName">the name of the executing object</param>
        /// <param name="methodName">the method that must be executed</param>
        /// <param name="arguments">arguments for the method call</param>
        /// <returns>a call-wrapper that will be triggered back, when the execution has finished</returns>
        Task<object> CallRemoteMethodAsync(string objectName, string methodName, object[] arguments);

        /// <summary>
        /// Gets a property of the targetobject with the provided name
        /// </summary>
        /// <param name="objectName">the target object from which to get a property</param>
        /// <param name="propertyName">the name of the property to read</param>
        /// <param name="index">the index used for indexed properties</param>
        /// <returns>the value of the requested property</returns>
        object GetProperty(string objectName, string propertyName, object[] index);

        /// <summary>
        /// Sets the value of a property on a target object
        /// </summary>
        /// <param name="objectName">the target object on which to set a property</param>
        /// <param name="propertyName">the propertyname to set</param>
        /// <param name="index">the index used for indexed properties</param>
        /// <param name="value">the new value for the specified property</param>
        void SetProperty(string objectName, string propertyName, object[] index, object value);

        /// <summary>
        /// Creates a proxy object for the requested objectName
        /// </summary>
        /// <typeparam name="T">the Client-interface type for the given objectName</typeparam>
        /// <param name="objectName">the name of the remote-Object to wrap</param>
        /// <returns>a Proxy - object that wraps the requested object</returns>
        T CreateProxy<T>(string objectName) where T : class;

        /// <summary>
        /// Validates the server connection
        /// </summary>
        /// <returns>a value indicating whether the connection to the service object is up and running</returns>
        bool ValidateConnection();
    }
}
