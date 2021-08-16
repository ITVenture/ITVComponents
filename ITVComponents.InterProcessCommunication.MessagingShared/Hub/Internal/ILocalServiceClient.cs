using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

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
        /// Processes a message that was received from a remote client
        /// </summary>
        /// <param name="message">the message that was received from the remote host</param>
        /// <returns>a response message that was generated as result of the received message</returns>
        ServiceOperationResponseMessage ProcessMessage(ServerOperationMessage message);
    }
}
