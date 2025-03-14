using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Channels
{
    public class ObjectReceivedEventArgs
    {
        public DataTransferContext Context { get; set; }

        public IProtocolMessage Value { get; set; }

        public IProtocolMessage Result { get; set; }
    }
}
