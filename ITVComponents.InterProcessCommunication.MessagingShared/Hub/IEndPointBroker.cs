using System;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub
{
    public interface IEndPointBroker:IDisposable
    {
        /// <summary>
        /// Sends a message to the given service
        /// </summary>
        /// <param name="message">the message to send to the given service</param>
        /// <param name="services">the services-collection identifying services that can be used by the server-object in order to retrieve requested instances</param>
        /// <returns>an operation response indicating whether the operation could be performed and the value it resulted to.</returns>
        Task<ServiceOperationResponseMessage> SendMessageToServer(ServerOperationMessage message, IServiceProvider services);

        /// <summary>
        /// Adds a Service-Tag to an existing service
        /// </summary>
        /// <param name="serviceSession">the service-session for which to add a tag</param>
        /// <param name="tagName">the tag-key of the given value</param>
        /// <param name="value">the tag-value</param>
        void AddServiceTag(ServiceSessionOperationMessage serviceSession, string tagName, string value);

        /// <summary>
        /// Gets the ServiceName by its tag
        /// </summary>
        /// <param name="tagName">the tag-name for search</param>
        /// <param name="tagValue">the value</param>
        /// <returns>the first service that applies to the given filter</returns>
        string GetServiceByTag(string tagName, string tagValue);

        /// <summary>
        /// Reads the next Operation-Request from this EndpointBroker object
        /// </summary>
        /// <param name="serviceSession">the serviceSession from which to read the information</param>
        /// <returns>the ServerOperation message that was sent by a client</returns>
        Task<ServerOperationMessage> NextRequest(ServiceSessionOperationMessage serviceSession);
        
        /// <summary>
        /// Responds with a Fail-message on the given operation object
        /// </summary>
        /// <param name="serviceSession">the operation that was orignally sent</param>
        /// <param name="req">when the operation requires a response, the requestId is provided in the req parameter</param>
        /// <param name="ex">the exception that occurred during the execution of the operation</param>
        void FailOperation(ServiceSessionOperationMessage serviceSession, string req, Exception ex);
        
        /// <summary>
        /// Commits the target operation
        /// </summary>
        /// <param name="response">the response-message that corresponds to a specific request of a client</param>
        void CommitServerOperation(ServiceOperationResponseMessage response);
        
        /// <summary>
        /// Registers a service on this hub
        /// </summary>
        /// <param name="registration">the service registration message that contains meta-data of the service to register</param>
        /// <returns>a message containing information about success or failure of the registration</returns>
        RegisterServiceResponseMessage RegisterService(RegisterServiceMessage registration);
        
        /// <summary>
        /// Save-Unregister method of a service
        /// </summary>
        /// <param name="msg">the service Session operation message containing the metadata of the service that needs to be removed from the list of active services</param>
        /// <returns>a value indicating whether the removal of the service was successful</returns>
        bool TryUnRegisterService(ServiceSessionOperationMessage msg);
        
        /// <summary>
        /// Performs a tick-request on this broker. This is used by services to indicate that they are still alive
        /// </summary>
        /// <param name="request">the tick-request from a specific service</param>
        /// <returns>the accurate response for a tick of the given service</returns>
        ServiceTickResponseMessage Tick(ServiceSessionOperationMessage request);
        
        /// <summary>
        /// Discovers a specific service
        /// </summary>
        /// <param name="request">all known metadata about the known service</param>
        /// <returns>a discovery message containing information about the service and if it was found</returns>
        ServiceDiscoverResponseMessage DiscoverService(ServiceDiscoverMessage request);
        
        /// <summary>
        /// Forces the removal of a specific service from the list of active services
        /// </summary>
        /// <param name="serviceName">the service-name that is no longer availalbe</param>
        void UnsafeServerDrop(string serviceName);
    }
}