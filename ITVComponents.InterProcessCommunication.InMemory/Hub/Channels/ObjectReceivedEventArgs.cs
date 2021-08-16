using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Channels
{
    public class ObjectReceivedEventArgs
    {
        public DataTransferContext Context { get; set; }

        public object Value { get; set; }

        public object Result { get; set; }
    }
}
