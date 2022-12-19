using ITVComponents.InterProcessCommunication.InMemory.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory;
using ITVComponents.InterProcessCommunication.Shared.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Factory
{
    internal class MemoryHubConnectionFactory : IHubConnectionFactory
    {
        private readonly string hubAddress;
        private readonly string targetService;
        private IHubFactory hubFactory;
        private readonly bool useEvents;
        private readonly ICustomServerSecurity security;
        private readonly bool clientMode;

        public MemoryHubConnectionFactory(string hubAddress, string targetService, IHubFactory factory, bool useEvents)
        {
            this.hubAddress = hubAddress;
            this.targetService = targetService;
            this.hubFactory= factory;
            this.useEvents = useEvents;
            clientMode = true;
        }

        public MemoryHubConnectionFactory(string hubAddress, string serviceName, IHubFactory factory, ICustomServerSecurity security)
        {
            this.hubAddress = hubAddress;
            this.targetService = serviceName;
            this.hubFactory = factory;
            this.security = security;
            clientMode = false;
        }

        public IHubConnection CreateConnection()
        {
            var tailId = targetService.IndexOf("@");
            string tail = null;
            if (tailId != -1)
            {
                tail = targetService.Substring(tailId);
            }

            if (clientMode)
            {
                return !useEvents ? new InMemoryServiceHubConsumer(hubAddress, hubFactory, targetService) : new InMemoryServiceHubConsumer(hubAddress, $"{Guid.NewGuid()}{tail}", hubFactory, targetService);
            }

            return new InMemoryServiceHubConsumer(hubAddress, targetService, hubFactory, security);
        }
    }
}
