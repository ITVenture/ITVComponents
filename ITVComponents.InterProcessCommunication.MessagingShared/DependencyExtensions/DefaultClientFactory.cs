using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Client;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.Shared.Base;

namespace ITVComponents.InterProcessCommunication.MessagingShared.DependencyExtensions
{
    public class DefaultClientFactory:IClientFactory
    {
        private readonly IServiceHubProvider serviceProvider;
        private readonly IIdentityProvider identityProvider;

        public DefaultClientFactory(IServiceHubProvider serviceProvider, IIdentityProvider identityProvider)
        {
            this.serviceProvider = serviceProvider;
            this.identityProvider = identityProvider;
        }

        public IBidirectionalClient GetClient(string serviceName, bool useEvents)
        {
            return new MessageClient(serviceProvider, serviceName, identityProvider, useEvents);
        }
    }
}
