using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Wallee.Client;
using Wallee.Model;
using Wallee.Service;
// Alias, weil ITVComponents.WebCoreToolkit.Configuration ein NAMENSRAUM ist und den Typ verdeckt.
// Der Fehler lautet dann "Configuration ist Namespace, wird aber wie Typ verwendet" und zeigt auf die
// Signatur statt auf das using.
using WalleeConfiguration = Wallee.Client.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Impl
{
    /// <summary>
    /// Verkäufe über wallee: eine <b>Transaktion</b> mit einer Position, deren Zahlungsseite abgeholt wird.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Die wichtigste Einschränkung steht gleich hier:</b> wallee kennt kein Marktplatz-Modell. Es gibt
    /// kein Gegenstück zur Provision, die bei einem Verkauf automatisch an die Plattform geht. Die
    /// Provision wird lokal weiterhin berechnet und auf der Verkaufszeile eingefroren — <b>eingezogen wird
    /// sie nicht</b>. Wer wallee für Achse B einsetzt, muss sie dem Mandanten getrennt in Rechnung stellen
    /// (naheliegend: über Achse A). Der Dienst sagt das bei jedem Verkauf mit Provision ins Protokoll,
    /// damit es niemand aus Versehen annimmt.
    /// </para>
    /// <para>
    /// <b>Beträge sind hier Dezimalzahlen in der HAUPTeinheit</b> (49.90), nicht in der kleinsten Einheit.
    /// Genau andersherum als bei Stripe und Payrexx. Die Umrechnung läuft über
    /// <see cref="CurrencyMinorUnits.ToMajor"/> — von Hand durch 100 zu teilen ist für JPY falsch.
    /// </para>
    /// <para>
    /// <b>Das SDK ist durchgehend synchron</b> — es gibt keine einzige <c>*Async</c>-Methode. Die Aufrufe
    /// laufen darum über <see cref="Task.Run{TResult}(Func{TResult})"/>: das blockiert einen
    /// Threadpool-Thread, aber nicht den Anfrage-Thread. Direkt aufrufen und das Ergebnis in
    /// <c>Task.FromResult</c> zu verpacken sähe harmloser aus und würde unter Last den Threadpool
    /// aushungern.
    /// </para>
    /// <para>
    /// <b>Der Raum (Space) ist kein Konto.</b> wallee hat nichts, was einem Connected Account entspricht;
    /// <c>ProviderAccountId</c> trägt hier die Raum-Kennung. Ein eigener Raum je Mandant ist möglich, aber
    /// Einrichtungsarbeit im Portal von wallee — es gibt keine Selbstregistrierung wie bei Stripe oder
    /// Payrexx. Ist auf dem Konto nichts hinterlegt, gilt der Raum aus den Einstellungen.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen einen echten Raum gelaufen.</b> Signaturen und Feldnamen stammen aus dem SDK
    /// selbst (Reflection über Wallee 10.4.0), nicht aus einer Vermutung — der Ablauf ist trotzdem
    /// ungeprüft.
    /// </para>
    /// </remarks>
    public class WalleeSaleService<TContext> : TenantSaleServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IGlobalSettings<WalleeOptions> walleeSettings;

        /// <summary>Initializes a new instance of the <see cref="WalleeSaleService{TContext}"/> class.</summary>
        public WalleeSaleService(IDbContextFactory<TContext> dbFactory,
            IGlobalSettings<WalleeOptions> walleeSettings,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
            IEnumerable<ITenantSaleObserver> observers)
            : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()),
                   new TenantSaleNotifier(observers))
        {
            this.walleeSettings = walleeSettings;
        }

        private WalleeOptions Wallee => walleeSettings.Value;

        /// <inheritdoc />
        protected override async Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
            TenantPaymentAccount account, int attempt, CancellationToken cancellationToken)
        {
            var wallee = Wallee;
            var space = ResolveSpace(account, wallee);

            if (sale.ApplicationFeeMinor > 0)
            {
                // Siehe Klassenkommentar. Bewusst bei JEDEM Verkauf und nicht nur einmal beim Start: die
                // Provision steht auf der Zeile, und wer die Abrechnung liest, soll im Protokoll finden,
                // warum das Geld nicht angekommen ist.
                LogEnvironment.LogEvent(
                    $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) carries a commission of {sale.ApplicationFeeMinor} {sale.Currency}, but wallee has no marketplace split — the amount is booked locally and must be invoiced to the tenant separately.",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
            }

            var create = new TransactionCreate
            {
                Currency = sale.Currency.ToUpperInvariant(),
                // Der Weg zurueck zu dieser Zeile: wallee gibt die Referenz in Benachrichtigungen und in der
                // Suche unveraendert wieder heraus.
                MerchantReference = sale.ExternalReference,
                SuccessUrl = request.SuccessUrl,
                FailedUrl = request.CancelUrl,
                // Ohne dies bleibt die Transaktion im Zustand PENDING stehen und wartet auf eine
                // Bestaetigung, die in diesem Ablauf niemand schickt.
                AutoConfirmationEnabled = true,
                LineItems =
                [
                    new LineItemCreate
                    {
                        Name = sale.Description,
                        // Eindeutig innerhalb der Transaktion - nicht global. Die Verkaufs-Id reicht.
                        UniqueId = sale.TenantSaleId.ToString(),
                        Quantity = 1,
                        AmountIncludingTax = CurrencyMinorUnits.ToMajor(sale.AmountMinor, sale.Currency),
                        // PFLICHT: die Vorbelegung des SDK ist 0, und 0 ist kein gueltiger Wert dieses
                        // Aufzaehlungstyps (gueltig sind 1..5). Ohne diese Zeile schickt das SDK etwas,
                        // das wallee nicht annimmt.
                        Type = LineItemType.PRODUCT
                    }
                ],
                MetaData = new Dictionary<string, string>
                {
                    ["tenantId"] = sale.TenantId.ToString(),
                    ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                    ["externalReference"] = sale.ExternalReference
                }
            };

            if (!string.IsNullOrWhiteSpace(sale.CustomerEmail))
            {
                // Nur setzen, wenn vorhanden. Das SDK ist nicht nullable-annotiert; eine null-Zuweisung
                // waere eine Warnung und - schlimmer - bei einem generierten Client nicht dasselbe wie
                // "Feld weglassen". Nur fuer den Beleg; ein Kundenprofil entsteht dadurch nicht.
                create.CustomerEmailAddress = sale.CustomerEmail;
            }

            if (wallee.PaymentMethodConfigurations is { Length: > 0 })
            {
                // Derselbe Gebuehren-Hebel wie anderswo: steht TWINT hier drin, zahlt der Endkunde eher damit.
                create.AllowedPaymentMethodConfigurations = [.. wallee.PaymentMethodConfigurations];
            }

            try
            {
                var service = new TransactionsService(WalleeRuntime.Configure(wallee));
                var transaction = await Task.Run(() => service.PostPaymentTransactions(space, create), cancellationToken)
                    .ConfigureAwait(false);
                // ACHTUNG Parameter-Reihenfolge: erst die Transaktion, DANN der Raum. Vertauscht laeuft der
                // Aufruf durch und liefert eine Adresse, die niemanden zu dieser Zahlung fuehrt.
                var url = await Task.Run(() => service.GetPaymentTransactionsIdPaymentPageUrl(transaction.Id, space), cancellationToken)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new WalleeSaleException($"wallee created transaction {transaction.Id} but returned no payment page URL.");
                }

                return new ProviderCheckout(transaction.Id.ToString(), url);
            }
            catch (Exception ex) when (ex is ApiException or WalleeSaleException)
            {
                LogEnvironment.LogEvent(
                    $"Could not create a payment page for sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}', space {space}, attempt {attempt}): {ex.OutlineException()}",
                    LogSeverity.Error, WalleeRuntime.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <inheritdoc />
        protected override async Task<ProviderRefund> CreateRefundAsync(TenantSale sale, long amountMinor,
            string? reason, bool refundApplicationFee, string accountId, long alreadyRefundedMinor,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sale.ProviderChargeId) || !long.TryParse(sale.ProviderChargeId, out var transactionId))
            {
                throw new TenantPaymentException(PaymentErrorCodes.MissingCharge,
                    $"Sale {sale.TenantSaleId} has no wallee transaction on record; the refund cannot be addressed.");
            }

            if (refundApplicationFee)
            {
                // Es gibt nichts zurueckzugeben, weil nie etwas einbehalten wurde. Die Basis schreibt den
                // Anteil trotzdem auf die Erstattungszeile - beides zusammen ergibt erst dann ein richtiges
                // Bild, wenn die Provision dem Mandanten getrennt verrechnet und dort ebenfalls gutgeschrieben
                // wird.
                LogEnvironment.LogEvent(
                    $"Refund of {amountMinor} on sale {sale.TenantSaleId} asks for the commission to be returned, but with wallee none was ever withheld — make sure the separate commission invoice is credited too.",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
            }

            var wallee = Wallee;
            var space = ResolveSpace(null, wallee, accountId);

            var create = new RefundCreate
            {
                Transaction = transactionId,
                Amount = CurrencyMinorUnits.ToMajor(amountMinor, sale.Currency),
                // PFLICHT wie oben: Vorbelegung 0 ist kein gueltiger Wert (gueltig 1..4).
                Type = RefundType.MERCHANTINITIATEDONLINE,
                // Die Idempotenz dieses Anbieters: derselbe ExternalId liefert dieselbe Erstattung, statt
                // ein zweites Mal auszuzahlen. Der Schluessel enthaelt den bisher erstatteten Stand, damit
                // eine ZWEITE, absichtliche Teilerstattung nicht als Wiederholung der ersten gilt.
                ExternalId = $"refund:{sale.TenantSaleId}:{alreadyRefundedMinor}:{amountMinor}",
                MerchantReference = sale.ExternalReference,
                MetaData = new Dictionary<string, string>
                {
                    ["tenantId"] = sale.TenantId.ToString(),
                    ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                    ["reason"] = reason ?? string.Empty
                }
            };

            try
            {
                var service = new RefundsService(WalleeRuntime.Configure(wallee));
                var refund = await Task.Run(() => service.PostPaymentRefunds(space, create), cancellationToken)
                    .ConfigureAwait(false);
                return new ProviderRefund(refund.Id.ToString(), refund.State?.ToString());
            }
            catch (ApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Refund of {amountMinor} on sale {sale.TenantSaleId} (transaction {transactionId}, space {space}) was refused by wallee: {ex.OutlineException()}",
                    LogSeverity.Error, WalleeRuntime.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <summary>
        /// Der Raum, in dem gearbeitet wird: der des Mandanten, sonst der aus den Einstellungen.
        /// </summary>
        private static long ResolveSpace(TenantPaymentAccount? account, WalleeOptions options, string? accountId = null)
        {
            var raw = accountId ?? account?.ProviderAccountId;
            if (!string.IsNullOrWhiteSpace(raw) && long.TryParse(raw, out var space) && space > 0)
            {
                return space;
            }

            if (options.SpaceId <= 0)
            {
                throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                    "Neither the tenant nor the 'WalleePayments' setting names a wallee space to work in.");
            }

            return options.SpaceId;
        }

    }

    /// <summary>wallee hat geantwortet, aber nicht brauchbar.</summary>
    public class WalleeSaleException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="WalleeSaleException"/> class.</summary>
        public WalleeSaleException(string message) : base(message) { }
    }
}
