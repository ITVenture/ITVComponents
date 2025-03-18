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

        Task WriteAsync(IProtocolMessage message);

        void Write(IProtocolMessage message);

        Task<IProtocolMessage> Request(IProtocolMessage requestMessage);

        event EventHandler ConnectionStatusChanged;
        
        event EventHandler<ObjectReceivedEventArgs> ObjectReceived;
    }
}
