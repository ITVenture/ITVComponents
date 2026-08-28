using System;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// The preconditions every payments operation shares, in one place. They are checked in the SERVICE and not
    /// only in the view: a sale can originate from an anonymous shop request or a background run, where there is
    /// no security scope to ask.
    /// </summary>
    internal sealed class PaymentsRuntime
    {
        private readonly IGlobalSettings<StripePaymentsOptions> settings;
        private readonly IPaymentFeatureGate? featureGate;

        public PaymentsRuntime(IGlobalSettings<StripePaymentsOptions> settings, IPaymentFeatureGate? featureGate)
        {
            this.settings = settings;
            this.featureGate = featureGate;
        }

        public StripePaymentsOptions Options => settings.Value;

        /// <summary>Master switch. Refuses regardless of what the tenant is entitled to.</summary>
        public void EnsureEnabled()
        {
            if (!Options.Enabled)
            {
                throw new TenantPaymentException(PaymentErrorCodes.Disabled, "The payments module is switched off for this deployment.");
            }

            if (!string.Equals(Options.ChargeType, "direct", StringComparison.OrdinalIgnoreCase))
            {
                // Destination charges make the PLATFORM merchant of record. That is a tax decision, so it must
                // not happen because a configuration string was changed and the code quietly went along.
                throw new TenantPaymentException(PaymentErrorCodes.UnsupportedChargeType,
                    $"Charge type '{Options.ChargeType}' is configured but only 'direct' is implemented.");
            }
        }

        /// <summary>
        /// Feature check by tenant id against the database. With no gate registered the answer is NO — the
        /// fail-closed direction, because the alternative is charging a commission nobody was entitled to.
        /// </summary>
        public async Task EnsureFeatureAsync(int tenantId, CancellationToken cancellationToken)
        {
            if (featureGate == null || !await featureGate.IsEnabledForTenantAsync(tenantId, cancellationToken))
            {
                throw new TenantPaymentException(PaymentErrorCodes.FeatureMissing,
                    $"Tenant {tenantId} does not hold the payments feature.");
            }
        }

        /// <summary>
        /// Whether this mirrored account may take money right now. Shared between the status read model and the
        /// sale guard on purpose: a display that is laxer than the guard strands the tenant at the checkout, a
        /// display that is stricter hides a shop that would work.
        /// </summary>
        public bool CanSell(TenantPaymentAccount account)
            => !account.Disconnected
               && account.ChargesEnabled
               && (!Options.RequirePayoutsEnabled || account.PayoutsEnabled);

        /// <summary>
        /// Throws the SPECIFIC reason this account cannot sell. A shared "not possible" would be worthless in
        /// support — the four causes have four different remedies.
        /// </summary>
        public void EnsureCanSell(TenantPaymentAccount? account)
        {
            if (account == null)
            {
                throw new TenantPaymentException(PaymentErrorCodes.NoAccount, "The tenant has no payout account yet.");
            }

            if (account.Disconnected)
            {
                throw new TenantPaymentException(PaymentErrorCodes.Disconnected,
                    $"The connected account {account.ProviderAccountId} is no longer linked to this platform.");
            }

            if (!account.ChargesEnabled)
            {
                throw new TenantPaymentException(PaymentErrorCodes.ChargesDisabled,
                    $"The connected account {account.ProviderAccountId} may not accept payments"
                    + (string.IsNullOrEmpty(account.DisabledReason) ? "." : $" ({account.DisabledReason})."));
            }

            if (Options.RequirePayoutsEnabled && !account.PayoutsEnabled)
            {
                throw new TenantPaymentException(PaymentErrorCodes.PayoutsDisabled,
                    $"The connected account {account.ProviderAccountId} has no payout route yet and the configuration requires one before selling.");
            }
        }

        /// <summary>Request options addressing the connected account (the provider turns this into its account header).</summary>
        public static RequestOptions ForAccount(string providerAccountId, string? idempotencyKey = null)
            => new() { StripeAccount = providerAccountId, IdempotencyKey = idempotencyKey };
    }
}
