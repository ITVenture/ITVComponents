using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Internal
{
    internal class OperationWaitHandle
    {
        public OperationWaitHandle(ServerOperationMessage op)
        {
            ClientRequest = op;
            if (!op.TickBack)
            {
                ServerResponse = new TaskCompletionSource<ServiceOperationResponseMessage>();
            }
        }
        public ServerOperationMessage ClientRequest { get; }

        public TaskCompletionSource<ServiceOperationResponseMessage> ServerResponse{get;}
    }
}
