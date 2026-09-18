using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Options;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Der Zugang zur <b>Service-API</b> von Payrexx — das ist die Plattform-Seite (White-Label /
    /// Marktplatz): Händler anlegen, ihren Stand lesen, erstatten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Das ist eine ANDERE API als die des Händlers</b>, und zwar in jeder Hinsicht, in der sie sich
    /// unterscheiden kann — deshalb ein eigener Zugang statt eines Schalters im bestehenden:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Andere Adresse:</b> <c>api.payrexx.com/v2.3/service</c> statt <c>api.payrexx.com/v1.x</c>.
    /// Und die Version ist nicht einmal innerhalb der Service-API einheitlich — die Prüfung der Identität
    /// (<i>verification</i>) liegt unter <b>v2.0</b>, während der Händler selbst unter <b>v2.3</b> steht.
    /// Darum ist die Version hier je Aufruf mitzugeben und nicht Teil der Basis-Adresse.</item>
    /// <item><b>Andere Anmeldung:</b> zusätzlich zu <c>X-API-KEY</c> ein <c>X-PLATFORM</c>. Fehlt der
    /// zweite Kopf, antwortet die API nicht mit "Kopfzeile fehlt", sondern wie bei einem falschen
    /// Schlüssel.</item>
    /// <item><b>Andere Antwortform:</b> <c>{ "data": …, "meta": … }</c> statt
    /// <c>{ "status": "success", "data": [ … ] }</c>. Es gibt hier <b>kein</b> <c>status</c>-Feld —
    /// wer den Prüfcode der Händler-API wiederverwendet, hält jede erfolgreiche Antwort für einen Fehler.</item>
    /// <item><b>Und <c>data</c> ist mal ein Objekt, mal eine Liste</b> — je Endpunkt verschieden. Darum
    /// wird hier auf <see cref="JsonElement"/> ausgepackt und die Form geprüft, statt sich auf eine
    /// festzulegen.</item>
    /// </list>
    /// <para>
    /// <b>Im White-Label-Betrieb liegt die HÄNDLER-API zudem auf der eigenen Domain</b>
    /// (<c>api.meine-plattform.ch</c> statt <c>api.payrexx.com</c>) — dafür gibt es
    /// <see cref="PayrexxOptions.ApiBaseUrl"/>. Diese Service-API bleibt davon unberührt.
    /// </para>
    /// </remarks>
    public sealed class PayrexxServiceApiClient
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient http;
        private readonly IGlobalSettings<PayrexxOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="PayrexxServiceApiClient"/> class.</summary>
        public PayrexxServiceApiClient(HttpClient http, IGlobalSettings<PayrexxOptions> settings)
        {
            this.http = http;
            this.settings = settings;
        }

        /// <summary>Die aktuelle Konfiguration - bei jedem Zugriff frisch aus dem GlobalSetting.</summary>
        public PayrexxOptions Options => settings.Value;

        /// <summary>Legt etwas an (<c>POST</c>).</summary>
        /// <param name="version">die API-Version dieses Endpunkts, etwa <c>v2.3</c> - sie ist NICHT einheitlich</param>
        /// <param name="resource">der Pfad hinter <c>/service</c>, etwa <c>merchant</c></param>
        /// <param name="payload">der Rumpf</param>
        /// <param name="cancellationToken">Abbruchmarke</param>
        public Task<T?> PostAsync<T>(string version, string resource, object payload, CancellationToken cancellationToken)
            => SendAsync<T>(HttpMethod.Post, version, resource, payload, cancellationToken);

        /// <summary>Liest etwas (<c>GET</c>).</summary>
        public Task<T?> GetAsync<T>(string version, string resource, CancellationToken cancellationToken)
            => SendAsync<T>(HttpMethod.Get, version, resource, null, cancellationToken);

        private async Task<T?> SendAsync<T>(HttpMethod method, string version, string resource, object? payload,
            CancellationToken cancellationToken)
        {
            var options = Options;
            if (string.IsNullOrWhiteSpace(options.ApiSecret) || string.IsNullOrWhiteSpace(options.PlatformKey))
            {
                // Fail-fast mit Ansage: ohne den Plattform-Kopf antwortet die API wie bei einem falschen
                // Schluessel, und die Suche beginnt beim Geheimnis statt bei der Konfiguration.
                throw new InvalidOperationException(
                    "The Payrexx service API is not configured: the 'PayrexxPayments' setting needs both ApiSecret and PlatformKey.");
            }

            var uri = $"{options.ServiceApiBaseUrl.TrimEnd('/')}/{version.Trim('/')}/service/{resource.TrimStart('/')}";
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Add("X-API-KEY", options.ApiSecret);
            request.Headers.Add("X-PLATFORM", options.PlatformKey);
            if (payload != null)
            {
                request.Content = JsonContent.Create(payload, options: Json);
            }

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Anders als die Haendler-API meldet diese hier Fehler ueber den Statuscode. Der Rumpf
                // traegt trotzdem meist den Klartext - und der ist das, was weiterhilft.
                LogEnvironment.LogEvent(
                    $"The Payrexx service API refused {method} {version}/service/{resource} (HTTP {(int)response.StatusCode}): {Trim(body)}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new PayrexxApiException(
                    $"The Payrexx service API refused the request ({(int)response.StatusCode}): {Trim(body)}");
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("data", out var data))
                {
                    throw new PayrexxApiException("The Payrexx service API answered without a 'data' member.");
                }

                // data ist je Endpunkt ein Objekt ODER eine Liste. Beides zulassen, statt sich auf die
                // Form eines einzelnen Endpunkts festzulegen.
                var element = data.ValueKind == JsonValueKind.Array
                    ? data.GetArrayLength() > 0 ? data[0] : default
                    : data;

                return element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                    ? default
                    : element.Deserialize<T>(Json);
            }
            catch (JsonException ex)
            {
                LogEnvironment.LogEvent(
                    $"The Payrexx service API answered {method} {version}/service/{resource} with something unreadable: {Trim(body)}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new PayrexxApiException("The Payrexx service API returned an unreadable response.", ex);
            }
        }

        private static string Trim(string value)
            => string.IsNullOrEmpty(value) ? "<empty>" : value.Length <= 400 ? value : value[..400] + "…";
    }
}
