using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Impl
{
    /// <summary>
    /// Der Zugang zur SumUp-API - mit dem Zugang DES MANDANTEN, je Aufruf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kein fester Schluessel am Client: bei SumUp bringt jeder Mandant sein eigenes Konto mit, und welches gilt,
    /// entscheidet der Vorgang (<c>ProviderAccessSelection</c>). Der Schluessel reist darum als Argument mit und
    /// steht nie in einem Feld, das zwei Aufrufe teilen.
    /// </para>
    /// <para>
    /// Ein API-Key von SumUp (<c>sup_sk_…</c>) und ein OAuth-Token werden gleich verwendet: als Bearer-Token.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen die SumUp-Sandbox gelaufen.</b>
    /// </para>
    /// </remarks>
    public sealed class SumUpApiClient
    {
        /// <summary>Protokoll-Kategorie.</summary>
        internal const string LogContext = "TenantPayments";

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient http;
        private readonly IGlobalSettings<SumUpOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="SumUpApiClient"/> class.</summary>
        public SumUpApiClient(HttpClient http, IGlobalSettings<SumUpOptions> settings)
        {
            this.http = http;
            this.settings = settings;
        }

        /// <summary>Die Einstellungen der Plattform fuer SumUp.</summary>
        public SumUpOptions Options => settings.Value;

        /// <summary>Ruft die API und liest die Antwort; ein leerer Rumpf ergibt <c>default</c>.</summary>
        /// <param name="method">die Methode</param>
        /// <param name="path">der Pfad samt Version, ohne Basis - etwa <c>v0.1/merchants/…</c></param>
        /// <param name="payload">der Rumpf, oder null</param>
        /// <param name="credential">der Zugang des Mandanten</param>
        /// <param name="cancellationToken">bricht den Aufruf ab</param>
        /// <exception cref="SumUpApiException">
        /// bei jeder Absage und wenn keine Antwort kam - dann ohne Statuscode. Der Unterschied zaehlt: ohne
        /// Antwort ist NICHT bekannt, ob der Auftrag angekommen ist.
        /// </exception>
        public async Task<T?> SendAsync<T>(HttpMethod method, string path, object? payload,
            ProviderCredential credential, CancellationToken cancellationToken)
        {
            var options = Options;
            var url = $"{options.ApiBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerOf(credential));
            if (payload != null)
            {
                request.Content = JsonContent.Create(payload, payload.GetType(), options: Json);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 5, 120)));

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Keine Uebersetzung in "fehlgeschlagen": dass die Leitung abbrach, sagt nichts darueber, ob
                // SumUp den Auftrag bekommen hat.
                throw new SumUpApiException(null, null, $"SumUp did not answer '{method} {path}': {ex.Message}", ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new SumUpApiException((int)response.StatusCode, ErrorTypeOf(body),
                        $"SumUp answered {(int)response.StatusCode} to '{method} {path}': {Shorten(body)}");
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    return default;
                }

                try
                {
                    return JsonSerializer.Deserialize<T>(body, Json);
                }
                catch (JsonException ex)
                {
                    throw new SumUpApiException((int)response.StatusCode, null,
                        $"The answer of SumUp to '{method} {path}' could not be read: {ex.Message}", ex);
                }
            }
        }

        private static string BearerOf(ProviderCredential credential)
            => credential.AuthenticationType switch
            {
                ExternalServiceAuthenticationType.BearerToken or ExternalServiceAuthenticationType.ApiKey
                    or ExternalServiceAuthenticationType.OAuthAuthorizationFlow => credential.Secret,
                _ => throw new InvalidOperationException(
                    $"A SumUp access needs an API key or an OAuth login, not {credential.AuthenticationType}.")
            };

        /// <summary>
        /// Die Fehlerart, soweit die Antwort eine nennt.
        /// </summary>
        /// <remarks>
        /// SumUp antwortet nicht einheitlich: die Geraete-API mit <c>{"errors":{"type":"READER_BUSY"}}</c>, andere
        /// Stellen als Problem-JSON mit <c>type</c> als Adresse (<c>…/problem/reader-offline</c>), aeltere mit
        /// <c>error_code</c>. Gelesen wird alles drei; ein Rumpf, der keines davon ist, ergibt keine Art.
        /// </remarks>
        internal static string? ErrorTypeOf(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object
                                                                  && errors.TryGetProperty("type", out var nested)
                                                                  && nested.ValueKind == JsonValueKind.String)
                {
                    return nested.GetString();
                }

                foreach (var name in new[] { "type", "error_code" })
                {
                    if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        var text = value.GetString();
                        // Problem-JSON: die Art ist das letzte Stueck der Adresse, "reader-offline" -> READER_OFFLINE.
                        return text?.Contains('/') == true
                            ? text[(text.LastIndexOf('/') + 1)..].Replace('-', '_').ToUpperInvariant()
                            : text;
                    }
                }
            }
            catch (JsonException ex)
            {
                // Kein JSON: dann gibt es keine Art zu lesen. Der Rumpf selbst steht in der Meldung der Ausnahme,
                // die der Aufrufer wirft.
                ITVComponents.Logging.LogEnvironment.LogDebugEvent(
                    $"The SumUp error body is not JSON, so it names no error type: {ex.Message}", ITVComponents.Logging.LogSeverity.Report);
                return null;
            }

            return null;
        }

        private static string Shorten(string body)
            => string.IsNullOrWhiteSpace(body) ? "(empty)" : body.Length <= 512 ? body : body[..512] + "…";
    }

    /// <summary>Eine Absage von SumUp - oder keine Antwort.</summary>
    public sealed class SumUpApiException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="SumUpApiException"/> class.</summary>
        public SumUpApiException(int? statusCode, string? errorType, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ErrorType = errorType;
        }

        /// <summary>Der HTTP-Status; leer, wenn keine Antwort kam.</summary>
        public int? StatusCode { get; }

        /// <summary>Die Fehlerart, etwa <c>READER_BUSY</c> oder <c>READER_OFFLINE</c>.</summary>
        public string? ErrorType { get; }

        /// <summary>Ob eine Antwort kam. Ohne Antwort ist nicht bekannt, ob der Auftrag angekommen ist.</summary>
        public bool Answered => StatusCode != null;
    }
}
