using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Channels
{
    internal class OperationWaitHandle
    {
        public OperationWaitHandle()
        {
            ServerResponse = new TaskCompletionSource<object>();
        }
        public TaskCompletionSource<object> ServerResponse{get;}
    }
}
