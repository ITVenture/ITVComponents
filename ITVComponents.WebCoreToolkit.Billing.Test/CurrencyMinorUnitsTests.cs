using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Amount conversion. The reason this is one helper with tests rather than an inline <c>* 100</c> is that
    /// <c>* 100</c> is simply wrong for a third of the world's currencies, and the failure is silent.
    /// </summary>
    [TestClass]
    public class CurrencyMinorUnitsTests
    {
        [TestMethod]
        public void TwoDecimalCurrenciesUseHundredths()
        {
            Assert.AreEqual(4990, CurrencyMinorUnits.ToMinor(49.90m, "CHF"));
            Assert.AreEqual(4990, CurrencyMinorUnits.ToMinor(49.90m, "eur"), "the code is matched case-insensitively");
            Assert.AreEqual(49.90m, CurrencyMinorUnits.ToMajor(4990, "CHF"));
        }

        [TestMethod]
        public void ZeroDecimalCurrenciesAreTheirOwnMinorUnit()
        {
            // The classic mistake: 1000 JPY billed as 100'000.
            Assert.AreEqual(1000, CurrencyMinorUnits.ToMinor(1000m, "JPY"));
            Assert.AreEqual(1000m, CurrencyMinorUnits.ToMajor(1000, "JPY"));
            Assert.AreEqual(1, CurrencyMinorUnits.Factor("KRW"));
        }

        [TestMethod]
        public void ThreeDecimalCurrenciesRoundToTens()
        {
            // The provider charges these in units of ten, so the last digit has to be zero.
            Assert.AreEqual(1000, CurrencyMinorUnits.Factor("KWD"));
            Assert.AreEqual(1240, CurrencyMinorUnits.ToMinor(1.238m, "KWD"));
            Assert.AreEqual(1230, CurrencyMinorUnits.ToMinor(1.234m, "KWD"));
        }

        [TestMethod]
        public void UnknownOrMissingCurrencyFallsBackToHundredths()
        {
            Assert.AreEqual(100, CurrencyMinorUnits.Factor(null));
            Assert.AreEqual(100, CurrencyMinorUnits.Factor("  "));
            Assert.AreEqual(100, CurrencyMinorUnits.Factor("XYZ"));
        }

        [TestMethod]
        public void RoundsCommerciallyRatherThanToEven()
        {
            Assert.AreEqual(5, CurrencyMinorUnits.ToMinor(0.045m, "CHF"));
            Assert.AreEqual(-5, CurrencyMinorUnits.ToMinor(-0.045m, "CHF"));
        }
    }
}
