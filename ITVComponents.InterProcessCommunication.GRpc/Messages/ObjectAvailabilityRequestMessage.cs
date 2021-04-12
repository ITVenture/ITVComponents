using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    [Serializable]
    public class ObjectAvailabilityRequestMessage:AuthenticatedRequestMessage
    {
        public string UniqueName { get; set; }
    }
}
