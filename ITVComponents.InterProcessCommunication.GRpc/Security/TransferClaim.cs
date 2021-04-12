using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Security
{
    public class TransferClaim
    {
        public string Value { get; set; }
        public string Issuer { get; set; }
        public Dictionary<string, string> Properties { get; set; }
        public string OriginalIssuer { get; set; }
        public string Type { get; set; }
        public string ValueType { get; set; }
    }
}
