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
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.WebToolkitOverrides;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.MessagingShared.Security.PrincipalProviders;
using ITVComponents.Threading;
using ITVComponents.WebCoreToolkit.Security.UserMappers;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Factory
{
    public class AuthHubFactory : IHubFactory
    {
        private IIdentityProvider provider;

        public string UniqueName { get; set; }

        public AuthHubFactory()
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
            return new AuthServiceHubIm(backend, new SimpleUserNameMapper(), new JsonSettingsSecurityRepository());
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
            var ttl = 15;
            if (initialChannel.IsGlobal)
            {
                guid = $@"Global\{guid}";
            }

            ReConnectChannel(guid, ttl, initialChannel);
            var retVal = new MemoryServiceChannel(guid, true, MscMode.Client, ttl, provider);
            return new ServiceClient(retVal, initialChannel, this, backStream);
        }

        public void ReConnectChannel(string name, int ttl, IMemoryChannel initialChannel)
        {
            initialChannel.Write(new ConnectionRequest
                    { ProposedGuid = name, Ttl = ttl, User = JsonHelper.ToJsonStrongTyped(provider.CurrentIdentity) });
            Task.Delay(1500).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
