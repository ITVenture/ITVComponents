using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    public class EventNotificationMessage
    {
        public string EventName { get; set; }
        public object[] Arguments { get; set; }
    }
}
