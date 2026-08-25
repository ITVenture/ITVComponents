using System;
using System.Collections.Generic;
using ITVComponents.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die Auswertung eines Zeitplan-Musters fuer Aufrufer, die ihren Plan <b>nicht</b> als
    /// lebendes Objekt halten, sondern seinen Stand in einer Datenbank fuehren.
    /// </summary>
    [TestClass]
    public class ScheduleEvaluatorTest
    {
        /// <summary>Taeglich um 08:00, ab 01.01.2020.</summary>
        private const string DailyAtEight = "d20200101080001";

        /// <summary>Dasselbe, aber mit dem Kennzeichen "der erste Lauf sofort".</summary>
        private const string DailyAtEightImmediate = "d20200101080001t";

        [TestMethod]
        public void NextDue_IsInTheFuture_AndInUtc()
        {
            DateTime nowUtc = DateTime.UtcNow;

            DateTime? next = ScheduleEvaluator.NextDueUtc(DailyAtEight, nowUtc, nowUtc);

            Assert.IsNotNull(next);
            Assert.IsTrue(next > nowUtc, "the next date must lie ahead of the anchor.");
            // Acht Uhr ORTSZEIT - in UTC ist das je nach Zone und Jahreszeit etwas anderes. Genau das ist
            // der Punkt: gerechnet wird lokal, gespeichert wird UTC.
            Assert.AreEqual(8, next.Value.ToLocalTime().Hour);
            Assert.AreEqual(0, next.Value.ToLocalTime().Minute);
        }

        [TestMethod]
        public void Immediately_OnlyAppliesWhenItNeverRan()
        {
            // DIE Falle des Musters: das Kennzeichen ist im TimeTable-Objekt ein verbrauchbarer Merker.
            // Wer den Plan je Auswertung neu baut, bekaeme sonst jedes Mal "jetzt sofort" - also Dauerfeuer.
            DateTime nowUtc = DateTime.UtcNow;

            DateTime? firstEver = ScheduleEvaluator.NextDueUtc(DailyAtEightImmediate, null, nowUtc);
            DateTime? afterARun = ScheduleEvaluator.NextDueUtc(DailyAtEightImmediate, nowUtc, nowUtc);

            Assert.AreEqual(nowUtc, firstEver, "never run yet - that is what 'immediately' means.");
            Assert.IsTrue(afterARun > nowUtc, "after the first run the pattern decides, not the flag.");
        }

        [TestMethod]
        public void WithoutTheFlag_ANewScheduleDoesNotFireRightAway()
        {
            DateTime nowUtc = DateTime.UtcNow;

            Assert.IsTrue(ScheduleEvaluator.NextDueUtc(DailyAtEight, null, nowUtc) > nowUtc,
                "a fresh schedule without the flag must wait for its first regular date.");
        }

        [TestMethod]
        public void AfterALongStandstill_TheNextDateIsAheadOfUs()
        {
            // Ein laengerer Stillstand erzeugt KEINE Kette verpasster Termine. Wer drei Tage aus war,
            // bekommt nicht drei Laeufe nachgereicht, sondern den naechsten Termin.
            //
            // Nachgeholt wird trotzdem - aber ueber die GESPEICHERTE Faelligkeit, nicht hier: die bleibt
            // stehen, solange sie niemand aufgreift, und feuert beim naechsten Aufgriff einmal. Genau
            // das ist "einmal nachholen, nicht n-mal".
            DateTime nowUtc = DateTime.UtcNow;

            DateTime? next = ScheduleEvaluator.NextDueUtc(DailyAtEight, nowUtc.AddDays(-3), nowUtc);

            Assert.IsNotNull(next);
            Assert.IsTrue(next > nowUtc, "the computed date is always the next one ahead, never a missed one.");
            Assert.IsTrue(next < nowUtc.AddDays(2), "and it is the NEXT one - not one three days from now.");
        }

        [TestMethod]
        public void AfterALongStandstill_TheNextDateIsAheadOfUs_AtEveryTimeOfDay()
        {
            // Derselbe Fall wie oben, aber unabhaengig davon, wie spaet es gerade ist.
            //
            // Der Test darueber traf den Fehler nur zwischen Mitternacht und 08:00 Ortszeit: TimeTable
            // baut den Termin AUS DEM ANKERDATUM, sobald an jenem Tag noch eine Tageszeit uebrig ist -
            // und "noch uebrig" haengt an der Tageszeit des Ankers, die aus DateTime.UtcNow stammt. Am
            // Nachmittag war 08:00 durch, die zukunfts-erzwingende Rekursion sprang an, und der Fehler
            // blieb unsichtbar. Acht Stunden am Tag war die Suite rot, sechzehn gruen.
            //
            // Hier wird der Anker deshalb ausdruecklich auf 01:00 ORTSZEIT gelegt - vor der ersten
            // Tageszeit des Musters, in jeder Zeitzone.
            DateTime anchorUtc = DateTime.SpecifyKind(
                DateTime.Now.Date.AddDays(-3).AddHours(1), DateTimeKind.Local).ToUniversalTime();
            DateTime nowUtc = DateTime.UtcNow;

            DateTime? next = ScheduleEvaluator.NextDueUtc(DailyAtEight, anchorUtc, nowUtc);

            Assert.IsNotNull(next);
            Assert.IsTrue(next > nowUtc,
                "an anchor whose time-of-day precedes the schedule must not yield that day's date.");
        }

        [TestMethod]
        public void TryValidate_RejectsGarbage()
        {
            Assert.IsFalse(ScheduleEvaluator.TryValidate("not a schedule", out string error));
            Assert.IsFalse(string.IsNullOrWhiteSpace(error), "the reason belongs in the message.");
        }

        [TestMethod]
        public void TryValidate_RejectsEmpty()
        {
            Assert.IsFalse(ScheduleEvaluator.TryValidate("   ", out _));
        }

        [TestMethod]
        public void TryValidate_AcceptsAWorkingPattern()
        {
            Assert.IsTrue(ScheduleEvaluator.TryValidate(DailyAtEight, out string error), error);
        }

        [TestMethod]
        public void Preview_ReturnsAscendingDates()
        {
            IReadOnlyList<DateTime> preview = ScheduleEvaluator.Preview(DailyAtEight, DateTime.UtcNow, 3);

            Assert.AreEqual(3, preview.Count);
            for (int i = 1; i < preview.Count; i++)
            {
                Assert.IsTrue(preview[i] > preview[i - 1], "the preview must move forward.");
            }
        }

        [TestMethod]
        public void Preview_OnGarbage_IsEmptyInsteadOfThrowing()
        {
            // Die Vorschau laeuft an der Tastatur des Benutzers und sieht jedes halb getippte Muster.
            Assert.AreEqual(0, ScheduleEvaluator.Preview("d2020", DateTime.UtcNow, 3).Count);
        }
    }
}
