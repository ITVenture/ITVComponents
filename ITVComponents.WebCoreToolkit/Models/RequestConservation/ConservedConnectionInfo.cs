using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal class ConservedConnectionInfo: ConnectionInfo
    {
        public override async Task<X509Certificate2> GetClientCertificateAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return ClientCertificate;
        }

        public override string Id { get; set; }
        public override IPAddress RemoteIpAddress { get; set; }
        public override int RemotePort { get; set; }
        public override IPAddress LocalIpAddress { get; set; }
        public override int LocalPort { get; set; }
        public override X509Certificate2 ClientCertificate { get; set; }
    }
}
