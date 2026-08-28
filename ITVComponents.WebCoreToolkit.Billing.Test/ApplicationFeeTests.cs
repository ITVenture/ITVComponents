using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// The commission arithmetic. Worth its own tests because it is the number the tenant reconciles against:
    /// once it is frozen on a sale, nothing recomputes it, so an error here is permanent for that sale.
    /// </summary>
    [TestClass]
    public class ApplicationFeeTests
    {
        [TestMethod]
        public void PercentageAndFixedAddUp()
        {
            var options = new ApplicationFeeOptions { PercentBasisPoints = 250, FixedMinor = 30 };
            // 2.5 % of 100.00 = 2.50, plus 0.30 fixed.
            Assert.AreEqual(280, ApplicationFeeMath.Calculate(options, 10000, "CHF"));
        }

        [TestMethod]
        public void RoundsCommerciallyToWholeMinorUnits()
        {
            var options = new ApplicationFeeOptions { PercentBasisPoints = 100 };
            // 1 % of 12.35 = 0.1235 -> 12 minor units after rounding half away from zero on 12.35.
            Assert.AreEqual(12, ApplicationFeeMath.Calculate(options, 1235, "CHF"));
            // 1 % of 12.50 = 0.125 -> rounds AWAY from zero, not to even.
            Assert.AreEqual(13, ApplicationFeeMath.Calculate(options, 1250, "CHF"));
        }

        [TestMethod]
        public void HonoursMinimumAndMaximum()
        {
            var options = new ApplicationFeeOptions { PercentBasisPoints = 100, MinMinor = 50, MaxMinor = 500 };
            Assert.AreEqual(50, ApplicationFeeMath.Calculate(options, 1000, "CHF"), "below the minimum the floor applies");
            Assert.AreEqual(500, ApplicationFeeMath.Calculate(options, 1000000, "CHF"), "above the maximum the cap applies");
        }

        [TestMethod]
        public void NeverExceedsTheSaleItself()
        {
            // A fixed surcharge larger than the sale would otherwise produce a commission above the amount —
            // that is not a rate, it is a bug, so it is clamped hard.
            var options = new ApplicationFeeOptions { PercentBasisPoints = 100, FixedMinor = 10000, MinMinor = 20000 };
            Assert.AreEqual(500, ApplicationFeeMath.Calculate(options, 500, "CHF"));
        }

        [TestMethod]
        public void ZeroOrNegativeAmountCostsNothing()
        {
            var options = new ApplicationFeeOptions { PercentBasisPoints = 250, FixedMinor = 30 };
            Assert.AreEqual(0, ApplicationFeeMath.Calculate(options, 0, "CHF"));
            Assert.AreEqual(0, ApplicationFeeMath.Calculate(options, -100, "CHF"));
        }

        [TestMethod]
        public void PerCurrencyBlockReplacesTheBaseCompletely()
        {
            var options = new ApplicationFeeOptions
            {
                PercentBasisPoints = 250,
                FixedMinor = 30,
                PerCurrency = new Dictionary<string, ApplicationFeeOptions>
                {
                    ["EUR"] = new() { PercentBasisPoints = 100 }
                }
            };

            Assert.AreEqual(280, ApplicationFeeMath.Calculate(options, 10000, "CHF"), "unlisted currency keeps the base rate");
            // The listed block wins entirely: the base's fixed 30 does NOT survive, because a half-inherited
            // rate could not be read off the configuration.
            Assert.AreEqual(100, ApplicationFeeMath.Calculate(options, 10000, "EUR"));
            Assert.AreEqual(100, ApplicationFeeMath.Calculate(options, 10000, "eur"), "currency matching is case-insensitive");
        }

        [TestMethod]
        public void FullRefundReturnsTheWholeCommission()
        {
            Assert.AreEqual(250, ApplicationFeeMath.ProportionalRefund(250, 10000, 10000));
            Assert.AreEqual(250, ApplicationFeeMath.ProportionalRefund(250, 20000, 10000), "over-refund is still capped at the fee");
        }

        [TestMethod]
        public void PartialRefundReturnsAProportionalShare()
        {
            // Mirrors the provider's documented rule: half the sale back means half the commission back.
            Assert.AreEqual(125, ApplicationFeeMath.ProportionalRefund(250, 5000, 10000));
            Assert.AreEqual(0, ApplicationFeeMath.ProportionalRefund(0, 5000, 10000));
            Assert.AreEqual(0, ApplicationFeeMath.ProportionalRefund(250, 0, 10000));
        }
    }
}
