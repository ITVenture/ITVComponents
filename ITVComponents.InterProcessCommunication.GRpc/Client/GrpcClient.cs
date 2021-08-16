using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Hub;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Factory;
using ITVComponents.InterProcessCommunication.Grpc.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.Grpc.Security;
using ITVComponents.InterProcessCommunication.MessagingShared.Client;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.InterProcessCommunication.Grpc.Client
{
    public class GrpcClient : MessageClient
    {
        public GrpcClient(string hubAddress, IHubClientConfigurator configurator, string targetService, bool useEvents) :
            base(new GrpcHubConnectionFactory(hubAddress, targetService, configurator, useEvents), targetService, null, useEvents)
        {
        }

        public GrpcClient(IServiceHubProvider serviceHub, string targetService, bool useEvents) : base(serviceHub, targetService, useEvents)
        {
        }

        public GrpcClient(string hubAddress, IHubClientConfigurator configurator, string targetService, IIdentityProvider identityProvider, bool useEvents) :
            base(new GrpcHubConnectionFactory(hubAddress, targetService, configurator, useEvents), targetService, identityProvider, useEvents)
        {
        }

        public GrpcClient(IServiceHubProvider serviceHub, string targetService, IIdentityProvider identityProvider, bool useEvents) : base(serviceHub, targetService, identityProvider, useEvents)
        {
        }
    }
}