using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.ProtoExtensions
{
    public class ConnectionDispose:IProtocolMessage
    {
        public bool ConnectionClosing { get; set; } = true;
        
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }
}
