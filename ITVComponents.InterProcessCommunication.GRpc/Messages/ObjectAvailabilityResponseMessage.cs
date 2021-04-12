using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    [Serializable]
    public class ObjectAvailabilityResponseMessage
    {
        public bool Available { get; set; }
        public string Message { get; set; }
    }
}
