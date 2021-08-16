using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub
{
    public interface IServiceHubProvider
    {
        /// <summary>
        /// Gets the EndPoint broker instance that manages all traffic between the communication endpoints
        /// </summary>
        EndPointBroker Broker { get; }
    }
}
