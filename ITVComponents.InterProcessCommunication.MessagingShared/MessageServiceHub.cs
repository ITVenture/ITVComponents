using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.MessagingShared
{
    public class MessageServiceHub: IServiceHubProvider
    {
        private static MessageServiceHub instance;

        public IEndPointBroker Broker { get; private set; }

        public MessageServiceHub()
        {
            if (instance != null)
            {
                throw new NotSupportedException("Can have only one active Instance of MessageServiceHub at a time");
            }

            Broker = new EndPointBroker();

        }

        public void Dispose()
        {
            Broker.Dispose();
            Broker = null;
        }
    }
}
