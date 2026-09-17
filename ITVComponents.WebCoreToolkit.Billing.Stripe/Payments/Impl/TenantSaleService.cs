using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Records end-customer sales on a tenant's connected account (direct charges) and reverses them.
    /// <para>
    /// The payment page is hosted by the provider: a Checkout session with exactly ONE synthetic line item made
    /// from total and caption. That keeps card data out of this application entirely (SAQ-A) and is why a sale
    /// needs no line items of its own.
    /// </para>
    /// <para>
    /// The bookkeeping — idempotency, frozen commission, status, refund arithmetic — lives in
    /// <see cref="TenantSaleServiceBase{TContext}"/>. What is left here is what only Stripe does.
    /// </para>
    /// </summary>
    public class TenantSaleService<TContext> : TenantSaleServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IStripeClient client;

        public TenantSaleService(IDbContextFactory<TContext> dbFactory, IStripeClient client,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
            IEnumerable<ITenantSaleObserver> observers)
            : base(dbFactory, new StripePaymentsRuntime(settings, featureGates.FirstOrDefault()),
                   new TenantSaleNotifier(observers))
        {
            this.client = client;
        }

        /// <inheritdoc />
        protected override async Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
            TenantPaymentAccount account, int attempt, CancellationToken cancellationToken)
        {
            var options = Runtime.Options;
            var expiryMinutes = Math.Clamp(options.CheckoutExpiryMinutes, 30, 1440);
            var metadata = new Dictionary<string, string>
            {
                ["tenantId"] = sale.TenantId.ToString(),
                ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                ["externalReference"] = sale.ExternalReference
            };

            var create = new SessionCreateOptions
            {
                Mode = "payment",
                // Deliberately NO Customer and NO SetupFutureUsage: the purchase is ad hoc. Handing the provider
                // a customer would turn an anonymous buyer into a stored profile with saved payment details —
                // see section 2.5 of the Connect plan. The e-mail below is for the receipt only.
                // Still to be verified against a live account: customer_creation behaves differently on a
                // connected account than on the platform account, so it is not sent at all here.
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = sale.Currency.ToLowerInvariant(),
                            UnitAmount = sale.AmountMinor,
                            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = sale.Description }
                        }
                    }
                ],
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    ApplicationFeeAmount = sale.ApplicationFeeMinor > 0 ? sale.ApplicationFeeMinor : null,
                    StatementDescriptorSuffix = Trim(options.StatementDescriptorSuffix, 22),
                    Metadata = metadata
                },
                // The way back from the webhook to this row without depending on the provider ids.
                ClientReferenceId = sale.TenantSaleId.ToString(),
                CustomerEmail = string.IsNullOrWhiteSpace(sale.CustomerEmail) ? null : sale.CustomerEmail,
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Metadata = metadata
            };

            try
            {
                var session = await new SessionService(client).CreateAsync(create,
                    // Keyed on the sale and the attempt: a repeated click returns the same session, a deliberate
                    // second attempt after an expiry gets a fresh one.
                    StripePaymentsRuntime.ForAccount(account.ProviderAccountId,
                        $"sale:{sale.TenantId}:{sale.ExternalReference}:{attempt}"),
                    cancellationToken);
                return new ProviderCheckout(session.Id, session.Url);
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not create a payment page for sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}', account {account.ProviderAccountId}): {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <inheritdoc />
        protected override async Task<ProviderRefund> CreateRefundAsync(TenantSale sale, long amountMinor,
            string? reason, bool refundApplicationFee, string accountId, long alreadyRefundedMinor,
            CancellationToken cancellationToken)
        {
            try
            {
                var refund = await new RefundService(client).CreateAsync(new RefundCreateOptions
                {
                    Charge = sale.ProviderChargeId,
                    Amount = amountMinor,
                    // The provider only accepts its own three reasons; anything else would be rejected, so the
                    // tenant's own wording travels as metadata and is kept on the local row.
                    Reason = MapReason(reason),
                    RefundApplicationFee = refundApplicationFee,
                    Metadata = BuildRefundMetadata(sale, reason)
                }, StripePaymentsRuntime.ForAccount(accountId,
                    $"refund:{sale.TenantSaleId}:{alreadyRefundedMinor}:{amountMinor}"), cancellationToken);
                return new ProviderRefund(refund.Id, refund.Status);
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Refund of {amountMinor} on sale {sale.TenantSaleId} (charge {sale.ProviderChargeId}, account {accountId}) was refused by the provider: {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <summary>The provider accepts a fixed set of reasons; everything else has to travel as metadata.</summary>
        private static string? MapReason(string? reason) => reason?.Trim().ToLowerInvariant() switch
        {
            "duplicate" => "duplicate",
            "fraudulent" => "fraudulent",
            "requested_by_customer" => "requested_by_customer",
            _ => null
        };

        private static Dictionary<string, string> BuildRefundMetadata(TenantSale sale, string? reason)
        {
            var metadata = new Dictionary<string, string>
            {
                ["tenantId"] = sale.TenantId.ToString(),
                ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                ["externalReference"] = sale.ExternalReference
            };

            if (!string.IsNullOrWhiteSpace(reason))
            {
                metadata["reason"] = Trim(reason, 500)!;
            }

            return metadata;
        }

        private static string? Trim(string? value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
    }
}
