using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Security
{
    public class TransferIdentity
    {
        public string NameType { get; set; }
        public string RoleType { get;set; }
        public string AuthenticationType { get; set; }
        public bool IsAuthenticated { get; set; }
        public string Label { get; set; }
        public List<TransferClaim> Claims { get; set; } = new List<TransferClaim>();
    }
}
