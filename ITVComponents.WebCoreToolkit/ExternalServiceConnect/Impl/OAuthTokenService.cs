using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.HttpSys;

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

        public string GetValidAccessToken(string connectionName)
        {
            return AsyncHelpers.RunSync(async () => await GetValidAccessTokenAsync(connectionName));
        }

        public async Task<string> GetValidAccessTokenAsync(string connectionName)
        {
            var semaphore = _locks.GetOrAdd(connectionName, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                var token = securityRepo.GetBufferedToken(connectionName, false, false, out var svc, out var update);
                if (token != null)
                {
                    // One minute of headroom BEFORE expiry. With AddMinutes(-1) a token was handed out for up to a
                    // minute after it had expired, and the call it was meant for failed with 401.
                    if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                        return token.AccessToken;
                }
                else if (svc.AuthenticationType == ExternalServiceAuthenticationType.OAuthAuthorizationFlow)
                {
                    throw new InvalidOperationException($"No appropriate Token found for {connectionName}");
                }
                    
                return (await RefreshAsync(token, svc, update)).AccessToken;

                //return (securityRepo.GetBufferedToken(connectionName, false, true, out _, out _)).AccessToken;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task RevokeAsync(string connectionName)
        {

            TranslatedTokenResponse token = null;
            ExternalServiceConnection svc = null;
            try
            {
                token = securityRepo.GetBufferedToken(connectionName, true, false, out svc, out var update);
            }
            catch (Exception ex)
            {
                // Revoking must not fail the caller: the local login is gone either way. But a lookup failure is
                // not the same as "nothing to revoke", so it has to be visible.
                LogEnvironment.LogEvent($"Could not read the stored token of external service '{connectionName}' for revocation: {ex.OutlineException()}", LogSeverity.Error);
                return;
            }

            if (token == null || token.RefreshToken == null)
            {
                LogEnvironment.LogDebugEvent($"No refresh token stored for external service '{connectionName}'; nothing to revoke at the provider.", LogSeverity.Report);
                return;
            }

            if (string.IsNullOrEmpty(svc?.RevocationEndpoint))
            {
                LogEnvironment.LogEvent($"External service '{connectionName}' has no revocation endpoint. The login was removed locally, the refresh token stays valid at the provider until it expires.", LogSeverity.Warning);
                return;
            }

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

            try
            {
                using var response = await client.PostAsync(
                    svc.RevocationEndpoint,
                    new FormUrlEncodedContent(formRaw));

                // RFC 7009: 200 OK even for an already invalid token - anything else means the provider did NOT
                // revoke, and the refresh token stays usable there.
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    LogEnvironment.LogEvent($"Revocation of the refresh token for external service '{connectionName}' was rejected with {(int)response.StatusCode}: {body}", LogSeverity.Error);
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Revocation of the refresh token for external service '{connectionName}' failed: {ex.OutlineException()}", LogSeverity.Error);
            }
        }

        private async Task<TranslatedTokenResponse> RefreshAsync(TranslatedTokenResponse token, ExternalServiceConnection svc, Action<TranslatedTokenResponse> update)
        {
            var c = svc;
            // token is null on the first client-credentials call - there is nothing buffered yet.
            if (token?.RefreshToken == null && c.AuthenticationType == ExternalServiceAuthenticationType.OAuthAuthorizationFlow)
                throw new InvalidOperationException("No refresh token");

            //var refreshToken = _protector.Unprotect(c.EncryptedRefreshToken);

            var client = httpClientFactory.CreateClient();

            HttpResponseMessage response = null;
            if (c.AuthenticationType == ExternalServiceAuthenticationType.OAuthAuthorizationFlow)
            {
                response = await client.PostAsync(
                    c.TokenEndpoint,
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["refresh_token"] = token.RefreshToken,
                        ["client_id"] = c.ClientId,
                        ["client_secret"] = c.ClientSecret,
                        ["scope"] = token.Scope
                    }));
            }
            else if (c.AuthenticationType == ExternalServiceAuthenticationType.OAuthClientCredentialsFlow)
            {
                response = await client.PostAsync(c.AuthorizationEndpoint, new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = "client_credentials",
                        ["client_id"] = c.ClientId,
                        ["client_secret"] = c.ClientSecret,
                    }));
            }
            else if (c.AuthenticationType == ExternalServiceAuthenticationType.OAuthBasicClientCredentialsFlow)
            {
                var content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = "client_credentials",
                    });

                var authHeaderRaw = $"{c.ClientId}:{c.ClientSecret}";
                var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(authHeaderRaw));
                //content.Headers.Add(new AuthenticationHeaderValue("Basic", authHeaderValue));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);
                response = await client.PostAsync(c.AuthorizationEndpoint, content);
                client.DefaultRequestHeaders.Authorization = null;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported OAuth-Flow: {c.AuthenticationType}");
            }

            if (!response.IsSuccessStatusCode)
            {
                // The body carries the reason (e.g. invalid_grant = the login has to be renewed by the user);
                // EnsureSuccessStatusCode alone would only leave the status code behind.
                var body = await response.Content.ReadAsStringAsync();
                LogEnvironment.LogEvent($"Token request for external service '{c.UniqueConnectionName}' ({c.AuthenticationType}) was rejected with {(int)response.StatusCode}: {body}", LogSeverity.Error);
                response.EnsureSuccessStatusCode();
            }

            var newToken = await response.Content
                .ReadFromJsonAsync<TokenResponse>();
            var dbToken = new TranslatedTokenResponse
            {
                ExpiresAt =
                    DateTimeOffset.UtcNow.AddSeconds(newToken.ExpiresIn),
                Scope = newToken.Scope,
                RefreshToken = newToken.RefreshToken,
                AccessToken = newToken.AccessToken,
                TokenType = newToken.TokenType
            };

            if (!string.IsNullOrEmpty(dbToken.RefreshToken))
            {
                update(dbToken);
            }

            return dbToken;
        }
    }
}
