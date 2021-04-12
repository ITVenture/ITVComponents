using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    public class RegisterEventRequestMessage:AuthenticatedRequestMessage
    {
        public string TargetObject { get; set; }
        public string EventName { get; set; }
        public string RespondChannel { get; set; }
    }
}
