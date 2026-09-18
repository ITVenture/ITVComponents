using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Models;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Nimmt die Zahlungsmeldungen von Payrexx entgegen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ohne diesen Weg erfährt die Anwendung nie, dass bezahlt wurde.</b> Die Rückkehr-Adresse aus dem
    /// Zahlungsvorgang ist dafür untauglich: sie läuft im Browser des Endkunden, und wer das Fenster nach
    /// der Zahlung schliesst, hätte bezahlt, ohne dass die Ware freigegeben wird.
    /// </para>
    /// <para>
    /// Was gebucht wird, steht in <see cref="TenantSaleWebhookSink{TContext}"/> — für alle Anbieter
    /// dasselbe. Hier steht nur, was Payrexx anders macht:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Zwei Formate.</b> Im Portal lässt sich zwischen <c>application/json</c> und
    /// <c>application/x-www-form-urlencoded</c> („Normal (PHP-Post)") wählen. Beide werden gelesen — eine
    /// falsch eingestellte Instanz soll nicht dazu führen, dass Zahlungen unbemerkt liegen bleiben.</item>
    /// <item><b>Die Signatur ist hex, nicht Base64.</b> <c>X-Webhook-Signature</c> ist ein
    /// kleingeschriebener Hex-HMAC-SHA256 über den <b>rohen</b> Rumpf, mit dem Signierschlüssel als
    /// UTF-8-Text — nicht Base64-dekodiert. Jede dieser drei Abweichungen ergibt eine Prüfung, die
    /// immer fehlschlägt.</item>
    /// <item><b>Erstattungen kommen als Gesamtstand.</b> Payrexx meldet keine einzelne Erstattung,
    /// sondern die Transaktion mit <c>refunded</c> / <c>partially-refunded</c> und
    /// <c>invoice.refundedAmount</c>.</item>
    /// <item><b>Payrexx wiederholt bis zu zehnmal</b>, über mehrere Tage. Eine Meldung, die wir
    /// annehmen und verlieren, ist damit endgültig verloren — darum wird bei einem Fehler abgelehnt und
    /// nicht bestätigt.</item>
    /// </list>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Konto gelaufen.</b> Feldnamen und Signaturverfahren stammen aus der
    /// öffentlichen Dokumentation.
    /// </para>
    /// </remarks>
    public class PayrexxWebhookHandler<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly TenantSaleWebhookSink<TContext> sink;
        private readonly IGlobalSettings<PayrexxOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="PayrexxWebhookHandler{TContext}"/> class.</summary>
        public PayrexxWebhookHandler(TenantSaleWebhookSink<TContext> sink, IGlobalSettings<PayrexxOptions> settings)
        {
            this.sink = sink;
            this.settings = settings;
        }

        /// <summary>
        /// Verarbeitet eine Benachrichtigung.
        /// </summary>
        /// <param name="rawBody">
        /// der Rumpf, <b>unverändert</b>. Über genau diese Zeichen läuft die Signatur — neu
        /// zusammengesetztes JSON ergibt eine andere.
        /// </param>
        /// <param name="signature">der Wert der Kopfzeile <c>X-Webhook-Signature</c>, falls vorhanden</param>
        /// <param name="contentType">der gemeldete Inhaltstyp, um das Format zu erkennen</param>
        /// <param name="cancellationToken">bricht die Verarbeitung ab</param>
        /// <exception cref="PayrexxWebhookRejectedException">
        /// wenn die Signatur nicht stimmt oder der Rumpf unlesbar ist. Der Aufrufer antwortet darauf mit
        /// einem Fehlercode, damit Payrexx es erneut versucht.
        /// </exception>
        public async Task HandleAsync(string rawBody, string? signature, string? contentType,
            CancellationToken cancellationToken)
        {
            var options = settings.Value;
            VerifySignature(rawBody, signature, options.WebhookSecret);

            var transaction = Parse(rawBody, contentType);
            if (transaction == null)
            {
                // Kein Fehler: Payrexx schickt auch Meldungen ohne Transaktion (Auszahlungen etwa). Die
                // gehen uns nichts an, und sie ABZULEHNEN hiesse, Payrexx tagelang wiederholen zu lassen.
                LogEnvironment.LogDebugEvent(
                    "A Payrexx notification arrived without a transaction. It is acknowledged and ignored — payout notifications look like this.",
                    LogSeverity.Report, PayrexxApiClient.LogContext);
                return;
            }

            await using var db = await sink.CreateContextAsync(cancellationToken);
            var sale = await sink.FindSaleAsync(db,
                transaction.Invoice?.PaymentRequestId?.ToString(),
                transaction.ReferenceId ?? transaction.Invoice?.ReferenceId,
                null, cancellationToken);

            if (sale == null)
            {
                // Bestaetigt, nicht abgelehnt: eine Zahlung, die zu keinem Verkauf von UNS gehoert, wird
                // auch beim zehnten Versuch keinem gehoeren. Protokolliert wird sie trotzdem - wenn hier
                // Geld ankommt, das niemand erwartet, muss das sichtbar sein.
                LogEnvironment.LogEvent(
                    $"Payrexx reports transaction {transaction.Id} (status '{transaction.Status}', reference '{transaction.ReferenceId}', gateway {transaction.Invoice?.PaymentRequestId}) but no sale matches it. Nothing was booked.",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
                return;
            }

            await ApplyAsync(sale, db, transaction, cancellationToken);
        }

        /// <summary>
        /// Übersetzt den Zustand einer Payrexx-Transaktion in das, was mit dem Verkauf geschieht.
        /// </summary>
        private async Task ApplyAsync(TenantSale sale, TContext db, PayrexxWebhookTransaction transaction,
            CancellationToken cancellationToken)
        {
            var status = transaction.Status?.Trim().ToLowerInvariant() ?? string.Empty;
            // Die Kennung, gegen die spaeter erstattet wird. Die UUID, nicht die Nummer - und bei einer
            // Erstattungs-Transaktion die der ZAHLUNG, nicht die eigene.
            var chargeUuid = transaction.OriginalTransactionUuid ?? transaction.SourceTransactionUuid
                ?? transaction.Uuid;
            // Eine abgeleitete Transaktion ist eine Erstattung, keine Zahlung - auch wenn sie
            // 'confirmed' meldet. Sie hier durchzulassen hiesse, aus einer bestaetigten Rueckzahlung eine
            // bestaetigte Zahlung zu machen.
            var isDerived = !string.IsNullOrWhiteSpace(transaction.OriginalTransactionUuid)
                            || !string.IsNullOrWhiteSpace(transaction.SourceTransactionUuid);

            switch (status)
            {
                case "confirmed" when isDerived:
                    await MirrorTotalAsync(sale, db, transaction, status, strict: false, cancellationToken);
                    break;

                case "confirmed":
                    var released = await sink.MarkPaidAsync(sale, db, chargeUuid,
                        transaction.Contact?.Email, cancellationToken);
                    if (released)
                    {
                        LogEnvironment.LogEvent(
                            $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) was paid through Payrexx{PaymentMeanSuffix(transaction)}.",
                            LogSeverity.Report, PayrexxApiClient.LogContext);
                    }

                    break;

                case "refunded":
                case "partially-refunded":
                    // Die Zahlung selbst kann in derselben Meldung noch ungebucht sein, wenn die
                    // Bestaetigung verloren ging und die Erstattung schnell folgte. Erst bezahlen, dann
                    // erstatten - andersherum stuende eine Erstattung auf einem offenen Verkauf.
                    if (!isDerived)
                    {
                        await sink.MarkPaidAsync(sale, db, chargeUuid, transaction.Contact?.Email,
                            cancellationToken);
                    }

                    await MirrorTotalAsync(sale, db, transaction, status, strict: true, cancellationToken);
                    break;

                case "cancelled":
                    await sink.MoveToAsync(sale, db, TenantSaleStatus.Canceled, cancellationToken);
                    break;

                case "expired":
                    await sink.MoveToAsync(sale, db, TenantSaleStatus.Expired, cancellationToken);
                    break;

                case "declined":
                case "error":
                    await sink.MoveToAsync(sale, db, TenantSaleStatus.Failed, cancellationToken);
                    break;

                case "chargeback":
                case "disputed":
                    // Kein eigener Zustand dafuer, und eine Rueckbelastung ist KEINE Erstattung: das Geld
                    // ist weg, die Ware ist draussen, und was zu tun ist, entscheidet ein Mensch. Der
                    // Verkauf bleibt darum unveraendert - aber laut.
                    LogEnvironment.LogEvent(
                        $"Payrexx reports a {status} on sale {sale.TenantSaleId} (tenant {sale.TenantId}, transaction {transaction.Uuid}, {transaction.Amount} {transaction.Currency}). The sale is left as it is: this is money taken back, not a refund, and it needs a decision.",
                        LogSeverity.Error, PayrexxApiClient.LogContext);
                    break;

                case "waiting":
                case "authorized":
                case "reserved":
                case "refund_pending":
                    // Zwischenstaende. Nichts zu tun, aber sichtbar zu halten: wenn ein Verkauf in
                    // 'authorized' haengen bleibt, ist genau das die Spur.
                    LogEnvironment.LogDebugEvent(
                        $"Payrexx reports sale {sale.TenantSaleId} as '{status}'. Nothing is booked for this state.",
                        LogSeverity.Report, PayrexxApiClient.LogContext);
                    break;

                default:
                    // Die Liste gehoert Payrexx und waechst. Ein unbekannter Zustand wird nicht geraten -
                    // aber er wird gemeldet, damit er beim naechsten Mal hier steht.
                    LogEnvironment.LogEvent(
                        $"Payrexx reports sale {sale.TenantSaleId} in the unknown state '{transaction.Status}'. Nothing was booked; this state needs to be added to the handler.",
                        LogSeverity.Warning, PayrexxApiClient.LogContext);
                    break;
            }
        }

        /// <summary>
        /// Gleicht die Erstattungen an den Gesamtstand an, den die Meldung nennt.
        /// </summary>
        /// <param name="strict">
        /// ob ein fehlender Gesamtstand ein Fehler ist. Bei <c>refunded</c> / <c>partially-refunded</c>
        /// ja — dort ist er der Inhalt der Meldung. Bei einer bestätigten Erstattungs-Transaktion nein:
        /// sie trägt den Stand nicht immer, und die zugehörige Meldung auf der Zahlung kommt ohnehin.
        /// </param>
        private async Task MirrorTotalAsync(TenantSale sale, TContext db, PayrexxWebhookTransaction transaction,
            string status, bool strict, CancellationToken cancellationToken)
        {
            var total = transaction.Invoice?.RefundedAmount;
            if (total is null or <= 0)
            {
                if (strict)
                {
                    LogEnvironment.LogEvent(
                        $"Payrexx reports sale {sale.TenantSaleId} as '{status}' but names no refunded amount (invoice.refundedAmount). Nothing was booked — the amount must be reconciled by hand.",
                        LogSeverity.Error, PayrexxApiClient.LogContext);
                }
                else
                {
                    LogEnvironment.LogDebugEvent(
                        $"Payrexx confirms a refund transaction on sale {sale.TenantSaleId} without a refunded total. Nothing is booked from this message; the one on the payment carries it.",
                        LogSeverity.Report, PayrexxApiClient.LogContext);
                }

                return;
            }

            await sink.MirrorRefundTotalAsync(sale, db, total.Value, "payrexx", status, cancellationToken);
        }

        private static string PaymentMeanSuffix(PayrexxWebhookTransaction transaction)
            => string.IsNullOrWhiteSpace(transaction.PaymentMean) ? string.Empty : $" ({transaction.PaymentMean})";

        /// <summary>
        /// Prüft die Signatur — oder sagt, dass sie nicht geprüft wird.
        /// </summary>
        /// <remarks>
        /// Ohne hinterlegtes Geheimnis wird durchgelassen, weil Payrexx die Signierung optional führt und
        /// ein Betrieb sie nicht eingeschaltet haben muss. Stillschweigend geschieht das aber nicht: wer
        /// diesen Endpunkt kennt, kann sonst beliebige Verkäufe als bezahlt melden, und das gehört ins
        /// Protokoll, bevor es jemand ausnutzt.
        /// </remarks>
        private static void VerifySignature(string rawBody, string? signature, string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                LogEnvironment.LogEvent(
                    "A Payrexx notification was accepted WITHOUT checking its signature: no 'Payrexx:WebhookSecret' is configured. Anyone who knows this endpoint can mark sales as paid. Set up webhook signing in the Payrexx portal and store the key.",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
                return;
            }

            if (string.IsNullOrEmpty(signature))
            {
                throw new PayrexxWebhookRejectedException(
                    "The notification carries no X-Webhook-Signature header, but a signing key is configured.");
            }

            // Der Schluessel ist UTF-8-TEXT, nicht Base64. Ihn zu dekodieren ergibt einen anderen
            // Schluessel und damit eine Pruefung, die immer fehlschlaegt - und zwar so, als waere der
            // Schluessel falsch hinterlegt.
            var expected = Convert.ToHexStringLower(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody)));

            // Zeitkonstanter Vergleich: ein gewoehnlicher bricht beim ersten abweichenden Zeichen ab und
            // verraet damit ueber die Laufzeit, wie weit ein Versuch gekommen ist.
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant())))
            {
                throw new PayrexxWebhookRejectedException(
                    "The signature of the notification does not match. Note that the key is used as UTF-8 text (not Base64-decoded) and the result is lower-case hex (not Base64) — a mismatch in either makes every check fail.");
            }
        }

        /// <summary>
        /// Liest den Rumpf, in welchem der beiden Formate er auch kommt.
        /// </summary>
        private static PayrexxWebhookTransaction? Parse(string rawBody, string? contentType)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                throw new PayrexxWebhookRejectedException("The notification had an empty body.");
            }

            // Nach dem INHALT entscheiden, nicht nur nach der Kopfzeile: der Inhaltstyp fehlt bei manchen
            // Vermittlern, und ein '{' am Anfang ist die verlaesslichere Aussage.
            var looksLikeJson = rawBody.TrimStart().StartsWith('{');
            if (looksLikeJson || contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    return JsonSerializer.Deserialize<PayrexxWebhookNotification>(rawBody, Json)?.Transaction;
                }
                catch (JsonException ex)
                {
                    throw new PayrexxWebhookRejectedException(
                        $"The notification claimed to be JSON but could not be read: {ex.Message}", ex);
                }
            }

            return ParseForm(rawBody);
        }

        /// <summary>
        /// Liest das <c>transaction[feld]</c>-Format der PHP-Post-Einstellung.
        /// </summary>
        /// <remarks>
        /// Bewusst von Hand und bewusst schmal: aus der Form <c>transaction[invoice][refundedAmount]</c>
        /// wird der Pfad <c>invoice.refundedAmount</c>, und gelesen werden nur die Felder, aus denen
        /// unten wirklich etwas gebucht wird. Ein vollständiger Parser wäre mehr Code für Felder, die
        /// niemand liest.
        /// </remarks>
        private static PayrexxWebhookTransaction? ParseForm(string rawBody)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in rawBody.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var split = pair.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..split].Replace('+', ' '));
                var value = Uri.UnescapeDataString(pair[(split + 1)..].Replace('+', ' '));
                if (!key.StartsWith("transaction[", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // transaction[invoice][refundedAmount] -> invoice.refundedAmount
                var path = key["transaction".Length..].Replace("][", ".").Trim('[', ']');
                values[path] = value;
            }

            if (values.Count == 0)
            {
                return null;
            }

            var transaction = new PayrexxWebhookTransaction
            {
                Id = ReadLong(values, "id") ?? 0,
                Uuid = Read(values, "uuid"),
                Status = Read(values, "status"),
                ReferenceId = Read(values, "referenceId"),
                Amount = ReadLong(values, "amount") ?? 0,
                Currency = Read(values, "currency"),
                PaymentMean = Read(values, "paymentMean"),
                OriginalTransactionUuid = Read(values, "originalTransactionUuid"),
                SourceTransactionUuid = Read(values, "sourceTransactionUuid")
            };

            var paymentRequestId = ReadLong(values, "invoice.paymentRequestId");
            var refundedAmount = ReadLong(values, "invoice.refundedAmount");
            var invoiceReference = Read(values, "invoice.referenceId");
            if (paymentRequestId != null || refundedAmount != null || invoiceReference != null)
            {
                transaction.Invoice = new PayrexxWebhookInvoice
                {
                    PaymentRequestId = paymentRequestId,
                    RefundedAmount = refundedAmount,
                    ReferenceId = invoiceReference,
                    OriginalAmount = ReadLong(values, "invoice.originalAmount")
                };
            }

            var email = Read(values, "contact.email");
            if (!string.IsNullOrWhiteSpace(email))
            {
                transaction.Contact = new PayrexxWebhookContact { Email = email };
            }

            return transaction;
        }

        private static string? Read(Dictionary<string, string> values, string key)
            => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

        private static long? ReadLong(Dictionary<string, string> values, string key)
            => Read(values, key) is { } raw && long.TryParse(raw, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Die Benachrichtigung wurde nicht angenommen.
    /// </summary>
    /// <remarks>
    /// Führt beim Aufrufer zu einer Absage, damit Payrexx es erneut versucht. Eine bestätigte und dann
    /// verworfene Meldung wäre dagegen endgültig weg.
    /// </remarks>
    public class PayrexxWebhookRejectedException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="PayrexxWebhookRejectedException"/> class.</summary>
        public PayrexxWebhookRejectedException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="PayrexxWebhookRejectedException"/> class.</summary>
        public PayrexxWebhookRejectedException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
