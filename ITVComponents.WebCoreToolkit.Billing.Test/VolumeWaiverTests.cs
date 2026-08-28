using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// The price model behind "turn over enough and the base fee is on us". Tested as arithmetic, because that
    /// is what it is: the question whether the sliding band actually removes the revenue dent has an exact
    /// answer, and rolling out a mode that does not do what it promises is the failure worth preventing.
    /// <para>
    /// Figures throughout: base fee CHF 20.— (2000 minor), threshold CHF 10'000.— (1'000'000 minor), commission
    /// 1 % (100 basis points).
    /// </para>
    /// </summary>
    [TestClass]
    public class VolumeWaiverTests
    {
        private const long Threshold = 1_000_000;
        private const long BaseFee = 2_000;

        [TestMethod]
        public void HardModeWaivesNothingBelowTheThreshold()
        {
            var options = new VolumeWaiverOptions { Enabled = true, Mode = WaiverMode.Hard, ThresholdMinor = Threshold };
            Assert.AreEqual(0, VolumeWaiver.Waived(options, Threshold - 1, BaseFee));
            Assert.AreEqual(BaseFee, VolumeWaiver.Waived(options, Threshold, BaseFee));
            Assert.AreEqual(BaseFee, VolumeWaiver.Waived(options, Threshold * 5, BaseFee), "never more than the position itself");
        }

        [TestMethod]
        public void DisabledOrUnconfiguredWaivesNothing()
        {
            Assert.AreEqual(0, VolumeWaiver.Waived(new VolumeWaiverOptions { Enabled = false, ThresholdMinor = Threshold }, Threshold * 2, BaseFee));
            Assert.AreEqual(0, VolumeWaiver.Waived(new VolumeWaiverOptions { Enabled = true, ThresholdMinor = 0 }, Threshold * 2, BaseFee));
        }

        [TestMethod]
        public void SlidingModeGrowsLinearlyOverTheBand()
        {
            var options = new VolumeWaiverOptions
            {
                Enabled = true,
                Mode = WaiverMode.Sliding,
                ThresholdMinor = Threshold,
                WaiverRampStartMinor = 800_000
            };

            Assert.AreEqual(0, VolumeWaiver.Waived(options, 800_000, BaseFee), "at the band start nothing is waived yet");
            Assert.AreEqual(1_000, VolumeWaiver.Waived(options, 900_000, BaseFee), "halfway through the band, half the fee");
            Assert.AreEqual(BaseFee, VolumeWaiver.Waived(options, Threshold, BaseFee));
            Assert.AreEqual(0, VolumeWaiver.Waived(options, 700_000, BaseFee), "below the band nothing is waived");
        }

        [TestMethod]
        public void NegativeTurnoverCountsAsZero()
        {
            // Many refunds can push the net turnover below zero. For the threshold that is simply "not reached";
            // it must not turn into a negative waiver.
            var options = new VolumeWaiverOptions { Enabled = true, Mode = WaiverMode.Sliding, ThresholdMinor = Threshold, WaiverRampStartMinor = 800_000 };
            Assert.AreEqual(0, VolumeWaiver.Waived(options, -50_000, BaseFee));
        }

        [TestMethod]
        public void AnUnusableBandFallsBackToTheHardThreshold()
        {
            // Ramp start at or above the threshold cannot describe a band. Falling back to the hard threshold is
            // visibly conservative; computing something anyway would be quietly wrong.
            var atThreshold = new VolumeWaiverOptions { Enabled = true, Mode = WaiverMode.Sliding, ThresholdMinor = Threshold, WaiverRampStartMinor = Threshold };
            Assert.IsFalse(VolumeWaiver.HasUsableBand(atThreshold));
            Assert.AreEqual(0, VolumeWaiver.Waived(atThreshold, 900_000, BaseFee));
            Assert.AreEqual(BaseFee, VolumeWaiver.Waived(atThreshold, Threshold, BaseFee));

            var negative = new VolumeWaiverOptions { Enabled = true, Mode = WaiverMode.Sliding, ThresholdMinor = Threshold, WaiverRampStartMinor = -1 };
            Assert.IsFalse(VolumeWaiver.HasUsableBand(negative));

            var hard = new VolumeWaiverOptions { Enabled = true, Mode = WaiverMode.Hard, ThresholdMinor = Threshold, WaiverRampStartMinor = 800_000 };
            Assert.IsFalse(VolumeWaiver.HasUsableBand(hard), "the ramp is only read in sliding mode");
        }

        [TestMethod]
        public void BandWidthDecidesWhetherTheDentIsRemovedAtAll()
        {
            // The condition is (threshold - ramp) * rate >= baseFee. At 1 % and CHF 20.— that means the band has
            // to be at least CHF 2'000.— wide; anything narrower merely stretches the dent.
            Assert.IsFalse(VolumeWaiver.IsBandWideEnough(Threshold, 900_000, 100, BaseFee), "CHF 1'000 band: 10.— of commission against a 20.— fee");
            Assert.IsTrue(VolumeWaiver.IsBandWideEnough(Threshold, 800_000, 100, BaseFee), "CHF 2'000 band is exactly the break-even case");
            Assert.IsTrue(VolumeWaiver.IsBandWideEnough(Threshold, 600_000, 100, BaseFee), "a wider band leaves revenue rising throughout");
        }

        [TestMethod]
        public void BandWidthCheckHandlesTheDegenerateCases()
        {
            Assert.IsTrue(VolumeWaiver.IsBandWideEnough(Threshold, 800_000, 100, 0), "nothing to waive, nothing to compensate");
            Assert.IsFalse(VolumeWaiver.IsBandWideEnough(Threshold, 800_000, 0, BaseFee), "without a commission no band can ever compensate");
            Assert.IsFalse(VolumeWaiver.IsBandWideEnough(Threshold, Threshold, 100, BaseFee), "a band of zero width compensates nothing");
        }

        [TestMethod]
        public void PerCurrencyBlockReplacesTheBase()
        {
            var options = new VolumeWaiverOptions
            {
                Enabled = true,
                ThresholdMinor = Threshold,
                PerCurrency = new System.Collections.Generic.Dictionary<string, VolumeWaiverOptions>
                {
                    ["EUR"] = new() { Enabled = true, ThresholdMinor = 500_000 }
                }
            };

            Assert.AreEqual(Threshold, VolumeWaiver.Resolve(options, "CHF").ThresholdMinor);
            Assert.AreEqual(500_000, VolumeWaiver.Resolve(options, "eur").ThresholdMinor);
            Assert.AreEqual(Threshold, VolumeWaiver.Resolve(options, null).ThresholdMinor);
        }
    }
}
