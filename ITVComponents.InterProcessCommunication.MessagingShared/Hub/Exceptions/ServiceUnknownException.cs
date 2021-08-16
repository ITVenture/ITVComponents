using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions
{
    public class ServiceUnknownException:CommunicationException
    {
        public ServiceUnknownException(string message) : base(message)
        {
        }

        public ServiceUnknownException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
