using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Models.RequestConservation
{
    internal static class Extensions
    {
        public static HttpRequest Conserve(this HttpRequest request, ConservedHttpContext parent)
        {
            return new ConservedHttpRequest(request, parent);
        }

        public static HttpResponse Conserve(this HttpResponse response, ConservedHttpContext parent)
        {
            return new ConservedHttpResponse(response, parent);
        }

        public static ConnectionInfo Conserve(this ConnectionInfo info)
        {
            return new ConservedConnectionInfo
            {
                ClientCertificate = info.ClientCertificate,
                Id = info.Id,
                LocalIpAddress = info.LocalIpAddress,
                LocalPort = info.LocalPort,
                RemoteIpAddress = info.RemoteIpAddress,
                RemotePort = info.RemotePort
            };
        }

        public static ISession Conserve(this ISession session)
        {
            return new ConservedSession(session);
        }
    }
}
