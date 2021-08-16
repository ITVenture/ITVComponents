using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.ProtoExtensions
{
    [Serializable]
    public class ConnectionRequest
    {
        public string User { get; set; }
        public string ProposedGuid { get; set; }

        public int Ttl { get; set; } = 15;
    }
}
