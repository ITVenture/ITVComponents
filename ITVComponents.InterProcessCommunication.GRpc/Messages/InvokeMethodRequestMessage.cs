using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    public class InvokeMethodRequestMessage:AuthenticatedRequestMessage
    {
        public string TargetObject { get; set; }
        public string TargetMethod { get; set; }
        public object[] MethodArguments { get; set; }
    }
}
