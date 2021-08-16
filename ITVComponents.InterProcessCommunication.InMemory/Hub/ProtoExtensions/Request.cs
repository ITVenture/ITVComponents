using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.ProtoExtensions
{
    [Serializable]
    public class Request
    {
        public string RequestId { get; set; }

        public object Payload { get; set; }

        public string Identity { get; set; }
    }
}
