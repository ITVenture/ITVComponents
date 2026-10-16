using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Impl
{
    /// <summary>
    /// Erstattungen der SumUp-Terminalverkäufe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Erstattet wird über die Weiche der Online-Verkäufe, nach dem Anbieter auf der Verkaufszeile - darum steht
    /// SumUp dort, auch wenn es hier (noch) keine Zahlungsseite gibt. Die Weiche weiss das
    /// (<c>SupportsOnlineSales = false</c>) und wählt SumUp nie von sich aus für einen Online-Verkauf.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen die SumUp-Sandbox gelaufen.</b>
    /// </para>
    /// </remarks>
    public class SumUpSaleService<TContext> : TenantSaleServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private readonly SumUpApiClient api;
        private readonly IProviderCredentialStore credentials;

        /// <summary>Initializes a new instance of the <see cref="SumUpSaleService{TContext}"/> class.</summary>
        public SumUpSaleService(IDbContextFactory<TContext> dbFactory, SumUpApiClient api,
            IProviderCredentialStore credentials, IGlobalSettings<TenantPaymentsOptions> settings,
            IEnumerable<IPaymentFeatureGate> featureGates, IEnumerable<ITenantSaleObserver> observers)
            : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()), new TenantSaleNotifier(observers))
        {
            this.api = api;
            this.credentials = credentials;
        }

        /// <inheritdoc />
        protected override string ProviderKey => SumUpAccess.Key;

        /// <inheritdoc />
        protected override bool SupportsOwnAccess => true;

        /// <inheritdoc />
        protected override bool RequiresOwnAccess => true;

        /// <inheritdoc />
        /// <remarks>
        /// SumUp hat auch Online-Checkouts; angebunden sind sie hier nicht. Ausdrücklich ablehnen statt still nichts
        /// tun.
        /// </remarks>
        protected override Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
            TenantPaymentAccount? account, int attempt, CancellationToken cancellationToken)
        {
            LogEnvironment.LogEvent(
                $"Sale '{sale.ExternalReference}' of tenant {sale.TenantId} was routed to SumUp for an online payment page; only SumUp terminals are connected.",
                LogSeverity.Error, SumUpApiClient.LogContext);
            throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                "Online payment pages are not connected for SumUp; only its terminals are.");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Erstattet wird gegen die Kennung der Transaktion (<see cref="TenantSale.ProviderChargeId"/>), den Betrag
        /// in der HAUPTeinheit - anders als beim Kassieren. SumUp antwortet ohne eigene Kennung der Erstattung; die
        /// hier gebildete trägt den Stand danach und ist damit je Teilerstattung eindeutig.
        /// </remarks>
        protected override async Task<ProviderRefund> CreateRefundAsync(TenantSale sale, long amountMinor, string? reason,
            bool refundApplicationFee, string accountId, long alreadyRefundedMinor, CancellationToken cancellationToken)
        {
            if (refundApplicationFee && sale.ApplicationFeeMinor > 0)
            {
                LogEnvironment.LogEvent(
                    $"Refund on sale {sale.TenantSaleId}: SumUp withheld no commission for the platform, so none goes back with it. The {sale.ApplicationFeeMinor} {sale.Currency} were a local booking figure.",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
            }

            var connection = await SumUpAccess.ConnectAsync(await AccessForSaleAsync(sale, cancellationToken), credentials, cancellationToken);
            var transactionId = sale.ProviderChargeId!;
            try
            {
                await api.SendAsync<object>(HttpMethod.Post,
                    $"v1.0/merchants/{Uri.EscapeDataString(connection.MerchantCode)}/payments/{Uri.EscapeDataString(transactionId)}/refunds",
                    new { amount = CurrencyMinorUnits.ToMajor(amountMinor, sale.Currency) },
                    connection.Credential, cancellationToken);
            }
            catch (SumUpApiException ex) when (ex.StatusCode == 409)
            {
                LogEnvironment.LogEvent(
                    $"SumUp refused to refund transaction {transactionId} of sale {sale.TenantSaleId}: it is not refundable. {ex.Message}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotRefundable,
                    "SumUp says this payment cannot be refunded (any more).", ex);
            }
            catch (SumUpApiException ex)
            {
                // Ohne Antwort ist nicht bekannt, ob die Erstattung durchging - darum laut, und KEINE Zeile buchen.
                // Ein zweiter Versuch ist hier riskanter als bei einer Zahlung: SumUp kennt keinen Idempotenz-
                // Schluessel. Vorher im SumUp-Dashboard nachsehen.
                LogEnvironment.LogEvent(
                    $"Refund of {amountMinor} {sale.Currency} on SumUp transaction {transactionId} (sale {sale.TenantSaleId}) {(ex.Answered ? "was refused" : "got no answer - it is NOT known whether it went through; check the SumUp dashboard before retrying")}: {ex.OutlineException()}",
                    LogSeverity.Error, SumUpApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }

            return new ProviderRefund($"{transactionId}:{alreadyRefundedMinor + amountMinor}", "requested");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Eine SumUp-Zeile hat nie eine Zahlungsseite - <see cref="CreateCheckoutAsync"/> stellt keine aus. Es gibt
        /// also nichts, auf dem noch bezahlt werden könnte.
        /// </remarks>
        protected override Task<ProviderCancel> CancelCheckoutAsync(TenantSale sale, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderCancel(ProviderCancelOutcome.Canceled));
    }
}
