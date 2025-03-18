using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.ProtoExtensions
{
    public class ConnectionRequest:IProtocolMessage
    {
        public string User { get; set; }
        public string ProposedGuid { get; set; }

        public int Ttl { get; set; } = 15;

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public TimeSpan? Timeout { get; set; }
    }
}
