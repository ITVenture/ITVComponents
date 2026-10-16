using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Models;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Verkäufe über Payrexx: die gehostete Zahlungsseite heisst dort <b>Gateway</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Buchführung liegt in <see cref="TenantSaleServiceBase{TContext}"/>. Hier steht nur, was Payrexx
    /// anders macht als andere — und das ist einiges:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Betrag in Rappen, aber die Waehrung als ISO-Code:</b> <c>amount</c> ist ganzzahlig in der
    /// kleinsten Einheit, <c>currency</c> gross geschrieben. Wer den Betrag als Franken schickt, verkauft
    /// fuer ein Hundertstel - und niemand merkt es, weil die Seite anstandslos erscheint.</item>
    /// <item><b>Keine Idempotenz im Protokoll.</b> Payrexx kennt keinen Idempotenz-Schluessel. Ein zweiter
    /// Aufruf legt eine ZWEITE Zahlungsseite an. Abgefangen wird das von der Basis, die eine noch gueltige
    /// Seite wiederverwendet und erst bei einem bewussten neuen Anlauf hierher durchstellt.</item>
    /// <item><b>Die Rueckkehr-Adressen muessen kodiert sein</b> - das verlangt die Doku ausdruecklich.</item>
    /// </list>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Konto gelaufen.</b> Feldnamen und Ablauf stammen aus der
    /// oeffentlichen API-Referenz. Was dort nicht steht, ist unten als offene Frage vermerkt, statt es zu
    /// raten.
    /// </para>
    /// </remarks>
    public class PayrexxSaleService<TContext> : TenantSaleServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private readonly PayrexxApiClient api;
        private readonly PayrexxServiceApiClient serviceApi;
        private readonly IProviderCredentialStore? credentials;

        /// <summary>Initializes a new instance of the <see cref="PayrexxSaleService{TContext}"/> class.</summary>
        public PayrexxSaleService(IDbContextFactory<TContext> dbFactory, PayrexxApiClient api,
            PayrexxServiceApiClient serviceApi,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
            IEnumerable<ITenantSaleObserver> observers, IProviderCredentialStore? credentials = null)
            : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()),
                   new TenantSaleNotifier(observers))
        {
            this.api = api;
            this.serviceApi = serviceApi;
            this.credentials = credentials;
        }

        /// <summary>Die Instanz, auf der dieser Verkauf laeuft - eingefroren beim Anlegen.</summary>
        private async Task<PayrexxConnection> ConnectionForAsync(TenantSale sale, CancellationToken cancellationToken)
            => await PayrexxConnection.ForAsync(await AccessForSaleAsync(sale, cancellationToken), api.Options,
                credentials, cancellationToken);

        /// <inheritdoc />
        protected override string ProviderKey => "payrexx";

        /// <inheritdoc />
        /// <remarks>
        /// Ein eigener Zugang ist eine eigene Payrexx-Instanz des Mandanten (<see cref="PayrexxConnection"/>). Erstattet
        /// wird dort ueber die HAENDLER-API, denn die Service-API der Plattform reicht nicht in eine fremde Instanz.
        /// </remarks>
        protected override bool SupportsOwnAccess => true;

        /// <inheritdoc />
        protected override async Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
            TenantPaymentAccount? account, int attempt, CancellationToken cancellationToken)
        {
            // Ohne Plattform-Konto kommt nur ein Verkauf ueber den eigenen Zugang hier an. Die Instanz sagt der
            // Zugang des Verkaufs, nicht das Konto.
            var options = Runtime.Options;
            var connection = await ConnectionForAsync(sale, cancellationToken);

            var payload = new Dictionary<string, object?>
            {
                // Rappen, nicht Franken - siehe die Warnung oben.
                ["amount"] = sale.AmountMinor,
                ["currency"] = sale.Currency.ToUpperInvariant(),
                ["purpose"] = sale.Description,
                // Der Weg zurueck zu dieser Zeile. Payrexx gibt die Referenz in der Benachrichtigung
                // unveraendert wieder heraus.
                ["referenceId"] = sale.ExternalReference,
                ["successRedirectUrl"] = request.SuccessUrl,
                ["failedRedirectUrl"] = request.CancelUrl,
                ["cancelRedirectUrl"] = request.CancelUrl,
                ["validity"] = Math.Clamp(options.CheckoutExpiryMinutes, 30, 1440)
            };

            if (sale.ApplicationFeeMinor > 0)
            {
                // Die Provision reist NICHT mit: anders als bei Stripes application_fee kennt Payrexx kein
                // Feld dafuer. Im Plattform-Modell entsteht die Marge aus der Differenz zwischen dem, was
                // Payrexx der Plattform berechnet, und dem, was die Plattform dem Haendler berechnet - das
                // wird in den Plattform-Konditionen gesetzt, nicht je Verkauf. Der eingefrorene Betrag auf
                // der Zeile bleibt darum eine reine BUCHGROESSE.
                LogEnvironment.LogEvent(
                    $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) carries a commission of {sale.ApplicationFeeMinor} {sale.Currency}. Payrexx has no per-transaction commission field — the platform's margin comes from its own tariff, so this amount is a local booking figure and is not withheld here.",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
            }

            if (connection.PaymentMeans is { Length: > 0 })
            {
                // Der Hebel gegen die Gebuehren: steht TWINT hier vorn, zahlt der Endkunde eher damit, und
                // das kostet rund einen Prozentpunkt weniger als eine Karte.
                payload["pm"] = connection.PaymentMeans;
            }

            if (!string.IsNullOrWhiteSpace(sale.CustomerEmail))
            {
                // Nur fuer den Beleg. Ein Kundenprofil entsteht dadurch nicht - der Kauf bleibt ad hoc.
                payload["fields"] = new Dictionary<string, object> { ["email"] = new { value = sale.CustomerEmail } };
            }

            try
            {
                var gateway = await api.PostAsync<PayrexxGateway>(connection, "Gateway/", payload, cancellationToken)
                              ?? throw new PayrexxApiException("Payrexx accepted the gateway but returned nothing.");

                if (string.IsNullOrWhiteSpace(gateway.Link))
                {
                    // Ohne Adresse ist die Seite wertlos, und der Verkauf wuerde als zahlbar gelten, ohne es
                    // zu sein. Lieber hier scheitern als beim Endkunden.
                    throw new PayrexxApiException($"Payrexx gateway {gateway.Id} came back without a payment link.");
                }

                return new ProviderCheckout(gateway.Id.ToString(), gateway.Link!);
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not create a payment page for sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}', attempt {attempt}): {ex.OutlineException()}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Payrexx schliesst eine Zahlungsseite, indem man sie LOESCHT. Vorher wird sie gelesen: eine bezahlte
        /// Seite zu loeschen, wuerde die Antwort auf die eigentliche Frage - kann hier noch bezahlt werden? -
        /// verwischen. Noch nicht gegen ein echtes Konto gelaufen, wie der Rest dieser Klasse.
        /// </remarks>
        protected override async Task<ProviderCancel> CancelCheckoutAsync(TenantSale sale, CancellationToken cancellationToken)
        {
            var resource = $"Gateway/{sale.ProviderSessionId}/";
            var connection = await ConnectionForAsync(sale, cancellationToken);
            var before = await ReadGatewayAsync(connection, sale, resource, cancellationToken);
            var verdict = Classify(before);
            if (verdict != null)
            {
                return verdict.Value;
            }

            try
            {
                await api.DeleteAsync<PayrexxGateway>(connection, resource, cancellationToken);
                return new ProviderCancel(ProviderCancelOutcome.Canceled);
            }
            catch (PayrexxApiException ex)
            {
                // Zwischen Lesen und Loeschen kann bezahlt worden sein. Was es war, sagt ein zweiter Blick.
                LogEnvironment.LogEvent(
                    $"Payrexx refused to delete gateway {sale.ProviderSessionId} of sale {sale.TenantSaleId}; reading its state: {ex.OutlineException()}",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
            }

            var after = await ReadGatewayAsync(connection, sale, resource, cancellationToken);
            return Classify(after)
                   ?? throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                       $"Payrexx gateway {sale.ProviderSessionId} is still '{after.Status}' but could not be deleted.");
        }

        private async Task<PayrexxGateway> ReadGatewayAsync(PayrexxConnection connection, TenantSale sale, string resource,
            CancellationToken cancellationToken)
        {
            try
            {
                return await api.GetAsync<PayrexxGateway>(connection, resource, cancellationToken)
                       ?? throw new PayrexxApiException($"Payrexx returned nothing for gateway {sale.ProviderSessionId}.");
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read gateway {sale.ProviderSessionId} of sale {sale.TenantSaleId}; the sale stays pending: {ex.OutlineException()}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <summary>
        /// Was der Zustand einer Zahlungsseite fuer den Abbruch bedeutet - oder null, solange sie offen ist
        /// und geloescht werden kann.
        /// </summary>
        private static ProviderCancel? Classify(PayrexxGateway gateway)
            => gateway.Status?.Trim().ToLowerInvariant() switch
            {
                "waiting" => null,
                // Die Transaktions-UUID steht nicht an der Seite. Gebucht wird ohne; die Bestaetigung per
                // Webhook folgt und traegt sie nach (die Senke ergaenzt Kennungen auch bei einer Wiederholung).
                "confirmed" => new ProviderCancel(ProviderCancelOutcome.Paid),
                "authorized" or "reserved" => new ProviderCancel(ProviderCancelOutcome.InProgress,
                    Detail: $"the gateway is '{gateway.Status}' — the amount is reserved, not yet collected"),
                "cancelled" or "declined" or "expired" => new ProviderCancel(ProviderCancelOutcome.Canceled),
                // Die Liste gehoert Payrexx. Ein unbekannter Zustand wird nicht als "tot" geraten.
                _ => new ProviderCancel(ProviderCancelOutcome.InProgress,
                    Detail: $"the gateway is in the unknown state '{gateway.Status}'")
            };

        /// <inheritdoc />
        protected override async Task<ProviderRefund> CreateRefundAsync(TenantSale sale, long amountMinor,
            string? reason, bool refundApplicationFee, string accountId, long alreadyRefundedMinor,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sale.ProviderChargeId))
            {
                throw new TenantPaymentException(PaymentErrorCodes.MissingCharge,
                    $"Sale {sale.TenantSaleId} has no Payrexx transaction on record; the refund cannot be addressed.");
            }

            if (refundApplicationFee)
            {
                // Es gibt nichts anteilig zurueckzugeben, weil je Verkauf nie etwas einbehalten wurde
                // (siehe oben). Die Basis hat den Anteil trotzdem ausgerechnet und schreibt ihn auf die
                // Erstattungszeile - richtig wird das Bild erst mit der Abrechnung der Plattform-Konditionen.
                LogEnvironment.LogEvent(
                    $"Refund of {amountMinor} on sale {sale.TenantSaleId} asks for the commission to be returned, but with Payrexx none was withheld per transaction. The local books show the share as returned — reconcile it against the platform tariff.",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
            }

            var connection = await ConnectionForAsync(sale, cancellationToken);
            if (connection.IsOwnAccess)
            {
                return await RefundOnOwnInstanceAsync(connection, sale, amountMinor, cancellationToken);
            }

            try
            {
                // ACHTUNG, drei Dinge auf einmal, und alle drei anders als bei der Haendler-API:
                // 1. Das laeuft ueber die SERVICE-API (v2.3/service), nicht ueber die des Haendlers.
                // 2. Adressiert wird mit der UUID der Transaktion, NICHT mit ihrer Zahl. Beides steht in
                //    der Transaktion, und die Zahl faellt hier nicht als Fehler auf - sie findet nur nichts.
                // 3. Der Betrag ist eine ZEICHENKETTE, in Rappen.
                var refund = await serviceApi.PostAsync<PayrexxServiceTransaction>("v2.3",
                    $"transaction/{sale.ProviderChargeId}/refund",
                    new Dictionary<string, object?> { ["amount"] = amountMinor.ToString() },
                    cancellationToken) ?? throw new PayrexxApiException("Payrexx accepted the refund but returned nothing.");

                return new ProviderRefund(refund.Uuid ?? refund.Id.ToString(), refund.Status);
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Refund of {amountMinor} on sale {sale.TenantSaleId} (transaction {sale.ProviderChargeId}) was refused by Payrexx: {ex.OutlineException()}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <summary>
        /// Erstattet auf der eigenen Instanz des Mandanten - ueber die Haendler-API.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Die Service-API der Plattform reicht nicht in eine fremde Instanz. Die Haendler-API erstattet mit
        /// <c>POST Transaction/{id}/refund/</c>; adressiert wird dort mit der NUMMER der Transaktion, nicht mit
        /// ihrer UUID. Darum legt der Webhook-Weg eines eigenen Zugangs die Nummer als
        /// <see cref="TenantSale.ProviderChargeId"/> ab.
        /// </para>
        /// <para>
        /// <b>Noch nicht gegen ein echtes Konto gelaufen.</b> Pfad und Feld (<c>amount</c> in Rappen) stammen aus
        /// der Referenz der Haendler-API.
        /// </para>
        /// </remarks>
        private async Task<ProviderRefund> RefundOnOwnInstanceAsync(PayrexxConnection connection, TenantSale sale,
            long amountMinor, CancellationToken cancellationToken)
        {
            if (!long.TryParse(sale.ProviderChargeId, out var transactionId))
            {
                LogEnvironment.LogEvent(
                    $"Sale {sale.TenantSaleId} ran on the own Payrexx instance {connection.Instance} but carries '{sale.ProviderChargeId}' instead of a transaction number; the merchant API cannot refund it. Refund it in the Payrexx portal.",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.MissingCharge,
                    $"Sale {sale.TenantSaleId} has no Payrexx transaction number on record; refund it in the Payrexx portal.");
            }

            try
            {
                var refund = await api.PostAsync<PayrexxTransaction>(connection, $"Transaction/{transactionId}/refund/",
                    new Dictionary<string, object?> { ["amount"] = amountMinor }, cancellationToken)
                             ?? throw new PayrexxApiException("Payrexx accepted the refund but returned nothing.");
                return new ProviderRefund(refund.Id.ToString(), refund.Status);
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Refund of {amountMinor} on sale {sale.TenantSaleId} (transaction {transactionId} on the own instance {connection.Instance}) was refused by Payrexx: {ex.OutlineException()}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }
    }
}
