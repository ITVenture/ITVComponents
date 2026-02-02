using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl
{
    public sealed class OAuthTokenService : IOAuthTokenService
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private readonly ISecurityRepository securityRepo;
        private readonly IHttpClientFactory httpClientFactory;

        public OAuthTokenService(
            ISecurityRepository securityRepo,
            IHttpClientFactory httpClientFactory)
        {
            this.securityRepo = securityRepo;
            this.httpClientFactory = httpClientFactory;
        }

        public async Task<string> GetValidAccessTokenAsync(string connectionName)
        {
            var semaphore = _locks.GetOrAdd(connectionName, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                var token = securityRepo.GetBufferedToken(connectionName, false, out var svc, out var update);
                if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(-1))
                    return token.AccessToken;

                await RefreshAsync(token, svc, update);

                return (securityRepo.GetBufferedToken(connectionName, false, out _, out _)).AccessToken;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task RevokeAsync(string connectionName)
        {

            TranslatedTokenResponse token = null;
            ExternalOAuthConnection svc = null; 
            try
            {
                token = securityRepo.GetBufferedToken(connectionName, true, out svc, out var update);
            }
            catch
            {
            }

            if (token == null || token.RefreshToken == null)
                return;

            var client = httpClientFactory.CreateClient();
            var formRaw = new Dictionary<string, string>
            {
                ["token"] = token.RefreshToken,
                ["token_type_hint"] = "refresh_token",
                ["client_id"] = svc.ClientId
                // client_secret nur wenn erlaubt
            };

            if (!string.IsNullOrEmpty(svc.ClientSecret))
            {
                formRaw.Add("client_secret", svc.ClientSecret);
            }

            var response = await client.PostAsync(
                svc.RevocationEndpoint,
                new FormUrlEncodedContent(formRaw));

            // RFC: 200 OK auch bei bereits ungültigem Token
            // => keine harte Fehlerbehandlung nötig
        }

        private async Task RefreshAsync(TranslatedTokenResponse token, ExternalOAuthConnection svc, Action<TranslatedTokenResponse> update)
        {
            var c = svc;
            if (token.RefreshToken == null)
                throw new InvalidOperationException("No refresh token");

            //var refreshToken = _protector.Unprotect(c.EncryptedRefreshToken);

            var client = httpClientFactory.CreateClient();

            var response = await client.PostAsync(
                c.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = token.RefreshToken,
                    ["client_id"] = c.ClientId,
                    ["client_secret"] = c.ClientSecret
                }));

            response.EnsureSuccessStatusCode();

            var newToken = await response.Content
                .ReadFromJsonAsync<TokenResponse>();
            var dbToken = new TranslatedTokenResponse
            {
                ExpiresAt =
                    DateTimeOffset.UtcNow.AddSeconds(newToken.ExpiresIn),
                Scope = token.Scope,
                RefreshToken = token.RefreshToken,
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };

            update(dbToken);
        }
    }
}
