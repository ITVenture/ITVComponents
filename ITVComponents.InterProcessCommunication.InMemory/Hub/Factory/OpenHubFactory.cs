using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Channels;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Client;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Hubs;
using ITVComponents.InterProcessCommunication.InMemory.Hub.ProtoExtensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.MessagingShared.Security.PrincipalProviders;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Factory
{
    public class OpenHubFactory:IHubFactory
    {
        private IIdentityProvider provider;

        public string UniqueName { get; set; }

        public OpenHubFactory()
        {
            provider = new IdentityFromWindowsProvider();
        }

        /// <summary>
        /// Creates a ServiceHub object that is able to process incoming messages
        /// </summary>
        /// <param name="backend">the backend that is used to exchange messages</param>
        /// <returns>the create hub instance</returns>
        public IServiceHub CreateHub(IServiceHubProvider backend)
        {
            return new OpenServiceHubIm(backend);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public IServiceHubClientChannel CreateClient(string serviceAddr, AsyncBackStream backStream)
        {
            MemoryServiceChannel initialChannel = new MemoryServiceChannel(serviceAddr, false, MscMode.Client, 6000, provider);
            var guid = Guid.NewGuid().ToString("N");
            if (initialChannel.IsGlobal)
            {
                guid = $@"Global\{guid}";
            }

            var retVal = new MemoryServiceChannel(guid, true, MscMode.Client, 15, provider);
            ReConnectChannel(retVal, initialChannel);
            return new ServiceClient(retVal, initialChannel, this, backStream);
        }

        public void ReConnectChannel(IMemoryChannel comm, IMemoryChannel initialChannel)
        {
            initialChannel.Write(new ConnectionRequest { ProposedGuid = comm.Name, Ttl = comm.Ttl, User = JsonHelper.ToJsonStrongTyped(provider.CurrentIdentity) });
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
