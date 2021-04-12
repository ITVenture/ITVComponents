using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    public class SetPropertyRequestMessage:InvokeMethodRequestMessage
    {
        public object Value { get;set; }
    }
}
