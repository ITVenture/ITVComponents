using System;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Pure arithmetic of the volume-based waiver: how much of a subscription invoice position is waived, given
    /// the net turnover of the period that closed. Separate from the invoice plumbing so the price model can be
    /// reasoned about (and tested) without a provider account.
    /// </summary>
    public static class VolumeWaiver
    {
        /// <summary>Picks the per-currency block if one matches, otherwise the base block.</summary>
        public static VolumeWaiverOptions Resolve(VolumeWaiverOptions? options, string? currency)
        {
            if (options == null)
            {
                return new VolumeWaiverOptions();
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
        /// True when <paramref name="options"/> asks for the sliding mode AND its band is usable. A ramp start
        /// outside [0, threshold) cannot describe a band, so the caller falls back to <see cref="WaiverMode.Hard"/>.
        /// </summary>
        public static bool HasUsableBand(VolumeWaiverOptions options)
            => options.Mode == WaiverMode.Sliding
               && options.WaiverRampStartMinor >= 0
               && options.WaiverRampStartMinor < options.ThresholdMinor;

        /// <summary>
        /// Whether a sliding band is wide enough to actually remove the earnings dent. Inside the band the
        /// platform's revenue is non-decreasing exactly when
        /// <c>(threshold - rampStart) * rate &gt;= baseFee</c> — a narrower band merely stretches the dent
        /// instead of removing it, which is worth a warning: the configuration would then not do what the mode
        /// promises.
        /// </summary>
        /// <param name="thresholdMinor">Turnover at which the fee is fully waived.</param>
        /// <param name="rampStartMinor">Turnover at which the waiver starts to grow.</param>
        /// <param name="percentBasisPoints">Commission rate in basis points (100 = 1 %).</param>
        /// <param name="baseFeeMinor">The waivable invoice position (the base fee) in minor units.</param>
        public static bool IsBandWideEnough(long thresholdMinor, long rampStartMinor, int percentBasisPoints, long baseFeeMinor)
        {
            if (baseFeeMinor <= 0)
            {
                return true;
            }

            if (percentBasisPoints <= 0 || thresholdMinor <= rampStartMinor)
            {
                return false;
            }

            var commissionOverBand = (thresholdMinor - rampStartMinor) * (decimal)percentBasisPoints / 10000m;
            return commissionOverBand >= baseFeeMinor;
        }

        /// <summary>
        /// How much of <paramref name="positionAmountMinor"/> is waived at <paramref name="netVolumeMinor"/>.
        /// Never more than the position itself: crediting beyond it would turn the invoice into a balance the
        /// tenant carries forward.
        /// </summary>
        /// <param name="options">The resolved (already per-currency picked) waiver block.</param>
        /// <param name="netVolumeMinor">Net turnover of the CLOSED period. Negative values count as zero.</param>
        /// <param name="positionAmountMinor">Actual amount of the invoice position, not the list price.</param>
        public static long Waived(VolumeWaiverOptions options, long netVolumeMinor, long positionAmountMinor)
        {
            if (!options.Enabled || positionAmountMinor <= 0 || options.ThresholdMinor <= 0)
            {
                return 0;
            }

            var volume = Math.Max(0, netVolumeMinor);
            if (volume >= options.ThresholdMinor)
            {
                return positionAmountMinor;
            }

            if (!HasUsableBand(options))
            {
                // Hard mode (or an unusable band, which the caller has already logged): below the threshold
                // nothing is waived.
                return 0;
            }

            if (volume <= options.WaiverRampStartMinor)
            {
                return 0;
            }

            var span = options.ThresholdMinor - options.WaiverRampStartMinor;
            var share = decimal.Round(positionAmountMinor * (decimal)(volume - options.WaiverRampStartMinor) / span, MidpointRounding.AwayFromZero);
            return Math.Clamp((long)share, 0, positionAmountMinor);
        }
    }
}
