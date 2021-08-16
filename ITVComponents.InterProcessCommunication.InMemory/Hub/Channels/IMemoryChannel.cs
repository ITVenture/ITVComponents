using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Channels
{
    public interface IMemoryChannel:IDisposable
    {
        string Name { get; }
        bool Connected { get; }
        CancellationToken CancellationToken { get; }
        int Ttl { get; }

        Task WriteAsync(object message);

        void Write(object message);

        Task<object> Request(object requestMessage);

        event EventHandler ConnectionStatusChanged;
        
        event EventHandler<ObjectReceivedEventArgs> ObjectReceived;
    }
}
