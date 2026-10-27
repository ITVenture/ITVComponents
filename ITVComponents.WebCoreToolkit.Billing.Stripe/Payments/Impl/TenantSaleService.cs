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
        protected override string ProviderKey => "stripe";

        /// <inheritdoc />
        protected override async Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
            TenantPaymentAccount? account, int attempt, CancellationToken cancellationToken)
        {
            // Ohne SupportsOwnAccess kommt hier immer das Plattform-Konto an; die Basis lehnt den Rest vorher ab.
            ArgumentNullException.ThrowIfNull(account);
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
                    // further attempt after an expiry or a cancel gets a fresh one. From the second attempt on,
                    // the PREVIOUS session is part of the key — "attempt" is 1 for every one of them, and the
                    // third attempt would otherwise get the dead page of the second back for 24 hours.
                    StripePaymentsRuntime.ForAccount(account.ProviderAccountId,
                        attempt == 0 || string.IsNullOrEmpty(sale.ProviderSessionId)
                            ? $"sale:{sale.TenantId}:{sale.ExternalReference}:{attempt}"
                            : $"sale:{sale.TenantId}:{sale.ExternalReference}:after:{sale.ProviderSessionId}"),
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
                // Terminal-Verkaeufe von vor dem Feld ProviderChargeId tragen hier die Zahlungsabsicht (pi_...).
                // Stripe erstattet auch ueber sie - als Belastung geschickt, liefe die Erstattung ins Leere.
                var byIntent = sale.ProviderChargeId?.StartsWith("pi_", StringComparison.Ordinal) == true;
                var refund = await new RefundService(client).CreateAsync(new RefundCreateOptions
                {
                    Charge = byIntent ? null : sale.ProviderChargeId,
                    PaymentIntent = byIntent ? sale.ProviderChargeId : null,
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

        /// <inheritdoc />
        /// <remarks>
        /// Stripe only expires an OPEN session. When that is refused, the session is read back and its state
        /// decides — a refusal alone says nothing about whether the customer paid.
        /// </remarks>
        protected override async Task<ProviderCancel> CancelCheckoutAsync(TenantSale sale, CancellationToken cancellationToken)
        {
            var accountId = sale.ProviderAccountId
                            ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                                $"Sale {sale.TenantSaleId} has no connected account on record; its checkout session cannot be addressed.");
            var sessions = new SessionService(client);

            try
            {
                await sessions.ExpireAsync(sale.ProviderSessionId, null, StripePaymentsRuntime.ForAccount(accountId),
                    cancellationToken);
                return new ProviderCancel(ProviderCancelOutcome.Canceled);
            }
            catch (StripeException ex)
            {
                // Expected when the session is no longer open (paid, or already expired). Which of the two is
                // read right below; the refusal itself is kept for the trail.
                LogEnvironment.LogEvent(
                    $"Stripe refused to expire checkout session {sale.ProviderSessionId} of sale {sale.TenantSaleId} (account {accountId}); reading its state: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
            }

            Session current;
            try
            {
                current = await sessions.GetAsync(sale.ProviderSessionId, null, StripePaymentsRuntime.ForAccount(accountId),
                    cancellationToken);
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read checkout session {sale.ProviderSessionId} of sale {sale.TenantSaleId} (account {accountId}) after the expiry was refused; the sale stays pending: {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }

            if (string.Equals(current.Status, "expired", StringComparison.OrdinalIgnoreCase))
            {
                return new ProviderCancel(ProviderCancelOutcome.Canceled);
            }

            if (string.Equals(current.Status, "complete", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(current.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(current.PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase)))
            {
                sale.ProviderPaymentIntentId ??= current.PaymentIntentId;
                var email = Runtime.Options.CaptureCustomerEmail ? Trim(current.CustomerDetails?.Email, 256) : null;
                return new ProviderCancel(ProviderCancelOutcome.Paid,
                    await ResolveChargeAsync(sale, current.PaymentIntentId, accountId, cancellationToken), email);
            }

            // "complete" but unpaid is a delayed payment method still settling; "open" means the expiry was
            // refused for some other reason. Either way the page cannot be declared dead.
            return new ProviderCancel(ProviderCancelOutcome.InProgress,
                Detail: $"checkout session is '{current.Status}' with payment status '{current.PaymentStatus}'");
        }

        /// <summary>Charge behind a payment intent — the identifier a refund is issued against.</summary>
        private async Task<string?> ResolveChargeAsync(TenantSale sale, string? paymentIntentId, string accountId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(paymentIntentId))
            {
                return null;
            }

            try
            {
                var intent = await new PaymentIntentService(client).GetAsync(paymentIntentId, null,
                    StripePaymentsRuntime.ForAccount(accountId), cancellationToken);
                return intent.LatestChargeId;
            }
            catch (StripeException ex)
            {
                // Not fatal: the webhook that follows records the charge on its repeat delivery.
                LogEnvironment.LogEvent(
                    $"Could not read the charge of payment intent {paymentIntentId} (sale {sale.TenantSaleId}, account {accountId}); the sale is booked as paid without a charge until the webhook supplies it: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
                return null;
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
