using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Shared.Helpers;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class ErrorResponse:IServerResponse
    {
        public SerializedException SerializedException { get; set; }
    }
}
