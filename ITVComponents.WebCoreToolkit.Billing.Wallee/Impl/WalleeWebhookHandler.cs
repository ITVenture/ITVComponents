using System.Collections.Concurrent;
using System.Text.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Models;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Wallee.Client;
using Wallee.Model;
using Wallee.Service;
using Wallee.Util;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Impl
{
    /// <summary>
    /// Nimmt die Zustandsmeldungen von wallee entgegen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ohne diesen Weg erfährt die Anwendung nie, dass bezahlt wurde.</b> Die Erfolgsadresse läuft im
    /// Browser des Endkunden; wer das Fenster nach der Zahlung schliesst, hätte bezahlt, ohne dass die
    /// Ware freigegeben wird.
    /// </para>
    /// <para>
    /// Was gebucht wird, steht in <see cref="TenantSaleWebhookSink{TContext}"/>. Hier steht, was wallee
    /// anders macht als die anderen:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Die Meldung enthält keine Daten, nur eine Kennung.</b> wallee schickt
    /// <c>entityId</c> + <c>listenerEntityTechnicalName</c> und erwartet, dass die Entität danach über
    /// die API gelesen wird. Ist am Listener „payload signing and state" eingeschaltet, kommt der
    /// Zustand als <c>state</c> mit — dann entfällt der Rückruf für Transaktionen.</item>
    /// <item><b>Signiert wird mit einem privaten Schlüssel</b> (SHA256withECDSA), nicht mit einem
    /// gemeinsamen Geheimnis. Die Kopfzeile <c>x-signature</c> nennt die Kennung des öffentlichen
    /// Schlüssels; der wird bei wallee geholt und danach behalten — sonst käme auf jede Meldung ein
    /// zusätzlicher API-Aufruf, und zwar innerhalb der Zeit, in der geantwortet werden muss.</item>
    /// <item><b>Erstattungen sind eigene Entitäten</b> mit eigener Kennung — anders als bei Payrexx, wo
    /// nur ein Gesamtstand gemeldet wird. Das passt direkt auf
    /// <see cref="TenantSaleWebhookSink{TContext}.MirrorRefundAsync(TenantSale, TContext, string, long, string, CancellationToken)"/>.</item>
    /// <item><b>Beträge sind Dezimalzahlen in der Hauptwährung</b> und müssen zurückgerechnet werden.</item>
    /// </list>
    /// <para>
    /// <b>Noch nicht gegen einen echten Raum gelaufen.</b> Signaturen und Feldnamen stammen aus dem SDK
    /// selbst, der Ablauf ist ungeprüft.
    /// </para>
    /// </remarks>
    public class WalleeWebhookHandler<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Die bereits geholten öffentlichen Schlüssel, nach ihrer Kennung.
        /// </summary>
        /// <remarks>
        /// Statisch und ohne Verfallszeit: ein Schlüssel gehört zu seiner Kennung und ändert sich nicht —
        /// ein neuer Schlüssel bekommt eine neue Kennung und damit einen eigenen Eintrag. Die Menge ist
        /// entsprechend klein und wächst nicht mit dem Verkehr.
        /// </remarks>
        private static readonly ConcurrentDictionary<string, string> PublicKeys = new();

        private readonly TenantSaleWebhookSink<TContext> sink;
        private readonly IGlobalSettings<WalleeOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="WalleeWebhookHandler{TContext}"/> class.</summary>
        public WalleeWebhookHandler(TenantSaleWebhookSink<TContext> sink, IGlobalSettings<WalleeOptions> settings)
        {
            this.sink = sink;
            this.settings = settings;
        }

        /// <summary>
        /// Verarbeitet eine Zustandsmeldung.
        /// </summary>
        /// <param name="rawBody">
        /// der Rumpf, <b>unverändert</b> — über genau diese Zeichen läuft die Signatur
        /// </param>
        /// <param name="signatureHeader">der Wert der Kopfzeile <c>x-signature</c>, falls vorhanden</param>
        /// <param name="cancellationToken">bricht die Verarbeitung ab</param>
        /// <exception cref="WalleeWebhookRejectedException">
        /// wenn die Signatur nicht stimmt oder der Rumpf unlesbar ist
        /// </exception>
        public async Task HandleAsync(string rawBody, string? signatureHeader, CancellationToken cancellationToken)
        {
            var options = settings.Value;
            VerifySignature(rawBody, signatureHeader, options);

            var notice = Read(rawBody);
            var space = notice.SpaceId > 0 ? notice.SpaceId : options.SpaceId;
            if (space <= 0)
            {
                throw new WalleeWebhookRejectedException(
                    "The notification names no space and no 'WalleePayments:SpaceId' is configured; there is no way to read the entity it refers to.");
            }

            switch (notice.ListenerEntityTechnicalName)
            {
                case "Transaction":
                    await HandleTransactionAsync(notice, space, options, cancellationToken);
                    break;

                case "Refund":
                    await HandleRefundAsync(notice, space, options, cancellationToken);
                    break;

                default:
                    // Bestaetigt, nicht abgelehnt: ein Listener auf etwas anderem ist kein Fehler, und
                    // eine Absage liesse wallee wiederholen, was hier nie anders ausgehen wird.
                    LogEnvironment.LogDebugEvent(
                        $"A wallee notification for '{notice.ListenerEntityTechnicalName}' (entity {notice.EntityId}) is ignored: this handler only acts on Transaction and Refund.",
                        LogSeverity.Report, WalleeRuntime.LogContext);
                    break;
            }
        }

        /// <summary>
        /// Eine Transaktion hat den Zustand gewechselt.
        /// </summary>
        private async Task HandleTransactionAsync(WalleeWebhookNotice notice, long space, WalleeOptions options,
            CancellationToken cancellationToken)
        {
            await using var db = await sink.CreateContextAsync(cancellationToken);
            // Die Transaktions-Kennung IST, was beim Anlegen gespeichert wurde. Es braucht darum keinen
            // Rueckruf, um den Verkauf zu finden - und wenn der Zustand mitkommt, gar keinen.
            var sale = await sink.FindSaleAsync(db, notice.EntityId.ToString(), null, null, cancellationToken);
            if (sale == null)
            {
                LogEnvironment.LogEvent(
                    $"wallee reports transaction {notice.EntityId} in space {space}, but no sale references it. Nothing was booked.",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
                return;
            }

            var state = notice.State;
            if (string.IsNullOrWhiteSpace(state))
            {
                // Ohne eingeschaltete Zustandsmitgabe bleibt nur der Rueckruf. Das kostet einen Aufruf je
                // Meldung - am Listener laesst sich das mit 'payload signing and state' abstellen.
                var transaction = await ReadTransactionAsync(notice.EntityId, space, options, cancellationToken);
                state = transaction.State?.ToString();
            }

            switch (state)
            {
                case "FULFILL":
                case "COMPLETED":
                    // Die Zahlung steht. Als Kennung fuer die Erstattung dient die Transaktion selbst -
                    // wallee adressiert eine Erstattung ueber sie, nicht ueber eine eigene Zahlungs-Id.
                    var released = await sink.MarkPaidAsync(sale, db, notice.EntityId.ToString(), null,
                        cancellationToken);
                    if (released)
                    {
                        LogEnvironment.LogEvent(
                            $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) was paid through wallee (transaction {notice.EntityId}, state {state}).",
                            LogSeverity.Report, WalleeRuntime.LogContext);
                    }

                    break;

                case "FAILED":
                case "DECLINE":
                    await sink.MoveToAsync(sale, db, TenantSaleStatus.Failed, cancellationToken);
                    break;

                case "VOIDED":
                    await sink.MoveToAsync(sale, db, TenantSaleStatus.Canceled, cancellationToken);
                    break;

                case "AUTHORIZED":
                case "CREATE":
                case "PENDING":
                case "CONFIRMED":
                case "PROCESSING":
                    // Zwischenstaende, AUTHORIZED eingeschlossen: da ist der Betrag reserviert, aber nicht
                    // eingezogen. Wer hier freigaebe, lieferte gegen ein Versprechen.
                    LogEnvironment.LogDebugEvent(
                        $"wallee reports sale {sale.TenantSaleId} as '{state}'. Nothing is booked for this state.",
                        LogSeverity.Report, WalleeRuntime.LogContext);
                    break;

                default:
                    LogEnvironment.LogEvent(
                        $"wallee reports sale {sale.TenantSaleId} in the unknown transaction state '{state}'. Nothing was booked; this state needs to be added to the handler.",
                        LogSeverity.Warning, WalleeRuntime.LogContext);
                    break;
            }
        }

        /// <summary>
        /// Eine Erstattung hat den Zustand gewechselt — auch eine, die im Portal von wallee ausgelöst wurde.
        /// </summary>
        private async Task HandleRefundAsync(WalleeWebhookNotice notice, long space, WalleeOptions options,
            CancellationToken cancellationToken)
        {
            Refund refund;
            try
            {
                var service = new RefundsService(WalleeRuntime.Configure(options));
                // 'transaction' ausdruecklich anfordern: ohne expand kommt das Feld als null zurueck, und
                // dann fehlt genau die Kennung, ueber die der Verkauf gefunden wird.
                refund = await Task.Run(
                        () => service.GetPaymentRefundsId(notice.EntityId, space, ["transaction"]),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                throw new WalleeWebhookRejectedException(
                    $"Refund {notice.EntityId} in space {space} could not be read back from wallee: {ex.Message}", ex);
            }

            if (refund.State != RefundState.SUCCESSFUL)
            {
                // Eine Erstattung, die noch laeuft oder gescheitert ist, darf nicht gebucht werden - sonst
                // stuende auf der Zeile Geld als zurueckgegeben, das niemand bekommen hat.
                LogEnvironment.LogDebugEvent(
                    $"wallee refund {refund.Id} is in state '{refund.State}'. Nothing is booked until it is SUCCESSFUL.",
                    LogSeverity.Report, WalleeRuntime.LogContext);
                return;
            }

            await using var db = await sink.CreateContextAsync(cancellationToken);
            var sale = await sink.FindSaleAsync(db, refund.Transaction?.Id.ToString(),
                refund.MerchantReference, null, cancellationToken);
            if (sale == null)
            {
                LogEnvironment.LogEvent(
                    $"wallee reports refund {refund.Id} on transaction {refund.Transaction?.Id} (reference '{refund.MerchantReference}'), but no sale matches it. The refund is NOT in the local books.",
                    LogSeverity.Error, WalleeRuntime.LogContext);
                return;
            }

            // Zurueck in die kleinste Einheit. Von Hand mal 100 waere fuer JPY falsch.
            var amountMinor = CurrencyMinorUnits.ToMinor(refund.Amount, sale.Currency);
            await sink.MirrorRefundAsync(sale, db, refund.Id.ToString(), amountMinor, refund.State?.ToString(),
                cancellationToken);
        }

        /// <summary>Liest die Transaktion zurück, wenn die Meldung den Zustand nicht mitbringt.</summary>
        private static async Task<Transaction> ReadTransactionAsync(long id, long space, WalleeOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                var service = new TransactionsService(WalleeRuntime.Configure(options));
                // Reihenfolge: erst die Transaktion, DANN der Raum - beim Anlegen ist es andersherum.
                return await Task.Run(() => service.GetPaymentTransactionsId(id, space), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                throw new WalleeWebhookRejectedException(
                    $"Transaction {id} in space {space} could not be read back from wallee: {ex.Message}", ex);
            }
        }

        /// <summary>Liest den Rumpf der Meldung.</summary>
        private static WalleeWebhookNotice Read(string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                throw new WalleeWebhookRejectedException("The notification had an empty body.");
            }

            WalleeWebhookNotice? notice;
            try
            {
                notice = JsonSerializer.Deserialize<WalleeWebhookNotice>(rawBody, Json);
            }
            catch (JsonException ex)
            {
                throw new WalleeWebhookRejectedException($"The notification could not be read: {ex.Message}", ex);
            }

            if (notice == null || notice.EntityId <= 0)
            {
                throw new WalleeWebhookRejectedException(
                    "The notification names no entityId; there is nothing to act on.");
            }

            return notice;
        }

        /// <summary>
        /// Prüft die Signatur der Meldung.
        /// </summary>
        /// <remarks>
        /// Der öffentliche Schlüssel wird bei wallee unter der Kennung aus der Kopfzeile geholt und
        /// danach behalten — ein Aufruf je Schlüssel, nicht je Meldung.
        /// </remarks>
        private static void VerifySignature(string rawBody, string? signatureHeader, WalleeOptions options)
        {
            if (!options.VerifyWebhookSignatures)
            {
                LogEnvironment.LogEvent(
                    "A wallee notification was accepted WITHOUT checking its signature ('WalleePayments:VerifyWebhookSignatures' is off). Anyone who knows this endpoint can mark sales as paid. Switch payload signing on at the webhook listener and turn this back on.",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
                return;
            }

            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                throw new WalleeWebhookRejectedException(
                    "The notification carries no x-signature header. Either enable payload signing on the wallee webhook listener, or set 'WalleePayments:VerifyWebhookSignatures' to false and accept that the endpoint is then unprotected.");
            }

            // Kopfzeile: "algorithm=SHA256withECDSA, keyId=<uuid>, signature=<base64>". Der Pruefer des
            // SDK will NUR den Base64-Teil - ihm die ganze Zeile zu geben ergibt einen Base64-Fehler weit
            // weg von hier.
            var parts = ParseHeader(signatureHeader);
            if (!parts.TryGetValue("keyId", out var keyId) || !parts.TryGetValue("signature", out var signature))
            {
                throw new WalleeWebhookRejectedException(
                    $"The x-signature header is not in the expected 'algorithm=…, keyId=…, signature=…' form: '{signatureHeader}'.");
            }

            var algorithm = parts.TryGetValue("algorithm", out var named) ? named : "SHA256withECDSA";
            var publicKey = PublicKeys.GetOrAdd(keyId, id =>
            {
                try
                {
                    var service = new WebhookEncryptionKeysService(WalleeRuntime.Configure(options));
                    return service.GetWebhooksEncryptionKeysId(id);
                }
                catch (ApiException ex)
                {
                    throw new WalleeWebhookRejectedException(
                        $"The public key '{id}' named by the x-signature header could not be fetched from wallee: {ex.Message}", ex);
                }
            });

            if (!EncryptionUtil.IsContentValid(rawBody, signature, publicKey, algorithm))
            {
                throw new WalleeWebhookRejectedException(
                    $"The signature of the notification does not match (key '{keyId}', algorithm '{algorithm}').");
            }
        }

        /// <summary>Zerlegt <c>algorithm=…, keyId=…, signature=…</c> in seine Teile.</summary>
        private static Dictionary<string, string> ParseHeader(string header)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                // NUR am ERSTEN '=' trennen: Base64 endet oft auf '=' als Fuellzeichen, und ein Split
                // ueber alle Vorkommen schneidet die Signatur ab.
                var split = part.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                result[part[..split].Trim()] = part[(split + 1)..].Trim();
            }

            return result;
        }
    }

    /// <summary>
    /// Die Benachrichtigung wurde nicht angenommen.
    /// </summary>
    /// <remarks>
    /// Führt beim Aufrufer zu einer Absage, damit wallee es erneut versucht.
    /// </remarks>
    public class WalleeWebhookRejectedException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="WalleeWebhookRejectedException"/> class.</summary>
        public WalleeWebhookRejectedException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="WalleeWebhookRejectedException"/> class.</summary>
        public WalleeWebhookRejectedException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
