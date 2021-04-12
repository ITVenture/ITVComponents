using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Security;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    public abstract class AuthenticatedRequestMessage
    {
        public TransferIdentity AuthenticatedUser { get; set; }
    }
}
