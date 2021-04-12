using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Security
{
    public interface IIdentityProvider
    {
        TransferIdentity CurrentIdentity{ get; }
    }
}
