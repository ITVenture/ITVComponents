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

        /// <summary>Initializes a new instance of the <see cref="PayrexxSaleService{TContext}"/> class.</summary>
        public PayrexxSaleService(IDbContextFactory<TContext> dbFactory, PayrexxApiClient api,
            PayrexxServiceApiClient serviceApi,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
            IEnumerable<ITenantSaleObserver> observers)
            : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()),
                   new TenantSaleNotifier(observers))
        {
            this.api = api;
            this.serviceApi = serviceApi;
        }

        /// <inheritdoc />
        protected override async Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
            TenantPaymentAccount account, int attempt, CancellationToken cancellationToken)
        {
            var options = Runtime.Options;
            var payrexx = api.Options;

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

            if (payrexx.PaymentMeans is { Length: > 0 })
            {
                // Der Hebel gegen die Gebuehren: steht TWINT hier vorn, zahlt der Endkunde eher damit, und
                // das kostet rund einen Prozentpunkt weniger als eine Karte.
                payload["pm"] = payrexx.PaymentMeans;
            }

            if (!string.IsNullOrWhiteSpace(sale.CustomerEmail))
            {
                // Nur fuer den Beleg. Ein Kundenprofil entsteht dadurch nicht - der Kauf bleibt ad hoc.
                payload["fields"] = new Dictionary<string, object> { ["email"] = new { value = sale.CustomerEmail } };
            }

            try
            {
                var gateway = await api.PostAsync<PayrexxGateway>("Gateway/", payload, cancellationToken)
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
    }
}
