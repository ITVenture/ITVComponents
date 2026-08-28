using System;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Works out the platform's commission for a sale. Display and booking MUST go through this one helper —
    /// the moment a page computes the fee itself, the number the tenant sees and the number that is charged can
    /// drift apart, and nobody notices until someone reconciles.
    /// </summary>
    public interface IApplicationFeeCalculator
    {
        /// <summary>Commission in minor units for <paramref name="amountMinor"/> in <paramref name="currency"/>.</summary>
        long Calculate(long amountMinor, string? currency);
    }

    /// <summary>
    /// Pure computation of the commission, independent of where the options come from (so it can be tested
    /// without a container and reused by the waiver evaluation).
    /// </summary>
    public static class ApplicationFeeMath
    {
        /// <summary>
        /// Picks the per-currency block if one matches, otherwise the base block. A per-currency entry replaces
        /// the base COMPLETELY — a partially-inherited rate would be impossible to read off the configuration.
        /// </summary>
        public static ApplicationFeeOptions Resolve(ApplicationFeeOptions? options, string? currency)
        {
            if (options == null)
            {
                return new ApplicationFeeOptions();
            }

            if (!string.IsNullOrWhiteSpace(currency)
                && options.PerCurrency != null
                && options.PerCurrency.TryGetValue(currency.Trim().ToUpperInvariant(), out var perCurrency)
                && perCurrency != null)
            {
                return perCurrency;
            }

            return options;
        }

        /// <summary>
        /// <c>fee = amount * bp / 10000 + fixed</c>, rounded commercially, then clamped to the configured bounds
        /// and hard-clamped to [0, amount] — a commission larger than the sale is not a rate, it is a bug.
        /// </summary>
        public static long Calculate(ApplicationFeeOptions? options, long amountMinor, string? currency)
        {
            if (amountMinor <= 0)
            {
                return 0;
            }

            var effective = Resolve(options, currency);
            var percentage = decimal.Round(amountMinor * (decimal)effective.PercentBasisPoints / 10000m, MidpointRounding.AwayFromZero);
            var fee = (long)percentage + effective.FixedMinor;

            if (effective.MinMinor > 0 && fee < effective.MinMinor)
            {
                fee = effective.MinMinor;
            }

            if (effective.MaxMinor > 0 && fee > effective.MaxMinor)
            {
                fee = effective.MaxMinor;
            }

            return Math.Clamp(fee, 0, amountMinor);
        }

        /// <summary>
        /// Share of the commission that goes back with a partial refund. Mirrors the provider's documented rule:
        /// a full refund returns the whole fee, a partial one returns a proportional share.
        /// </summary>
        public static long ProportionalRefund(long applicationFeeMinor, long refundedMinor, long saleAmountMinor)
        {
            if (applicationFeeMinor <= 0 || refundedMinor <= 0 || saleAmountMinor <= 0)
            {
                return 0;
            }

            if (refundedMinor >= saleAmountMinor)
            {
                return applicationFeeMinor;
            }

            var share = decimal.Round(applicationFeeMinor * (decimal)refundedMinor / saleAmountMinor, MidpointRounding.AwayFromZero);
            return Math.Clamp((long)share, 0, applicationFeeMinor);
        }
    }

    /// <summary>
    /// The commission calculator as a service, reading the configured rates from the global setting. Injected
    /// wherever a fee has to be SHOWN before a sale exists (a preview on the sales page); the booked fee comes
    /// from the same code path, so the two cannot drift.
    /// </summary>
    public class ApplicationFeeCalculator : IApplicationFeeCalculator
    {
        private readonly IGlobalSettings<StripePaymentsOptions> settings;

        public ApplicationFeeCalculator(IGlobalSettings<StripePaymentsOptions> settings)
        {
            this.settings = settings;
        }

        /// <inheritdoc />
        public long Calculate(long amountMinor, string? currency)
            => ApplicationFeeMath.Calculate(settings.Value.ApplicationFee, amountMinor, currency);
    }
}
