using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Options;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Der Zugang zur Kassen-Schnittstelle von Payrexx (ECR) — die dritte ihrer APIs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eigener Client neben <see cref="PayrexxApiClient"/> und <see cref="PayrexxServiceApiClient"/>,
    /// und zwar nicht aus Ordnungsliebe: die Anmeldung gleicht zwar der Händler-API
    /// (<c>X-API-KEY</c> plus <c>instance</c> als Abfrageparameter), die <b>Hülle der Antwort aber
    /// nicht</b>. Dort ist <c>data</c> eine Liste, hier ein Objekt. Denselben Leser für beides zu
    /// benutzen, endet in einer Antwort, die als leer gilt, obwohl sie da ist.
    /// </para>
    /// <para>
    /// <b>Der Ablauf ist fragend, nicht wartend:</b> <c>POST …/payment</c> beauftragt das Gerät und
    /// kehrt zurück; ob die Karte belastet wurde, sagt erst <c>GET …/payment/{id}</c>. Payrexx hat dafür
    /// eigens einen Zustand <c>UNKNOWN</c> — dieselbe Einsicht, die auch bei den beiden anderen
    /// Anbietern zum Nachfragen zwingt.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Gerät gelaufen.</b> Die Pfade unterhalb von <c>/payment</c>
    /// (Nachfragen, Abbrechen, Stornieren) stehen in der Endpunktliste nur mit ihrem Namen; ihre genaue
    /// Form ist hier angenommen und beim ersten Testlauf zu prüfen.
    /// </para>
    /// </remarks>
    public sealed class PayrexxEcrClient
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient http;
        private readonly IGlobalSettings<PayrexxOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="PayrexxEcrClient"/> class.</summary>
        public PayrexxEcrClient(HttpClient http, IGlobalSettings<PayrexxOptions> settings)
        {
            this.http = http;
            this.settings = settings;
        }

        /// <summary>Die Einstellungen dieses Anbieters.</summary>
        public PayrexxOptions Options => settings.Value;

        /// <summary>Beauftragt ein Gerät oder fragt es ab.</summary>
        /// <param name="method">die Methode</param>
        /// <param name="resource">der Pfad unterhalb der Basis, ohne führenden Schrägstrich</param>
        /// <param name="payload">der Rumpf, oder null</param>
        public async Task<PayrexxEcrPayment?> SendAsync(HttpMethod method, string resource, object? payload,
            CancellationToken cancellationToken)
        {
            var options = Options;
            var url = BuildUrl(options, resource);

            using var request = new HttpRequestMessage(method, url);
            request.Headers.Add("X-API-KEY", options.ApiSecret);
            if (payload != null)
            {
                request.Content = JsonContent.Create(payload, options: Json);
            }

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // KEINE Uebersetzung in "fehlgeschlagen". Dass die Leitung abbrach, sagt nichts darueber,
                // ob das Geraet den Auftrag bekommen hat - und der Aufrufer muss diesen Unterschied sehen.
                throw new PayrexxApiException(
                    $"The Payrexx terminal API at '{url}' did not answer: {ex.Message}", ex);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new PayrexxApiException(
                    $"The Payrexx terminal API answered {(int)response.StatusCode} for '{resource}': {Shorten(body)}");
            }

            PayrexxEcrEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<PayrexxEcrEnvelope>(body, Json);
            }
            catch (JsonException ex)
            {
                throw new PayrexxApiException(
                    $"The answer of the Payrexx terminal API to '{resource}' could not be read: {ex.Message}", ex);
            }

            if (string.Equals(envelope?.Status, "error", StringComparison.OrdinalIgnoreCase))
            {
                // Wie bei der Haendler-API: Fehler kommen als HTTP 200 mit status:error. Wer nur den
                // Statuscode prueft, haelt jeden Fehler fuer einen Erfolg.
                throw new PayrexxApiException(
                    $"The Payrexx terminal API refused '{resource}': {envelope?.Message ?? "no reason given"}");
            }

            return envelope?.Data;
        }

        /// <summary>
        /// Setzt die Adresse zusammen.
        /// </summary>
        /// <remarks>
        /// Der Instanzname reist als Abfrageparameter mit, nicht im Kopf — so verlangt es Payrexx, und er
        /// ist kein Geheimnis. Im White-Label-Betrieb ist die Basis <b>nicht</b> <c>api.payrexx.com</c>,
        /// sondern die Domain der Plattform; das steht in <see cref="PayrexxOptions.ApiBaseUrl"/>.
        /// </remarks>
        private static string BuildUrl(PayrexxOptions options, string resource)
        {
            var baseUrl = options.ApiBaseUrl.TrimEnd('/');
            var separator = resource.Contains('?') ? '&' : '?';
            return $"{baseUrl}/{resource.TrimStart('/')}{separator}instance={Uri.EscapeDataString(options.Instance)}";
        }

        private static string Shorten(string body)
            => string.IsNullOrWhiteSpace(body) ? "(empty)" : body.Length <= 512 ? body : body[..512] + "…";

        /// <summary>
        /// Die Hülle der ECR-Antwort.
        /// </summary>
        /// <remarks>
        /// <c>data</c> ist hier ein Objekt und keine Liste — anders als bei der Händler-API. Der Wandler
        /// unten nimmt trotzdem beides an: die Doku zeigt ein Objekt, die übrigen Endpunkte desselben
        /// Hosts liefern Listen, und eine Antwort deswegen zu verwerfen wäre der teuerste Umgang mit
        /// dieser Unsicherheit.
        /// </remarks>
        private sealed class PayrexxEcrEnvelope
        {
            public string? Status { get; set; }

            public string? Message { get; set; }

            [JsonConverter(typeof(SingleOrListConverter))]
            public PayrexxEcrPayment? Data { get; set; }
        }

        /// <summary>Nimmt <c>data</c> als Objekt ODER als einelementige Liste.</summary>
        private sealed class SingleOrListConverter : JsonConverter<PayrexxEcrPayment?>
        {
            public override PayrexxEcrPayment? Read(ref Utf8JsonReader reader, Type typeToConvert,
                JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    var list = JsonSerializer.Deserialize<List<PayrexxEcrPayment>>(ref reader, options);
                    return list is { Count: > 0 } ? list[0] : null;
                }

                return JsonSerializer.Deserialize<PayrexxEcrPayment>(ref reader, options);
            }

            public override void Write(Utf8JsonWriter writer, PayrexxEcrPayment? value, JsonSerializerOptions options)
                => JsonSerializer.Serialize(writer, value, options);
        }
    }

    /// <summary>Ein Kassenvorgang, wie Payrexx ihn führt.</summary>
    public sealed class PayrexxEcrPayment
    {
        /// <summary>Die Kennung des Vorgangs bei Payrexx.</summary>
        public string? PaymentId { get; set; }

        /// <summary>
        /// <c>PAYMENT_REQUESTED</c>, <c>IN_PROGRESS</c>, <c>SUCCESS</c>, <c>DECLINED</c>,
        /// <c>UNDERPAID</c>, <c>TERMINATED</c>, <c>REVERTED</c>, <c>EXPIRED</c>, <c>FAILED</c>,
        /// <c>UNKNOWN</c>. Roh übernommen.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>Die Seriennummer des Geräts.</summary>
        public string? Terminal { get; set; }

        /// <summary>Der Betrag in Rappen.</summary>
        public long Amount { get; set; }

        /// <summary>Das Trinkgeld in Rappen.</summary>
        public long TipAmount { get; set; }

        /// <summary>Die Währung.</summary>
        public string? Currency { get; set; }

        /// <summary><c>CHARGE</c> oder <c>REFUND</c>.</summary>
        public string? Type { get; set; }

        /// <summary>Womit gezahlt wurde.</summary>
        public string? PaymentMethod { get; set; }

        /// <summary>Die Kartenmarke.</summary>
        public string? CardBrand { get; set; }

        /// <summary>Unsere Referenz, unverändert zurück.</summary>
        public string? PaymentReference { get; set; }

        /// <summary>
        /// Die Zeilen des Kartenbelegs, wie das Gerät sie geliefert hat.
        /// </summary>
        /// <remarks>
        /// Diesen Text dem selbst zusammengebauten vorziehen: was auf einem Kartenbeleg stehen muss,
        /// weiss der Abwickler besser als wir, und er unterscheidet sich je nach Karte und Land.
        /// </remarks>
        public List<string>? Slip { get; set; }
    }
}
