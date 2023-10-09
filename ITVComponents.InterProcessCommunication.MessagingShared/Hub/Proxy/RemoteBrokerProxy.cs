using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Client;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Proxy
{
    public class RemoteBrokerProxy:IEndPointBroker, IDeferredInit
    {
        public string Name { get; }
        private readonly IBidirectionalClient client;
        private readonly string decoratedName;
        private readonly IServiceHubProvider parent;

        private IRemoteBrokerProxy remoteProxy;

        private string name;

        public RemoteBrokerProxy(IBidirectionalClient client, string decoratedName, IServiceHubProvider parent, string name)
        {
            Name = name;
            this.client = client;
            this.decoratedName = decoratedName;
            this.parent = parent;
        }

        //public string UniqueName { get; set; }

        private IRemoteBrokerProxy RemoteProxy => remoteProxy ??= client.CreateProxy<IRemoteBrokerProxy>(decoratedName);

        public async Task<ServiceOperationResponseMessage> SendMessageToServer(ServerOperationMessage message, IServiceProvider services)
        {
            CleanService(message);
            var retVal = await RemoteProxy.SendMessageToServer(message);
            EnrichService(retVal);
            EnrichResponder(retVal);
            return retVal;
        }

        public void AddServiceTag(ServiceSessionOperationMessage serviceSession, string tagName, string value)
        {
            CleanService(serviceSession);
            CleanResponder(serviceSession);
            RemoteProxy.AddServiceTag(serviceSession, tagName, value);
        }

        public string GetServiceByTag(string tagName, string tagValue)
        {
            return RemoteProxy.GetServiceByTag(tagName, tagValue);
        }

        public async Task<ServerOperationMessage> NextRequest(ServiceSessionOperationMessage serviceSession)
        {
            CleanService(serviceSession);
            CleanResponder(serviceSession);
            var retVal = await RemoteProxy.NextRequest(serviceSession);
            EnrichService(retVal);
            return retVal;
        }

        public void FailOperation(ServiceSessionOperationMessage serviceSession, string req, Exception ex)
        {
            CleanService(serviceSession);
            CleanResponder(serviceSession);
            RemoteProxy.FailOperation(serviceSession, req, ex);
        }

        public void CommitServerOperation(ServiceOperationResponseMessage response)
        {
            CleanService(response);
            CleanResponder(response);
            RemoteProxy.CommitServerOperation(response);
        }

        public RegisterServiceResponseMessage RegisterService(RegisterServiceMessage registration)
        {
            CleanService(registration);
            CleanResponder(registration);
            return RemoteProxy.RegisterService(registration);
        }

        public bool TryUnRegisterService(ServiceSessionOperationMessage msg)
        {
            CleanService(msg);
            CleanResponder(msg);
            return RemoteProxy.TryUnRegisterService(msg);
        }

        public ServiceTickResponseMessage Tick(ServiceSessionOperationMessage request)
        {
            CleanService(request);
            CleanResponder(request);
            return RemoteProxy.Tick(request);
        }

        public ServiceDiscoverResponseMessage DiscoverService(ServiceDiscoverMessage request)
        {
            CleanService(request);
            var retVal = RemoteProxy.DiscoverService(request);
            EnrichService(retVal);
            return retVal;
        }

        public void UnsafeServerDrop(string serviceName)
        {
            RemoteProxy.UnsafeServerDrop(CleanService(serviceName));
        }

        private void CleanService(IServiceMessage message)
        {
            message.TargetService = CleanService(message.TargetService);
        }

        private void CleanService(IServerMessage message)
        {
            message.ServiceName = CleanService(message.ServiceName);
        }

        private string CleanService(string serviceName)
        {
            if (!string.IsNullOrEmpty(serviceName) && serviceName.EndsWith($"@{Name}"))
            {
                return serviceName.Substring(0, serviceName.Length - (Name.Length + 1));
            }

            return serviceName;
        }

        private void CleanResponder(IResponderMessage message)
        {
            message.ResponderFor = CleanService(message.ResponderFor);
        }

        private void EnrichService(IServiceMessage message)
        {
            message.TargetService= EnrichService(message.TargetService);
        }

        private void EnrichService(IServerMessage message)
        {
            message.ServiceName=EnrichService(message.ServiceName);
        }

        private void EnrichResponder(IResponderMessage message)
        {
            message.ResponderFor = EnrichService(message.ResponderFor);
        }

        private string EnrichService(string serviceName)
        {
            if (!string.IsNullOrEmpty(serviceName))
            {
                return $"{serviceName}@{Name}";
            }

            return serviceName;
        }

        public bool Initialized { get; private set; }
        public bool ForceImmediateInitialization => false;
        public void Initialize()
        {
            if (!Initialized)
            {
                if (parent.Broker is EndPointBroker broker)
                {
                    broker.RegisterBrokerProxy(Name, this);
                    Initialized = true;
                }
                else
                {
                    throw new InvalidOperationException("An EndPoint broker is required to install a proper proxy");
                }
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public virtual void Dispose()
        {
        }
    }
}
