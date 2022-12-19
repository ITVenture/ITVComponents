using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using System;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Internal
{
    /// <summary>
    /// A local implementation of a HubClient
    /// </summary>
    internal interface ILocalServiceClient
    {
        /// <summary>
        /// Gets the ServiceName behind this HubConnection
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Gets the Service that is consumed by this service
        /// </summary>
        string ConsumedService { get; }

        /// <summary>
        /// Processes a message that was received from a remote client
        /// </summary>
        /// <param name="message">the message that was received from the remote host</param>
        /// <returns>a response message that was generated as result of the received message</returns>
        ServiceOperationResponseMessage ProcessMessage(ServerOperationMessage message, IServiceProvider services);
    }
}
