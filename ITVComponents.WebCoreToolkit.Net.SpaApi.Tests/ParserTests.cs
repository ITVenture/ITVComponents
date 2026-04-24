using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Tests
{
    [TestClass]
    public class ParserTests
    {
        public ParserTests()
        {
        }

        [TestMethod]
        public void TestBasicTokenizer()
        {
            List<TestData> testData = new List<TestData>
            {
                new TestData{Id=1, Field1 = 118, Field2 = "locallocahol(~drio)", Field3 = null, Field4 = "Hooorndampf", Field5 = 7},
                new TestData{Id=2, Field1 = 119, Field2 = "locallocahol((~~drio))", Field3 = "peng", Field4 = "horndampf", Field5 = 3},
                new TestData{Id=3, Field1 = 119, Field2 = "locallocahol(~drio)", Field3 = "peng", Field4 = "Horndampf", Field5 = 3},
                new TestData{Id=4, Field1 = 120, Field2 = "locallocahol((~~drio))", Field3 = "peng", Field4 = "Hooorndampf", Field5 = 3}
            };
            var filterParser = new FilterParser();
            var filterRaw = filterParser.GetFilter("(~(~Field1~eq~119~or~Field2~ct~hol((~~drio))~)~and~Field4~neq~horndampf~)~and~(~Field3~innl~or~Field5~bt~1~5~)");
            var filter = filterRaw.First().ToFilter();
            var filterExpression = ExpressionBuilder.BuildExpression<TestData>(filter);
            var resultItem = testData.FirstOrDefault(filterExpression.Compile());
            Assert.IsNotNull(resultItem);
            Assert.IsTrue(resultItem.Id==3);
        }
    }


    public class TestData
    {
        public int Id { get; set; }
        public int Field1 { get; set; }

        public string Field2 { get; set; }

        public string Field3 { get; set; }

        public string Field4 { get; set; }

        public int Field5 { get; set; }
    }
}
