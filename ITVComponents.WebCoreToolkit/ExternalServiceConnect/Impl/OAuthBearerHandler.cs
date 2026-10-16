using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl
{
    /// <summary>
    /// Puts the access token of an external service on every request and retries ONCE with a fresh token when the
    /// provider answers 401 to a token this handler had kept from an earlier call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The retry sends a COPY of the request. An <see cref="HttpRequestMessage"/> can be sent only once; resending the
    /// original threw "The request message was already sent" - exactly on the path that was meant to recover from an
    /// expired token, i.e. on every client that lived longer than one token lifetime.
    /// </para>
    /// <para>
    /// The fresh token comes from <see cref="IOAuthTokenService"/>, which refreshes when the stored token is (about to
    /// be) expired. If it hands back the same token that was just rejected, there is nothing to gain from a retry and
    /// the 401 goes to the caller.
    /// </para>
    /// </remarks>
    public sealed class OAuthBearerHandler : DelegatingHandler
    {
        private readonly IOAuthTokenService tokenService;
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
            var token = bufferedToken;
            var fromBuffer = !string.IsNullOrEmpty(token);
            if (!fromBuffer)
            {
                bufferedToken = token = tokenService.GetValidAccessToken(connectionName);
            }
            else
            {
                // A retry needs the body a second time; a streamed body would be consumed by the first attempt.
                request.Content?.LoadIntoBufferAsync(cancellationToken).GetAwaiter().GetResult();
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var msg = base.Send(request, cancellationToken);
            if (!fromBuffer || msg.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                return msg;
            }

            bufferedToken = null;
            var freshToken = tokenService.GetValidAccessToken(connectionName);
            if (freshToken == token)
            {
                LogEnvironment.LogEvent($"External service '{connectionName}' rejected the current access token with 401 and no newer token is available.", LogSeverity.Warning);
                return msg;
            }

            bufferedToken = freshToken;
            msg.Dispose();
            var retry = CloneRequest(request, request.Content?.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult());
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", freshToken);
            return base.Send(retry, cancellationToken);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            var token = bufferedToken;
            var fromBuffer = !string.IsNullOrEmpty(token);
            if (!fromBuffer)
            {
                bufferedToken = token = await tokenService.GetValidAccessTokenAsync(connectionName);
            }
            else if (request.Content != null)
            {
                // A retry needs the body a second time; a streamed body would be consumed by the first attempt.
                await request.Content.LoadIntoBufferAsync(ct);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var msg = await base.SendAsync(request, ct);
            if (!fromBuffer || msg.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                return msg;
            }

            bufferedToken = null;
            var freshToken = await tokenService.GetValidAccessTokenAsync(connectionName);
            if (freshToken == token)
            {
                LogEnvironment.LogEvent($"External service '{connectionName}' rejected the current access token with 401 and no newer token is available.", LogSeverity.Warning);
                return msg;
            }

            bufferedToken = freshToken;
            msg.Dispose();
            var body = request.Content != null ? await request.Content.ReadAsByteArrayAsync(ct) : null;
            var retry = CloneRequest(request, body);
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", freshToken);
            return await base.SendAsync(retry, ct);
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[] body)
        {
            var retVal = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };

            foreach (var header in request.Headers)
            {
                retVal.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var option in request.Options)
            {
                ((IDictionary<string, object>)retVal.Options)[option.Key] = option.Value;
            }

            if (body != null)
            {
                retVal.Content = new ByteArrayContent(body);
                foreach (var header in request.Content.Headers)
                {
                    retVal.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return retVal;
        }
    }
}
