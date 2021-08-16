using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Channels;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Client;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Factory
{
    public interface IHubFactory:IPlugin
    {
        /// <summary>
        /// Creates a ServiceHub object that is able to process incoming messages
        /// </summary>
        /// <param name="backend">the backend that is used to exchange messages</param>
        /// <returns>the create hub instance</returns>
        IServiceHub CreateHub(IServiceHubProvider backend);

        /// <summary>
        /// Creates a new ServiceClient based on the given base-Address
        /// </summary>
        /// <param name="serviceAddr">the base-address that listens to connection requests</param>
        /// <param name="messageStream">a back-stream that is used to read data that is received from the service</param>
        /// <returns>a ServiceHubClientChannel object that is capable of sending and receiveing messages from the hub-service</returns>
        IServiceHubClientChannel CreateClient(string serviceAddr, AsyncBackStream messageStream);

        /// <summary>
        /// Re-Connects the given channel. A Connection-Registration message is sent.
        /// </summary>
        /// <param name="comm">the communication channel</param>
        /// <param name="initialChannel">the initialization channel</param>
        void ReConnectChannel(IMemoryChannel comm, IMemoryChannel initialChannel);
    }
}
