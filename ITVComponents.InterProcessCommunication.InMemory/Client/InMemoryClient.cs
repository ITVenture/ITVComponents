using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Client;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Factory;
using ITVComponents.InterProcessCommunication.InMemory.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.MessagingShared.Client;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.InterProcessCommunication.InMemory.Client
{
    public class InMemoryClient:MessageClient
    {
        public InMemoryClient(string hubAddress, IHubFactory hubFactory, string targetService, bool useEvents) :
            base(new MemoryHubConnectionFactory(hubAddress,targetService,hubFactory,useEvents), targetService, null, useEvents)
        {
        }

        public InMemoryClient(IServiceHubProvider serviceHub, string targetService, bool useEvents):base(serviceHub, targetService,useEvents)
        {
        }

        public InMemoryClient(string hubAddress, IHubFactory hubFactory, string targetService, IIdentityProvider identityProvider, bool useEvents):
            base(new MemoryHubConnectionFactory(hubAddress, targetService, hubFactory, useEvents), targetService, identityProvider, useEvents)
        {
        }

        public InMemoryClient(IServiceHubProvider serviceHub, string targetService, IIdentityProvider identityProvider, bool useEvents) : base(serviceHub, targetService, identityProvider, useEvents)
        {
        }
    }
}
