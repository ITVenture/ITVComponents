using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Client;
using ITVComponents.InterProcessCommunication.Shared.Base;

namespace ITVComponents.InterProcessCommunication.MessagingShared.DependencyExtensions
{
    /// <summary>
    /// Provides a connection factory to communicate with processes on the same or on remote machines
    /// </summary>
    public interface IClientFactory
    {
        /// <summary>
        /// Gets a message-client instance that is linked to a specific service
        /// </summary>
        /// <param name="serviceName">the name of the target-service</param>
        /// <param name="useEvents">indicates whether to allow the service to raise events to the created client</param>
        /// <returns>a client instance that is capable of communicating with the target service</returns>
        IBidirectionalClient GetClient(string serviceName, bool useEvents);
    }
}
