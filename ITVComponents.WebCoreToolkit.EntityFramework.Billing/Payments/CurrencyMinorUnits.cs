using System;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Conversion between a human amount (49.90) and the provider's minor units (4990). ONE place on purpose:
    /// <c>amount * 100</c> is wrong for JPY, and a currency table scattered over three call sites is a currency
    /// table that disagrees with itself.
    /// </summary>
    public static class CurrencyMinorUnits
    {
        /// <summary>Currencies without a fractional part — the amount IS the minor unit.</summary>
        private static readonly HashSet<string> ZeroDecimal = new(StringComparer.OrdinalIgnoreCase)
        {
            "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
        };

        /// <summary>
        /// Currencies with three decimals. The provider additionally requires the last digit to be 0 (it charges
        /// in units of ten), which <see cref="ToMinor"/> enforces.
        /// </summary>
        private static readonly HashSet<string> ThreeDecimal = new(StringComparer.OrdinalIgnoreCase)
        {
            "BHD", "JOD", "KWD", "OMR", "TND"
        };

        /// <summary>Number of minor units per major unit for <paramref name="currency"/> (default 100).</summary>
        public static int Factor(string? currency)
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                return 100;
            }

            var code = currency.Trim();
            if (ZeroDecimal.Contains(code))
            {
                return 1;
            }

            return ThreeDecimal.Contains(code) ? 1000 : 100;
        }

        /// <summary>
        /// Converts a major-unit amount to minor units, rounding commercially. For three-decimal currencies the
        /// result is rounded to the nearest ten, because the provider rejects anything else.
        /// </summary>
        public static long ToMinor(decimal amount, string? currency)
        {
            var factor = Factor(currency);
            var minor = (long)Math.Round(amount * factor, MidpointRounding.AwayFromZero);
            if (factor == 1000)
            {
                minor = (long)Math.Round(minor / 10m, MidpointRounding.AwayFromZero) * 10;
            }

            return minor;
        }

        /// <summary>Converts minor units back to a major-unit amount for display.</summary>
        public static decimal ToMajor(long minor, string? currency) => minor / (decimal)Factor(currency);
    }
}
