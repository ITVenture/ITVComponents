using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Options;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Der schmale Zugang zur Payrexx-REST-API: Adresse zusammensetzen, Schluessel anhaengen, Antwort
    /// auspacken. Alles Fachliche liegt eine Ebene hoeher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Die Antwortform ist die Falle.</b> Payrexx antwortet IMMER mit
    /// <c>{ "status": "success|error", "data": [ … ] }</c> - auch dort, wo genau ein Objekt gemeint ist:
    /// eine einzelne Zahlungsseite kommt als Liste mit einem Element. Wer direkt auf das Objekt
    /// deserialisiert, bekommt null und sucht den Fehler beim Aufruf statt beim Auspacken.
    /// </para>
    /// <para>
    /// <b>Und der Statuscode luegt.</b> Ein fachlicher Fehler kommt als HTTP 200 mit
    /// <c>status: "error"</c>. <see cref="EnsureSuccess"/> prueft darum den Rumpf, nicht den Code.
    /// </para>
    /// </remarks>
    public sealed class PayrexxApiClient
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient http;
        private readonly IGlobalSettings<PayrexxOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="PayrexxApiClient"/> class.</summary>
        public PayrexxApiClient(HttpClient http, IGlobalSettings<PayrexxOptions> settings)
        {
            this.http = http;
            this.settings = settings;
        }

        /// <summary>Die aktuelle Konfiguration - bei jedem Zugriff frisch aus dem GlobalSetting.</summary>
        public PayrexxOptions Options => settings.Value;

        /// <summary>Legt etwas an (<c>POST</c>) und liefert das ERSTE Element der Antwort.</summary>
        public Task<T?> PostAsync<T>(string resource, object payload, CancellationToken cancellationToken)
            => SendAsync<T>(HttpMethod.Post, resource, payload, cancellationToken);

        /// <summary>Liest etwas (<c>GET</c>) und liefert das ERSTE Element der Antwort, oder null.</summary>
        public Task<T?> GetAsync<T>(string resource, CancellationToken cancellationToken)
            => SendAsync<T>(HttpMethod.Get, resource, null, cancellationToken);

        /// <summary>Loescht etwas (<c>DELETE</c>) - etwa eine Zahlungsseite, die niemand mehr bezahlen soll.</summary>
        public Task<T?> DeleteAsync<T>(string resource, CancellationToken cancellationToken)
            => SendAsync<T>(HttpMethod.Delete, resource, null, cancellationToken);

        private async Task<T?> SendAsync<T>(HttpMethod method, string resource, object? payload,
            CancellationToken cancellationToken)
        {
            var options = Options;
            if (string.IsNullOrWhiteSpace(options.Instance) || string.IsNullOrWhiteSpace(options.ApiSecret))
            {
                // Fail-fast mit Ansage: ohne diese beiden antwortet Payrexx mit einem nichtssagenden
                // Fehler, und die Suche beginnt bei der Anfrage statt bei der Konfiguration.
                throw new InvalidOperationException(
                    "Payrexx is not configured: the 'PayrexxPayments' setting needs both Instance and ApiSecret.");
            }

            // Die Instanz gehoert IMMER in die Query - auch bei POST, wo alles andere in den Rumpf geht.
            var uri = $"{options.ApiBaseUrl.TrimEnd('/')}/{resource.TrimStart('/')}" +
                      (resource.Contains('?') ? "&" : "?") + $"instance={Uri.EscapeDataString(options.Instance)}";

            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Add("X-API-KEY", options.ApiSecret);
            if (payload != null)
            {
                request.Content = JsonContent.Create(payload, options: Json);
            }

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            PayrexxEnvelope<T>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<PayrexxEnvelope<T>>(body, Json);
            }
            catch (JsonException ex)
            {
                // Kein Payrexx-Rumpf: fast immer eine Fehlerseite davor (Proxy, falsche Adresse, WAF-Sperre
                // nach 600 Anfragen je 5 Minuten). Der Anfang des Rumpfes sagt mehr als die Ausnahme.
                LogEnvironment.LogEvent(
                    $"Payrexx answered {(int)response.StatusCode} on {method} {resource} with something that is not its usual envelope: {Trim(body)}",
                    LogSeverity.Error, LogContext);
                throw new PayrexxApiException($"Payrexx returned an unreadable response ({(int)response.StatusCode}).", ex);
            }

            EnsureSuccess(envelope, method, resource, response.StatusCode, body);
            return envelope!.Data is { Count: > 0 } ? envelope.Data[0] : default;
        }

        private static void EnsureSuccess<T>(PayrexxEnvelope<T>? envelope, HttpMethod method, string resource,
            System.Net.HttpStatusCode statusCode, string body)
        {
            if (envelope != null && string.Equals(envelope.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var message = envelope?.Message ?? Trim(body);
            LogEnvironment.LogEvent(
                $"Payrexx refused {method} {resource} (HTTP {(int)statusCode}): {message}",
                LogSeverity.Error, LogContext);
            throw new PayrexxApiException($"Payrexx refused the request: {message}");
        }

        private static string Trim(string value)
            => string.IsNullOrEmpty(value) ? "<empty>" : value.Length <= 400 ? value : value[..400] + "…";

        internal const string LogContext = "TenantPayments";

        /// <summary>Die Huelle, in die Payrexx jede Antwort packt.</summary>
        private sealed class PayrexxEnvelope<T>
        {
            public string? Status { get; set; }

            public List<T>? Data { get; set; }

            /// <summary>Der Klartext im Fehlerfall.</summary>
            public string? Message { get; set; }
        }
    }

    /// <summary>Payrexx hat die Anfrage abgelehnt oder unverstaendlich geantwortet.</summary>
    public class PayrexxApiException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="PayrexxApiException"/> class.</summary>
        public PayrexxApiException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="PayrexxApiException"/> class.</summary>
        public PayrexxApiException(string message, Exception innerException) : base(message, innerException) { }
    }
}
