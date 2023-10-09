using ITVComponents.InterProcessCommunication.Grpc.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory;
using ITVComponents.InterProcessCommunication.Shared.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Factory
{
    internal class GrpcHubConnectionFactory : IHubConnectionFactory
    {
        private readonly string hubAddress;
        private readonly string targetService;
        private readonly IHubClientConfigurator configurator;
        private readonly bool useEvents;
        private readonly ICustomServerSecurity security;
        private readonly bool clientMode;

        public GrpcHubConnectionFactory(string hubAddress, string targetService, IHubClientConfigurator configurator, bool useEvents)
        {
            this.hubAddress = hubAddress;
            this.targetService = targetService;
            this.configurator = configurator;
            this.useEvents = useEvents;
            clientMode = true;
        }

        public GrpcHubConnectionFactory(string hubAddress, string serviceName, IHubClientConfigurator configurator, ICustomServerSecurity security)
        {
            this.hubAddress = hubAddress;
            this.targetService = serviceName;
            this.configurator = configurator;
            this.security = security;
            clientMode = false;
        }

        /// <summary>
        /// Gets the target hub that this hub-connector connects to
        /// </summary>
        public string Target => hubAddress;

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
                return !useEvents ? new ServiceHubConsumer(hubAddress, configurator, targetService) : new ServiceHubConsumer(hubAddress, $"{Guid.NewGuid()}{tail}", configurator, targetService);
            }
            else
            {
                return new ServiceHubConsumer(hubAddress, targetService, configurator, security);
            }
        }
    }
}
