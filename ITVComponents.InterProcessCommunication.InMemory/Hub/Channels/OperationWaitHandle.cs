using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Channels
{
    internal class OperationWaitHandle
    {
        public OperationWaitHandle()
        {
            ServerResponse = new TaskCompletionSource<IProtocolMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public TaskCompletionSource<IProtocolMessage> ServerResponse{get;}
    }
}
