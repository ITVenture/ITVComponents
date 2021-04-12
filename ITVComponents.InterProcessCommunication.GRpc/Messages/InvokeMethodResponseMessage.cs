using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    public class InvokeMethodResponseMessage
    {
        public object[] Arguments { get; set; }
        public object Result { get; set; }
    }
}
