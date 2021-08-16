using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Client
{
    public interface IServiceHubClientChannel : IDisposable
    {
        ServiceOperationResponseMessage ConsumeService(ServerOperationMessage serverOperationMessage);
        Task<ServiceOperationResponseMessage> ConsumeServiceAsync(ServerOperationMessage serverOperationMessage);
        ServiceDiscoverResponseMessage DiscoverService(ServiceDiscoverMessage serviceDiscoverMessage);
        void ServiceTick(ServiceSessionOperationMessage session);
        RegisterServiceResponseMessage RegisterService(RegisterServiceMessage mySvc);
        void ServiceReady(ServiceSessionOperationMessage session);
        void CommitServiceOperation(ServiceOperationResponseMessage ret);
        event EventHandler Broken;
    }
}
