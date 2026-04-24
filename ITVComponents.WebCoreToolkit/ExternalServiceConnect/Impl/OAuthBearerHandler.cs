using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.VisualBasic;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl
{
    public sealed class OAuthBearerHandler : DelegatingHandler
    {
        private IOAuthTokenService tokenService;
        private readonly string connectionName;
        private string bufferedToken;

        public OAuthBearerHandler(
            IOAuthTokenService tokenService,
            string connectionName)
        {
            this.tokenService = tokenService;
            this.connectionName = connectionName;
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string token = null;
            bool allowRetry = false;
            bool retry = true;
            HttpResponseMessage msg  =null;
            while (retry)
            {
                retry = false;
                if (!string.IsNullOrEmpty(bufferedToken))
                {
                    token = bufferedToken;
                    allowRetry = true;
                }
                else
                {
                    bufferedToken = token = tokenService.GetValidAccessToken(connectionName);
                }

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                msg = base.Send(request, cancellationToken);
                if (msg != null && msg.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Token ungültig, nächsten Aufruf mit neuem Token versuchen
                    bufferedToken = null;
                    retry = allowRetry;
                }
            }

            return msg;

        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            string token = null;
            bool allowRetry = false;
            bool retry = true;
            HttpResponseMessage msg = null;
            while (retry)
            {
                retry = false;
                if (!string.IsNullOrEmpty(bufferedToken))
                {
                    token = bufferedToken;
                    allowRetry = true;
                }
                else
                {
                    bufferedToken = token = await tokenService.GetValidAccessTokenAsync(connectionName);
                }

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                msg = await base.SendAsync(request, ct);
                if (msg != null && msg.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Token ungültig, nächsten Aufruf mit neuem Token versuchen
                    bufferedToken = null;
                    retry = allowRetry;
                }
            }

            return msg;
        }
    }
}
