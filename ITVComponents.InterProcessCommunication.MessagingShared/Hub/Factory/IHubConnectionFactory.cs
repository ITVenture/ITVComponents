using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory
{
    public interface IHubConnectionFactory
    {
        /// <summary>
        /// Gets the target hub that this hub-connector connects to
        /// </summary>
        string Target { get; }

        /// <summary>
        /// Initializes a new Hub-Connection object
        /// </summary>
        /// <returns>the connection to a remote hub</returns>
        IHubConnection CreateConnection();
    }
}
