using System;
using System.Globalization;
using System.Threading;
using ITVComponents.EFRepo.DataSync;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.EFRepo.Test
{
    /// <summary>
    /// The config-exchange writes its values culture-invariant. These tests pin the reading side to the same rule -
    /// the culture of the user who applies a change must not decide what a price or a timestamp means.
    /// </summary>
    [TestClass]
    public class ChangeValueConverterTest
    {
        private CultureInfo previousCulture;

        public enum SampleKind
        {
            First = 0,
            Second = 1
        }

        [TestInitialize]
        public void SetUp()
        {
            previousCulture = Thread.CurrentThread.CurrentCulture;
            // "de" is the culture the reporting host runs on: comma as decimal separator, dot as group separator.
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de");
        }

        [TestCleanup]
        public void TearDown()
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }

        [TestMethod]
        public void DecimalIsReadInvariantOfTheCurrentCulture()
        {
            Assert.AreEqual(12.50m, ChangeValueConverter.ToTypedValue("12.50", typeof(decimal)));
            Assert.AreEqual(12.50m, ChangeValueConverter.ToTypedValue("12.50", typeof(decimal?)));
            Assert.AreEqual(12.50d, ChangeValueConverter.ToTypedValue("12.50", typeof(double)));
        }

        [TestMethod]
        public void EnumsAreReadFromNameAndFromNumber()
        {
            Assert.AreEqual(SampleKind.Second, ChangeValueConverter.ToTypedValue("Second", typeof(SampleKind)));
            Assert.AreEqual(SampleKind.Second, ChangeValueConverter.ToTypedValue("1", typeof(SampleKind)));
            Assert.AreEqual(SampleKind.Second, ChangeValueConverter.ToTypedValue("second", typeof(SampleKind)));
            Assert.AreEqual(SampleKind.Second, ChangeValueConverter.ToTypedValue("Second", typeof(SampleKind?)));
        }

        [TestMethod]
        public void EmptyTextMeansNoValueOnNullableTargets()
        {
            Assert.IsNull(ChangeValueConverter.ToTypedValue("", typeof(decimal?)));
            Assert.IsNull(ChangeValueConverter.ToTypedValue(null, typeof(SampleKind?)));
            Assert.AreEqual("", ChangeValueConverter.ToTypedValue("", typeof(string)));
            Assert.ThrowsExactly<FormatException>(() => ChangeValueConverter.ToTypedValue("", typeof(int)));
        }

        [TestMethod]
        public void TimestampsNeverComeBackAsLocalTime()
        {
            var utc = (DateTime)ChangeValueConverter.ToTypedValue("2025-09-19T15:59:59.0000000Z", typeof(DateTime));
            Assert.AreEqual(DateTimeKind.Utc, utc.Kind);
            Assert.AreEqual(new DateTime(2025, 9, 19, 15, 59, 59, DateTimeKind.Utc), utc);

            // A stamp without zone-information is taken as UTC rather than being shifted into the local zone.
            var unzoned = (DateTime)ChangeValueConverter.ToTypedValue("2025-09-19T15:59:59", typeof(DateTime?));
            Assert.AreEqual(DateTimeKind.Utc, unzoned.Kind);
            Assert.AreEqual(new DateTime(2025, 9, 19, 15, 59, 59, DateTimeKind.Utc), unzoned);

            // An offset is converted into UTC.
            var offset = (DateTime)ChangeValueConverter.ToTypedValue("2025-09-19T17:59:59+02:00", typeof(DateTime));
            Assert.AreEqual(DateTimeKind.Utc, offset.Kind);
            Assert.AreEqual(new DateTime(2025, 9, 19, 15, 59, 59, DateTimeKind.Utc), offset);
        }

        [TestMethod]
        public void TypesWithoutIConvertibleAreHandled()
        {
            var id = Guid.NewGuid();
            Assert.AreEqual(id, ChangeValueConverter.ToTypedValue(id.ToString(), typeof(Guid)));
            Assert.AreEqual(TimeSpan.FromMinutes(90), ChangeValueConverter.ToTypedValue("01:30:00", typeof(TimeSpan)));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 },
                (byte[])ChangeValueConverter.ToTypedValue(Convert.ToBase64String(new byte[] { 1, 2, 3 }), typeof(byte[])));
        }

        [TestMethod]
        public void SimpleTypesKeepWorking()
        {
            Assert.AreEqual(42, ChangeValueConverter.ToTypedValue("42", typeof(int)));
            Assert.AreEqual(true, ChangeValueConverter.ToTypedValue("True", typeof(bool)));
            Assert.AreEqual("text", ChangeValueConverter.ToTypedValue("text", typeof(string)));
        }
    }
}
