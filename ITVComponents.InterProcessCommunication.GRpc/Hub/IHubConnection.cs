using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub
{
    public interface IHubConnection:IDisposable, IOperationalProvider
    {
        /// <summary>
        /// Gets the ServiceName behind this HubConnection
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Gets a value indicating whether this hubConnection was initialized
        /// </summary>
        internal bool Initialized { get; }

        /// <summary>
        /// Initializes this ServiceHub instance
        /// </summary>
        internal void Initialize();

        /// <summary>
        /// Invokes an action on the given ServiceName
        /// </summary>
        /// <param name="serviceName">the name of the target-service</param>
        /// <param name="serviceMessage">the message to send to the service</param>
        /// <returns>the response-message that came from the remote service</returns>
        string InvokeService(string serviceName, string serviceMessage);

        /// <summary>
        /// Invokes an action on the given ServiceName
        /// </summary>
        /// <param name="serviceName">the name of the target-service</param>
        /// <param name="serviceMessage">the message to send to the service</param>
        /// <returns>the response-message that came from the remote service</returns>
        Task<string> InvokeServiceAsync(string serviceName, string serviceMessage);

        /// <summary>
        /// Checks whether the given service is available
        /// </summary>
        /// <param name="serviceName">the name of the service</param>
        /// <returns>a value indicating whether the requested service is available</returns>
        bool DiscoverService(string serviceName);

        /// <summary>
        /// Is raised when a message has arrived. A Service or client object can process the event and return an appropriate message or exception
        /// </summary>
        event EventHandler<MessageArrivedEventArgs> MessageArrived;
    }

    public class MessageArrivedEventArgs : EventArgs
    {
        /// <summary>
        /// The Request message that was received from the hub
        /// </summary>
        public string Message { get;set; }

        /// <summary>
        /// Indicates whether the message was processed and the action completed
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// the Target-service for which this message is destined
        /// </summary>
        public string TargetService { get; set; }

        /// <summary>
        /// The Service-Response that must be sent to the client
        /// </summary>
        public string Response { get; set; }

        /// <summary>
        /// If processing lead to an exception, set this property
        /// </summary>
        public SerializedException Error { get; set; }

        /// <summary>
        /// Gets or sets the Identity of the HubConsumer that sent this message
        /// </summary>
        public IIdentity HubUser { get; set; }
    }
}
