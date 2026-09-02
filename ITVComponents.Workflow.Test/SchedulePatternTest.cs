using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die <b>zerlegte</b> Form eines Zeitplan-Musters: aufmachen, aendern, wieder zusammensetzen.
    /// </summary>
    /// <remarks>
    /// Der wichtigste Test ist die <b>Rundreise</b>: was zerlegt und wieder zusammengebaut wurde, muss
    /// dasselbe Muster ergeben. Sonst verstellt der Designer beim blossen Oeffnen einen gepflegten Plan -
    /// und das faellt erst auf, wenn er zu einer anderen Zeit laeuft.
    /// </remarks>
    [TestClass]
    public class SchedulePatternTest
    {
        // Die Reihenfolge der Teile liegt fest: Periode, Startdatum, Zeiten, Wochentage, Monatstage,
        // Monate, Takt. Sie ist NICHT frei - der Takt steht am Ende, nicht direkt hinter der Uhrzeit.
        [TestMethod]
        [DataRow("d20200101080001", DisplayName = "daily at 08:00")]
        [DataRow("d20200101080001t", DisplayName = "daily, first run immediately")]
        [DataRow("w202001010800mon01", DisplayName = "weekly on monday")]
        [DataRow("w202001010700montuewedthufri02", DisplayName = "every 2nd week, weekdays")]
        [DataRow("m2020010108000101", DisplayName = "monthly on the 1st")]
        [DataRow("m202001010800-101", DisplayName = "monthly on the LAST day")]
        [DataRow("m20200101080001-101", DisplayName = "monthly on the 1st AND the last")]
        [DataRow("y20200101080001jan01", DisplayName = "yearly, 1st of january")]
        [DataRow("i20200101000015", DisplayName = "every 15 minutes")]
        public void RoundTrip_KeepsThePattern(string pattern)
        {
            Assert.IsTrue(SchedulePattern.TryParse(pattern, out SchedulePattern parsed, out string error),
                error);

            Assert.AreEqual(pattern, parsed.ToPattern(),
                "taking a pattern apart and putting it back together must not change it - otherwise "
                + "merely opening the designer rewrites a schedule that was fine.");
        }

        [TestMethod]
        public void Parse_ReadsTheParts()
        {
            Assert.IsTrue(SchedulePattern.TryParse("w202001010730montue02t", out SchedulePattern p, out _));

            Assert.AreEqual(SchedulePeriod.Week, p.Period);
            Assert.AreEqual(new DateTime(2020, 1, 1, 7, 30, 0), p.FirstDate);
            CollectionAssert.AreEquivalent(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }, p.WeekDays);
            Assert.AreEqual(2, p.Occurrence);
            Assert.IsTrue(p.RunFirstImmediately);
        }

        [TestMethod]
        public void Parse_LastDayOfMonth_IsMinusOne()
        {
            // Das, was Cron nicht kann - und deshalb der Grund, warum es diese Zerlegung ueberhaupt gibt.
            Assert.IsTrue(SchedulePattern.TryParse("m202001010800-101", out SchedulePattern p, out _));

            CollectionAssert.Contains(p.DaysOfMonth, -1);
        }

        [TestMethod]
        public void Parse_TrailingRubbish_IsRejected_NotSilentlyCut()
        {
            // DER Fund beim Bauen: der Regex der Auswertung ist nicht verankert. Ohne eigene Verankerung
            // liest Regex.Match den kuerzesten passenden ANFANG und laesst den Rest liegen - ein Muster
            // mit vertauschten Teilen wuerde dann klaglos als ein anderes, kuerzeres gelesen, und der
            // Plan liefe zu einer anderen Zeit als dasteht.
            Assert.IsFalse(SchedulePattern.TryParse("w20200101080001mon", out _, out string error),
                "the parts are in the wrong order (step before the weekdays) - that must not pass.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }

        [TestMethod]
        public void Validate_CatchesTheSameThing()
        {
            // Und dieselbe Strenge im Editor: was nur halb gelesen wird, tut nachweislich etwas anderes,
            // als es aussieht - das gehoert gemeldet, solange noch jemand davorsitzt.
            Assert.IsFalse(ScheduleEvaluator.TryValidate("w20200101080001mon", out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }

        [TestMethod]
        public void Parse_Garbage_FailsWithAReason()
        {
            Assert.IsFalse(SchedulePattern.TryParse("not a schedule", out _, out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error), "the reason belongs in the message.");
        }

        [TestMethod]
        public void Parse_Empty_FailsWithAReason()
        {
            Assert.IsFalse(SchedulePattern.TryParse("   ", out _, out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }

        [TestMethod]
        public void Build_ProducesSomethingTheEvaluatorAccepts()
        {
            // Die Probe aufs Exempel: was der Designer baut, muss die Auswertung lesen koennen. Ein
            // eigener Zusammenbau, den die Engine nicht versteht, waere die schlimmste Sorte Fehler -
            // im Editor sieht alles richtig aus, und der Plan schweigt.
            var pattern = new SchedulePattern
            {
                Period = SchedulePeriod.Week,
                FirstDate = new DateTime(2020, 1, 1, 8, 0, 0),
                WeekDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday },
                Occurrence = 1
            };

            string built = pattern.ToPattern();

            Assert.IsTrue(ScheduleEvaluator.TryValidate(built, out string error), $"{built}: {error}");
            Assert.AreEqual(3, ScheduleEvaluator.Preview(built, DateTime.UtcNow, 3).Count);
        }

        [TestMethod]
        public void Consistency_WeekWithoutWeekdays_IsRejected()
        {
            // Formal baubar, faktisch stumm - und der Benutzer soll es sofort erfahren, nicht erst, wenn
            // wochenlang nichts passiert.
            var pattern = new SchedulePattern { Period = SchedulePeriod.Week };

            Assert.IsFalse(pattern.IsConsistent(out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
        }

        [TestMethod]
        public void Consistency_MonthWithoutDays_IsRejected()
        {
            var pattern = new SchedulePattern { Period = SchedulePeriod.Month };

            Assert.IsFalse(pattern.IsConsistent(out _));
        }

        [TestMethod]
        public void Consistency_RemainderNotSmallerThanTheStep_IsRejected()
        {
            // Ein Rest, der so gross ist wie der Takt, kommt nie heraus - der Plan schwiege fuer immer.
            var pattern = new SchedulePattern
            {
                Period = SchedulePeriod.Day, Occurrence = 2, DesiredModulus = 2
            };

            Assert.IsFalse(pattern.IsConsistent(out string error));
            StringAssert.Contains(error, "2");
        }

        [TestMethod]
        public void Consistency_PlainDaily_IsFine()
        {
            var pattern = new SchedulePattern { Period = SchedulePeriod.Day, Occurrence = 1 };

            Assert.IsTrue(pattern.IsConsistent(out string error), error);
        }
    }
}
