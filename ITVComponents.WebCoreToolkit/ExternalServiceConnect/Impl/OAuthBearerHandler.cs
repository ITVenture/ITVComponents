using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl
{
    public sealed class OAuthBearerHandler : DelegatingHandler
    {
        private IOAuthTokenService tokenService;
        private readonly string connectionName;

        public OAuthBearerHandler(
            IOAuthTokenService tokenService,
            string connectionName)
        {
            this.tokenService = tokenService;
            this.connectionName = connectionName;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            var token = await tokenService.GetValidAccessTokenAsync(connectionName);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            return await base.SendAsync(request, ct);
        }
    }
}
