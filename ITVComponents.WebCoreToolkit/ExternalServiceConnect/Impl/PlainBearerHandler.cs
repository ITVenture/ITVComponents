using ITVComponents.WebCoreToolkit.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl
{
    public class PlainBearerHandler: DelegatingHandler
    {
        private readonly ISecurityRepository securityRepo;
        private readonly string connectionName;

        public PlainBearerHandler(ISecurityRepository securityRepo, string connectionName)
        {
            this.securityRepo = securityRepo;
            this.connectionName = connectionName;
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var svc = securityRepo.GetExternalService(connectionName, true);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", svc.ClientSecret);
            return base.Send(request, cancellationToken);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            var svc = securityRepo.GetExternalService(connectionName, true);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", svc.ClientSecret);

            return await base.SendAsync(request, ct);
        }
    }
}
