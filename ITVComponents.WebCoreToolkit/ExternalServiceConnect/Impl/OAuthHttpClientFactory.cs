using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl
{
    public sealed class OAuthHttpClientFactory : IOAuthHttpClientFactory
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IOAuthTokenService tokenService;

        public OAuthHttpClientFactory(
            IHttpClientFactory httpClientFactory,
            IOAuthTokenService tokenService)
        {
            this.httpClientFactory = httpClientFactory;
            this.tokenService = tokenService;
        }

        public HttpClient Create(string connectionName)
        {
            var handler = new OAuthBearerHandler(tokenService, connectionName)
            {
                InnerHandler = new HttpClientHandler()
            };

            // Named Client für Timeouts, BaseAddress etc.
            var client = httpClientFactory.CreateClient();
            // Handler-Kette ersetzen
            client = new HttpClient(handler)
            {
                BaseAddress = client.BaseAddress,
                Timeout = client.Timeout
            };

            return client;
        }
    }
}
