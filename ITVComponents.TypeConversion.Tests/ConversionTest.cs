using ITVComponents.DataAccess.Extensions;
using ITVComponents.Security;
using ITVComponents.TypeConversion;
using ITVComponents.TypeConversion.DefaultConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.TypeConversion.Tests
{
    [TestClass]
    public class ConversionTest
    {
        public ConversionTest()
        {
            new EnumConverter();
            new NullableConverter();
        }

        [TestMethod]
        public void TestConversions()
        {
            byte? raw1 = 0;
            byte raw2 = 1;
            var ok1 = TypeConverter.Convert(raw1, typeof(TestEnum001?));
            var ok2 = TypeConverter.Convert(raw2, typeof(TestEnum001?));
            var ok3 = new Model1 { Horn = 1 }.ToViewModel<Model1, Model2>();
            Assert.AreEqual(TestEnum001.Value1, ok1);
            Assert.AreEqual(TestEnum001.Value2, ok2);
            Assert.AreEqual(TestEnum001.Value2, ok3.Horn);
        }
    }

    public enum TestEnum001
    {
        Value1,
        Value2
    }

    public class Model1
    {
        public byte Horn { get; set; }
    }

    public class Model2
    {
        public TestEnum001? Horn { get; set; }
    }
}
