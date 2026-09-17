using System;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// The preconditions every payments operation shares, in one place. They are checked in the SERVICE and not
    /// only in the view: a sale can originate from an anonymous shop request or a background run, where there is
    /// no security scope to ask.
    /// </summary>
    public class PaymentsRuntime
    {
        private readonly IGlobalSettings<TenantPaymentsOptions> settings;
        private readonly IPaymentFeatureGate? featureGate;

        public PaymentsRuntime(IGlobalSettings<TenantPaymentsOptions> settings, IPaymentFeatureGate? featureGate)
        {
            this.settings = settings;
            this.featureGate = featureGate;
        }

        public TenantPaymentsOptions Options => settings.Value;

        /// <summary>
        /// Der Haken fuer das, was nur EIN Anbieter pruefen kann - etwa eine Betriebsart, die er zwar
        /// konfigurieren laesst, aber nicht umgesetzt hat. Die Vorbelegung prueft nichts.
        /// </summary>
        protected virtual void EnsureProviderReady()
        {
        }

        /// <summary>Master switch. Refuses regardless of what the tenant is entitled to.</summary>
        public void EnsureEnabled()
        {
            if (!Options.Enabled)
            {
                throw new TenantPaymentException(PaymentErrorCodes.Disabled, "The payments module is switched off for this deployment.");
            }

            EnsureProviderReady();
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
    }
}
