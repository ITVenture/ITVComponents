using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.MessagingShared
{
    public class MessageServiceHub: IServiceHubProvider, IPlugin
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
            OnDisposed();
        }

        public string UniqueName { get; set; }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
