using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Channels;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub
{
    public interface IServiceHub
    {
        Task CommitServiceOperation(ServiceOperationResponseMessage request, DataTransferContext context);

        Task<ServiceOperationResponseMessage> ConsumeService(ServerOperationMessage request, DataTransferContext context);

        Task<ServiceDiscoverResponseMessage> DiscoverService(ServiceDiscoverMessage request, DataTransferContext context);

        Task<RegisterServiceResponseMessage> RegisterService(RegisterServiceMessage request, DataTransferContext context);

        Task ServiceReady(ServiceSessionOperationMessage request, IMemoryChannel channel, DataTransferContext context);

        Task<ServiceTickResponseMessage> ServiceTick(ServiceSessionOperationMessage request, DataTransferContext context);
    }
}
