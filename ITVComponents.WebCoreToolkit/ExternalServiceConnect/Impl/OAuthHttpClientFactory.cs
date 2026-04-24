using ITVComponents.WebCoreToolkit.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl
{
    public sealed class OAuthHttpClientFactory : IOAuthHttpClientFactory
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IOAuthTokenService tokenService;
        private readonly ISecurityRepository securityRepo;

        public OAuthHttpClientFactory(
            IHttpClientFactory httpClientFactory,
            IOAuthTokenService tokenService,
            ISecurityRepository securityRepo)
        {
            this.httpClientFactory = httpClientFactory;
            this.tokenService = tokenService;
            this.securityRepo = securityRepo;
        }

        public HttpClient Create(string connectionName)
        {
            var client = httpClientFactory.CreateClient();
            var connection = securityRepo.GetExternalService(connectionName, false);
            if (connection.AuthenticationType is ExternalServiceAuthenticationType.OAuthAuthorizationFlow
                or ExternalServiceAuthenticationType.OAuthClientCredentialsFlow
                or ExternalServiceAuthenticationType.OAuthBasicClientCredentialsFlow)
            {
                var handler = new OAuthBearerHandler(tokenService, connectionName)
                {
                    InnerHandler = new HttpClientHandler()
                };

                // Named Client für Timeouts, BaseAddress etc.

                // Handler-Kette ersetzen
                client = new HttpClient(handler)
                {
                    BaseAddress = client.BaseAddress,
                    Timeout = client.Timeout
                };
            }
            else if (connection.AuthenticationType == ExternalServiceAuthenticationType.Basic)
            {
                var svc = securityRepo.GetExternalService(connectionName, true);
                var basicHeaderRaw = $"{svc.ClientId}:{svc.ClientSecret}";
                var basicHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(basicHeaderRaw));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicHeaderValue);
            }
            else if (connection.AuthenticationType == ExternalServiceAuthenticationType.ApiKey)
            {
                var svc = securityRepo.GetExternalService(connectionName, true);
                client.DefaultRequestHeaders.Add(svc.ClientId, svc.ClientSecret);
            }
            else if (connection.AuthenticationType == ExternalServiceAuthenticationType.BearerToken)
            {
                var handler = new PlainBearerHandler(securityRepo, connectionName)
                {
                    InnerHandler = new HttpClientHandler()
                };

                client = new HttpClient(handler)
                {
                    BaseAddress = client.BaseAddress,
                    Timeout = client.Timeout
                };
            }

            return client;
        }
    }
}
