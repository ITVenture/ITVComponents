using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth.Config;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth
{
    /// <summary>
    /// Der mitgelieferte Weg: ein API-Schluessel wird gegen ein Token getauscht.
    /// </summary>
    /// <remarks>
    /// Passt zum Endpunkt <c>ClientAppToken</c> des Toolkits, der einen Geraete-Schluessel
    /// (<c>&lt;ClientKey&gt;.&lt;Label&gt;.&lt;Geheimnis&gt;</c>) entgegennimmt. Wer sein Token anders
    /// bekommt, setzt eine eigene <see cref="ITokenSource"/> ein, statt diese Klasse zu kopieren.
    /// </remarks>
    public class ApiKeyTokenSource : ITokenSource, IDisposable
    {
        private readonly JwtAuthConfig configuration;
        private readonly HttpClient client;

        public ApiKeyTokenSource(JwtAuthConfig configuration)
        {
            this.configuration = configuration;
            client = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, configuration.TimeoutSeconds)) };
        }

        /// <inheritdoc/>
        public async Task<BearerToken> AcquireAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(configuration.TokenEndpoint)
                || string.IsNullOrWhiteSpace(configuration.ApiKey))
            {
                // Ohne diese beiden kann dieser Weg nichts tun. Eine Meldung statt eines leeren Tokens:
                // ein leeres Token faellt erst am Hub auf, und dort als RECHTE-Problem.
                LogEnvironment.LogEvent(
                    $"The jwt-configuration '{configuration.Name}' has no token-endpoint or no api-key. " +
                    "Either configure both, or give the JwtAuthInit an own ITokenSource.",
                    LogSeverity.Error);
                return null;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint);
                request.Headers.Add("X-Api-Key", configuration.ApiKey);
                request.Content = new StringContent("{}", System.Text.Encoding.UTF8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    LogEnvironment.LogEvent(
                        $"The token-endpoint {configuration.TokenEndpoint} answered {(int)response.StatusCode} " +
                        $"({response.ReasonPhrase}) for configuration '{configuration.Name}'.",
                        LogSeverity.Error);
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
                var expires = doc.RootElement.TryGetProperty("expiresUtc", out var e)
                    ? e.GetDateTime()
                    : DateTime.UtcNow;

                if (string.IsNullOrEmpty(token))
                {
                    LogEnvironment.LogEvent(
                        $"The token-endpoint {configuration.TokenEndpoint} answered without a token for configuration '{configuration.Name}'.",
                        LogSeverity.Error);
                    return null;
                }

                return new BearerToken(token, expires);
            }
            catch (Exception ex)
            {
                // Der Aufrufer bekommt null und meldet das seinerseits - hier steht das WARUM, das ihm
                // sonst fehlt.
                LogEnvironment.LogEvent(
                    $"Could not obtain a token from {configuration.TokenEndpoint} for configuration '{configuration.Name}': {ex.OutlineException()}",
                    LogSeverity.Error);
                return null;
            }
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
