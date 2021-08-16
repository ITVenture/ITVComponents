using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc
{
    public class GrpcHubSettings
    {
        public bool TrustAllCertificates { get; set; }

        public List<string> TrustedCertificates { get; set; } = new List<string>();


    }
}