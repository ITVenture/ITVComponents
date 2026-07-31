using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Prueft die Aufloesung von <b>Konstruktor-Ueberladungen</b>. Der Anlass ist ein realer Fall aus dem
    /// Workflow-Designer: <c>TimerNode.DueExpression</c> erwartet ein <see cref="DateTime"/> ODER eine
    /// <see cref="TimeSpan"/> - und genau <c>new TimeSpan(0,0,10)</c> wurde als "Ueberladung existiert
    /// nicht" abgelehnt.
    /// </summary>
    [TestClass]
    public class ConstructorOverloadTest
    {
        private static object Parse(string expression, IDictionary<string, object> variables = null)
            => ScriptInterpreter.Parse(expression,
                variables ?? new Dictionary<string, object> { { "TimeSpan", typeof(TimeSpan) } });

        private static Dictionary<string, object> WithTimeSpan()
            => new Dictionary<string, object> { { "TimeSpan", typeof(TimeSpan) } };

        [TestMethod]
        public void TimeSpan_HoursMinutesSeconds_IsFound()
        {
            Assert.AreEqual(new TimeSpan(0, 0, 10), Parse("new TimeSpan(0,0,10)", WithTimeSpan()),
                "die (int,int,int)-Ueberladung muss getroffen werden.");
        }

        [TestMethod]
        public void TimeSpan_SingleTicksArgument_StillWorks()
        {
            // Die 1-Argument-Ueberladung nimmt long - ein int-Literal muss dorthin konvertiert werden.
            Assert.AreEqual(new TimeSpan(500L), Parse("new TimeSpan(500)", WithTimeSpan()));
        }

        [TestMethod]
        public void TimeSpan_DaysHoursMinutesSeconds_IsFound()
        {
            Assert.AreEqual(new TimeSpan(1, 2, 3, 4), Parse("new TimeSpan(1,2,3,4)", WithTimeSpan()));
        }

        [TestMethod]
        public void TimeSpan_ResolvesTheSameWayTwice()
        {
            // Die Kandidaten werden je Argumentanzahl gepuffert - der zweite Aufruf darf nicht auf einem
            // falsch gemerkten Kandidaten landen.
            Assert.AreEqual(new TimeSpan(0, 0, 10), Parse("new TimeSpan(0,0,10)", WithTimeSpan()));
            Assert.AreEqual(new TimeSpan(0, 0, 10), Parse("new TimeSpan(0,0,10)", WithTimeSpan()));
            Assert.AreEqual(new TimeSpan(0, 5, 0), Parse("new TimeSpan(0,5,0)", WithTimeSpan()));
        }

        [TestMethod]
        public void DateTime_YearMonthDay_IsFound()
        {
            var vars = new Dictionary<string, object> { { "DateTime", typeof(DateTime) } };
            Assert.AreEqual(new DateTime(2026, 7, 31), Parse("new DateTime(2026,7,31)", vars));
        }
    }
}
