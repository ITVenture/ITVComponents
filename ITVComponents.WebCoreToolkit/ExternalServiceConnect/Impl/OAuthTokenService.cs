using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using SkiaSharp;
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
using ITVComponents.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl
{
    public sealed class OAuthTokenService : IOAuthTokenService
    {
        private const int MaxLoggedResponseLength = 2000;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private readonly ISecurityRepository securityRepo;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<OAuthTokenService> logger;

        public OAuthTokenService(
            ISecurityRepository securityRepo,
            IHttpClientFactory httpClientFactory,
            ILogger<OAuthTokenService> logger)
        {
            this.securityRepo = securityRepo;
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
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
                    // renew the token one minute ahead of its expiration
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
                // revocation is best-effort: without a buffered token there is nothing to revoke
                logger.LogWarning(ex, "Unable to read the buffered token of {ConnectionName} for revocation.", connectionName);
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

        private async Task<TranslatedTokenResponse> RefreshAsync(TranslatedTokenResponse token, ExternalServiceConnection svc, Action<TranslatedTokenResponse> update)
        {
            var c = svc;
            if (token?.RefreshToken == null && c.AuthenticationType == ExternalServiceAuthenticationType.OAuthAuthorizationFlow)
                throw new InvalidOperationException("No refresh token");

            //var refreshToken = _protector.Unprotect(c.EncryptedRefreshToken);

            var client = httpClientFactory.CreateClient();

            HttpResponseMessage response = null;
            string endpoint;
            IDictionary<string, string> sentForm;
            if (c.AuthenticationType == ExternalServiceAuthenticationType.OAuthAuthorizationFlow)
            {
                endpoint = c.TokenEndpoint;
                sentForm = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = token.RefreshToken,
                    ["client_id"] = c.ClientId,
                    ["client_secret"] = c.ClientSecret,
                    ["scope"] = token.Scope
                };
                response = await client.PostAsync(endpoint, new FormUrlEncodedContent(sentForm));
            }
            else if (c.AuthenticationType == ExternalServiceAuthenticationType.OAuthClientCredentialsFlow)
            {
                var formRaw = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = c.ClientId,
                    ["client_secret"] = c.ClientSecret,
                };

                AppendTokenRequestParameters(formRaw, c);
                endpoint = c.AuthorizationEndpoint;
                sentForm = formRaw;
                response = await client.PostAsync(endpoint, new FormUrlEncodedContent(formRaw));
            }
            else if (c.AuthenticationType == ExternalServiceAuthenticationType.OAuthBasicClientCredentialsFlow)
            {
                var formRaw = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                };

                AppendTokenRequestParameters(formRaw, c);
                var content = new FormUrlEncodedContent(formRaw);

                var authHeaderRaw = $"{c.ClientId}:{c.ClientSecret}";
                var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(authHeaderRaw));
                //content.Headers.Add(new AuthenticationHeaderValue("Basic", authHeaderValue));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);
                endpoint = c.AuthorizationEndpoint;
                sentForm = formRaw;
                response = await client.PostAsync(endpoint, content);
                client.DefaultRequestHeaders.Authorization = null;
            }
            else if (c.AuthenticationType == ExternalServiceAuthenticationType.OAuthPasswordFlow)
            {
                // Resource Owner Password Credentials Grant (RFC 6749, 4.3): ClientId/ClientSecret carry username/password
                var formRaw = new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["username"] = c.ClientId,
                    ["password"] = c.ClientSecret
                };

                if (!string.IsNullOrEmpty(c.Scope))
                {
                    formRaw.Add("scope", c.Scope);
                }

                AppendTokenRequestParameters(formRaw, c);
                endpoint = c.AuthorizationEndpoint;
                sentForm = formRaw;
                response = await client.PostAsync(endpoint, new FormUrlEncodedContent(formRaw));
            }
            else
            {
                throw new InvalidOperationException($"Unsupported OAuth-Flow: {c.AuthenticationType}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await ReadResponseBodyForLogAsync(response, c.UniqueConnectionName);
                var fields = DescribeFormFields(sentForm);
                logger.LogError("Token request of {ConnectionName} ({AuthenticationType}) to {Endpoint} failed with {StatusCode} {ReasonPhrase}. Sent fields: {Fields}. Response: {ResponseBody}",
                    c.UniqueConnectionName, c.AuthenticationType, endpoint, (int)response.StatusCode, response.ReasonPhrase, fields, errorBody);
                throw new HttpRequestException($"Token request of {c.UniqueConnectionName} to {endpoint} failed with {(int)response.StatusCode} ({response.ReasonPhrase}). Sent fields: {fields}. Response: {errorBody}", null, response.StatusCode);
            }

            TokenResponse newToken;
            try
            {
                newToken = await response.Content
                    .ReadFromJsonAsync<TokenResponse>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The token response of {ConnectionName} from {Endpoint} could not be read as token-json.", c.UniqueConnectionName, endpoint);
                throw;
            }

            if (string.IsNullOrEmpty(newToken?.AccessToken))
            {
                logger.LogError("The token response of {ConnectionName} from {Endpoint} contains no access_token.", c.UniqueConnectionName, endpoint);
                throw new InvalidOperationException($"The token response of {c.UniqueConnectionName} from {endpoint} contains no access_token.");
            }

            // tokens without refresh-token (e.g. client-credentials) are buffered too. When a refresh does not
            // return a new refresh-token, the previous one remains valid and is kept.
            var dbToken = new TranslatedTokenResponse
            {
                ExpiresAt =
                    DateTimeOffset.UtcNow.AddSeconds(newToken.ExpiresIn),
                Scope = newToken.Scope,
                RefreshToken = !string.IsNullOrEmpty(newToken.RefreshToken) ? newToken.RefreshToken : token?.RefreshToken,
                AccessToken = newToken.AccessToken,
                TokenType = newToken.TokenType
            };

            update(dbToken);

            return dbToken;
        }

        /// <summary>
        /// Describes the sent form-fields for diagnostics. Only names are listed, values are never exposed because they may
        /// contain credentials. Empty values are marked, which reveals e.g. a password that was not stored
        /// </summary>
        /// <param name="form">the sent form-data</param>
        /// <returns>a comma-separated list of the field-names</returns>
        private static string DescribeFormFields(IDictionary<string, string> form)
        {
            return string.Join(", ", form.Select(n => string.IsNullOrEmpty(n.Value) ? $"{n.Key} (empty)" : n.Key));
        }

        /// <summary>
        /// Reads the body of a failed response for diagnostics and truncates it to a reasonable length
        /// </summary>
        /// <param name="response">the failed response</param>
        /// <param name="connectionName">the connection for which the token was requested</param>
        /// <returns>the (truncated) response-body</returns>
        private async Task<string> ReadResponseBodyForLogAsync(HttpResponseMessage response, string connectionName)
        {
            try
            {
                var body = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(body))
                {
                    return "(empty)";
                }

                return body.Length > MaxLoggedResponseLength ? $"{body.Substring(0, MaxLoggedResponseLength)}..." : body;
            }
            catch (Exception ex)
            {
                // the body is diagnostic only, the failed status is reported anyway
                logger.LogWarning(ex, "Unable to read the error response of the token request of {ConnectionName}.", connectionName);
                return "(unreadable)";
            }
        }

        /// <summary>
        /// Appends the additional token-request parameters of the connection to the form. Parameters that the flow already set
        /// are skipped, so a configured parameter never overrides grant_type, credentials or scope
        /// </summary>
        /// <param name="formRaw">the form-data of the token-request</param>
        /// <param name="connection">the connection that provides the additional parameters</param>
        private void AppendTokenRequestParameters(Dictionary<string, string> formRaw, ExternalServiceConnection connection)
        {
            if (connection.TokenRequestParameters == null)
            {
                return;
            }

            foreach (var parameter in connection.TokenRequestParameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Key))
                {
                    logger.LogWarning("Skipping a token-request parameter without name on {ConnectionName}.", connection.UniqueConnectionName);
                    continue;
                }

                var name = parameter.Key.Trim();
                if (formRaw.Keys.Any(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogWarning("Token-request parameter {ParameterName} on {ConnectionName} is set by the {AuthenticationType} itself and is ignored.", name, connection.UniqueConnectionName, connection.AuthenticationType);
                    continue;
                }

                formRaw.Add(name, parameter.Value ?? string.Empty);
            }
        }
    }
}
