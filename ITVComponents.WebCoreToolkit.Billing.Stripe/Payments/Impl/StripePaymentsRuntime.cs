using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>Die gemeinsame Klammer, um das erweitert, was nur Stripe betrifft.</summary>
    internal sealed class StripePaymentsRuntime : PaymentsRuntime
    {
        public StripePaymentsRuntime(IGlobalSettings<TenantPaymentsOptions> settings, IPaymentFeatureGate? featureGate)
            : base(settings, featureGate)
        {
        }

        /// <inheritdoc />
        protected override void EnsureProviderReady()
        {
            if (!string.Equals(Options.Stripe.ChargeType, "direct", StringComparison.OrdinalIgnoreCase))
            {
                // Destination charges make the PLATFORM merchant of record. That is a tax decision, so it must
                // not happen because a configuration string was changed and the code quietly went along.
                throw new TenantPaymentException(PaymentErrorCodes.UnsupportedChargeType,
                    $"Charge type '{Options.Stripe.ChargeType}' is configured but only 'direct' is implemented.");
            }
        }

        /// <summary>Request options addressing the connected account (the provider turns this into its account header).</summary>
        public static RequestOptions ForAccount(string providerAccountId, string? idempotencyKey = null)
            => new() { StripeAccount = providerAccountId, IdempotencyKey = idempotencyKey };
    }
}
