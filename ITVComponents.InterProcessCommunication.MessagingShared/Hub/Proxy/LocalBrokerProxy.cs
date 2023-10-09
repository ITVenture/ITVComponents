using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Proxy
{
    public class LocalBrokerProxy
    {
        private readonly IServiceHubProvider serviceHubProvider;
        private readonly IServiceProvider services;

        public LocalBrokerProxy(IServiceHubProvider serviceHubProvider, IServiceProvider services)
        {
            this.serviceHubProvider = serviceHubProvider;
            this.services = services;
        }

        public LocalBrokerProxy(IServiceHubProvider serviceHubProvider):this(serviceHubProvider, null)
        {
        }

        public Task<ServiceOperationResponseMessage> SendMessageToServer(ServerOperationMessage message, IIdentity authenticatedUser)
        {
            if (string.IsNullOrEmpty(message.HubUser) && authenticatedUser is ClaimsIdentity cli)
            {
                message.HubUser = JsonHelper.ToJsonStrongTyped(cli.ForTransfer());
            }

            return serviceHubProvider.Broker.SendMessageToServer(message, services);
        }

        public void AddServiceTag(ServiceSessionOperationMessage serviceSession, string tagName, string value)
        {
            serviceHubProvider.Broker.AddServiceTag(serviceSession, tagName, value);
        }

        public string GetServiceByTag(string tagName, string tagValue)
        {
            return serviceHubProvider.Broker.GetServiceByTag(tagName, tagValue);
        }

        public Task<ServerOperationMessage> NextRequest(ServiceSessionOperationMessage serviceSession)
        {
            return serviceHubProvider.Broker.NextRequest(serviceSession);
        }

        public void FailOperation(ServiceSessionOperationMessage serviceSession, string req, Exception ex)
        {
            serviceHubProvider.Broker.FailOperation(serviceSession, req, ex);
        }

        public void CommitServerOperation(ServiceOperationResponseMessage response)
        {
            serviceHubProvider.Broker.CommitServerOperation(response);
        }

        public RegisterServiceResponseMessage RegisterService(RegisterServiceMessage registration)
        {
            return serviceHubProvider.Broker.RegisterService(registration);
        }

        public bool TryUnRegisterService(ServiceSessionOperationMessage msg)
        {
            return serviceHubProvider.Broker.TryUnRegisterService(msg);
        }

        public ServiceTickResponseMessage Tick(ServiceSessionOperationMessage request)
        {
            return serviceHubProvider.Broker.Tick(request);
        }

        public ServiceDiscoverResponseMessage DiscoverService(ServiceDiscoverMessage request)
        {
            return serviceHubProvider.Broker.DiscoverService(request);
        }

        public void UnsafeServerDrop(string serviceName)
        {
            serviceHubProvider.Broker.UnsafeServerDrop(serviceName);
        }
    }
}
