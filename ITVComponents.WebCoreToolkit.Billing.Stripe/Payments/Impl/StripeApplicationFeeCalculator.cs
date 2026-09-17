using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// The commission calculator as a service, reading the configured rates from the global setting. Injected
    /// wherever a fee has to be SHOWN before a sale exists (a preview on the sales page); the booked fee comes
    /// from the same code path, so the two cannot drift.
    /// <para>
    /// Lives HERE and not next to <see cref="IApplicationFeeCalculator"/>: the contract and the arithmetic
    /// (<see cref="ApplicationFeeMath"/>) are provider-neutral, but the RATES come out of this provider's
    /// setting. A second provider brings its own setting and registers its own calculator — it must not be
    /// forced to keep a Stripe setting around just to state its commission.
    /// </para>
    /// </summary>
    public class StripeApplicationFeeCalculator : IApplicationFeeCalculator
    {
        private readonly IGlobalSettings<StripePaymentsOptions> settings;

        public StripeApplicationFeeCalculator(IGlobalSettings<StripePaymentsOptions> settings)
        {
            this.settings = settings;
        }

        /// <inheritdoc />
        public long Calculate(long amountMinor, string? currency)
            => ApplicationFeeMath.Calculate(settings.Value.ApplicationFee, amountMinor, currency);
    }
}
