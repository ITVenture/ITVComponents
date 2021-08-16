using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions
{
    public class ServiceTimeoutException:CommunicationException
    {
        public ServiceTimeoutException(string message) : base(message)
        {
        }

        public ServiceTimeoutException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
