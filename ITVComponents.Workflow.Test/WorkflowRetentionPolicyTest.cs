using System;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Retention;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Welche Aufbewahrungsfrist gilt - die Kette Mandant, Definition, global, und der Riegel davor.
    /// </summary>
    /// <remarks>
    /// Reine Rechnung, deshalb ohne Speicher und ohne Uhr. Die Frage "warum wurde das weggeraeumt?"
    /// beantwortet sich hier und nicht an einem Datensatz - und das ist der Grund, warum die Regel
    /// ueberhaupt eine eigene Klasse ist.
    /// </remarks>
    [TestClass]
    public class WorkflowRetentionPolicyTest
    {
        private static WorkflowDefinition Definition(int? days = null, bool allowOverride = false)
            => new WorkflowDefinition
            {
                Id = "wf",
                Version = 1,
                RetentionDays = days,
                AllowTenantRetentionOverride = allowOverride
            };

        private static WorkflowRetentionOverride Objection(int? days)
            => new WorkflowRetentionOverride { OwnerTenantId = "t1", DefinitionId = "wf", RetentionDays = days };

        private static WorkflowRetentionDefaults Global(int? days)
            => new WorkflowRetentionDefaults { RetentionDays = days };

        // --- Die Kette ---------------------------------------------------------------------------------

        [TestMethod]
        public void NobodySaysAnything_NothingIsCleanedUp()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(Definition(), null, null);

            Assert.IsFalse(result.Applies,
                "die sichere Richtung: wer nichts einstellt, verliert nichts.");
            Assert.AreEqual(RetentionSource.None, result.Source);
            Assert.IsNull(result.DueBefore(DateTime.UtcNow));
        }

        [TestMethod]
        public void OnlyTheGlobalDefault_Applies()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(Definition(), null, Global(90));

            Assert.AreEqual(90, result.Days);
            Assert.AreEqual(RetentionSource.Global, result.Source);
        }

        [TestMethod]
        public void TheDefinitionBeatsTheGlobalDefault()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(Definition(30), null, Global(90));

            Assert.AreEqual(30, result.Days);
            Assert.AreEqual(RetentionSource.Definition, result.Source);
        }

        [TestMethod]
        public void TheTenantBeatsTheDefinition_WhenAllowed()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(
                Definition(30, allowOverride: true), Objection(180), Global(90));

            Assert.AreEqual(180, result.Days);
            Assert.AreEqual(RetentionSource.Tenant, result.Source);
        }

        // --- Der Riegel --------------------------------------------------------------------------------

        /// <summary>
        /// Der Kern der Sache: eine Frist, der ein Mandant unbemerkt widersprechen kann, ist keine Frist.
        /// Die Vorgabe ist deshalb, dass sein Widerspruch NICHT wirkt.
        /// </summary>
        [TestMethod]
        public void TheObjectionDoesNothing_WhenTheDefinitionDoesNotAllowIt()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(
                Definition(30), Objection(180), Global(90));

            Assert.AreEqual(30, result.Days, "es bleibt bei der Vorgabe der Definition.");
            Assert.AreEqual(RetentionSource.Definition, result.Source);
            Assert.IsFalse(WorkflowRetentionPolicy.MayOverride(Definition()),
                "und ohne ausdrueckliche Erlaubnis darf niemand widersprechen.");
        }

        /// <summary>
        /// Der Widerspruch wird nicht abgelehnt, sondern er wirkt nur nicht - erlaubt die Definition ihn
        /// spaeter doch, soll der Wunsch des Mandanten noch da sein. Dieselbe Regel wie bei einer
        /// Muster-Uebersteuerung ohne <c>AllowReschedule</c>.
        /// </summary>
        [TestMethod]
        public void AnObjectionSurvivesUntilItIsAllowed()
        {
            WorkflowRetentionOverride objection = Objection(180);

            Assert.AreEqual(RetentionSource.Definition,
                WorkflowRetentionPolicy.Archive(Definition(30), objection, null).Source);
            Assert.AreEqual(RetentionSource.Tenant,
                WorkflowRetentionPolicy.Archive(Definition(30, allowOverride: true), objection, null).Source,
                "derselbe Widerspruch, nur die Definition hat ihre Meinung geaendert.");
        }

        // --- Der Rahmen -------------------------------------------------------------------------------

        private static WorkflowDefinition Bounded(int? days, int? min, int? max)
            => new WorkflowDefinition
            {
                Id = "wf",
                RetentionDays = days,
                AllowTenantRetentionOverride = true,
                MinTenantRetentionDays = min,
                MaxTenantRetentionDays = max
            };

        /// <summary>
        /// Innerhalb des Rahmens gilt der Wunsch unveraendert - und WasLimited sagt, dass nichts
        /// angefasst wurde.
        /// </summary>
        [TestMethod]
        public void AWishInsideTheBounds_IsGrantedAsIs()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(
                Bounded(90, min: 30, max: 365), Objection(180), null);

            Assert.AreEqual(180, result.Days);
            Assert.AreEqual(RetentionSource.Tenant, result.Source);
            Assert.IsFalse(result.WasLimited);
            Assert.IsNull(result.RequestedDays);
        }

        /// <summary>
        /// Der Fall, fuer den es den Rahmen gibt: eine vorgeschriebene Mindestaufbewahrung. Der Mandant
        /// darf laenger aufheben, aber nicht kuerzer.
        /// </summary>
        [TestMethod]
        public void TooShort_IsRaisedToTheMinimum_AndSaysSo()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(
                Bounded(90, min: 30, max: null), Objection(10), null);

            Assert.AreEqual(30, result.Days, "die Untergrenze setzt sich durch.");
            Assert.AreEqual(RetentionSource.Tenant, result.Source, "es bleibt SEIN Wunsch, nur begrenzt.");
            Assert.IsTrue(result.WasLimited,
                "still zu begrenzen hiesse: er stellt zehn Tage ein, bekommt dreissig und erfaehrt es nie.");
            Assert.AreEqual(10, result.RequestedDays, "was er wollte, bleibt ablesbar.");
        }

        /// <summary>Die Gegenrichtung: wo eine Loeschfrist gilt, darf niemand beliebig lange aufheben.</summary>
        [TestMethod]
        public void TooLong_IsCutToTheMaximum_AndSaysSo()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(
                Bounded(90, min: null, max: 365), Objection(3650), null);

            Assert.AreEqual(365, result.Days);
            Assert.IsTrue(result.WasLimited);
            Assert.AreEqual(3650, result.RequestedDays);
        }

        /// <summary>
        /// Der Rahmen begrenzt NUR den Widerspruch. Die Vorgabe der Definition ist die Norm - sie an
        /// ihren eigenen Grenzen zu beschneiden hiesse, dem Autor zu widersprechen.
        /// </summary>
        [TestMethod]
        public void TheBoundsDoNotTouchTheDefinitionsOwnDefault()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(
                Bounded(5, min: 30, max: 365), null, null);

            Assert.AreEqual(5, result.Days);
            Assert.AreEqual(RetentionSource.Definition, result.Source);
            Assert.IsFalse(result.WasLimited);
        }

        /// <summary>
        /// Ohne Erlaubnis nuetzt auch ein Rahmen nichts: der Widerspruch wirkt gar nicht, und dann ist
        /// auch nichts zu begrenzen.
        /// </summary>
        [TestMethod]
        public void WithoutPermission_TheBoundsAreNotEvenReached()
        {
            var definition = new WorkflowDefinition
            {
                Id = "wf",
                RetentionDays = 90,
                MinTenantRetentionDays = 30,
                AllowTenantRetentionOverride = false
            };

            EffectiveRetention result = WorkflowRetentionPolicy.Archive(definition, Objection(10), null);

            Assert.AreEqual(90, result.Days);
            Assert.AreEqual(RetentionSource.Definition, result.Source);
            Assert.IsFalse(result.WasLimited);
        }

        /// <summary>
        /// Ein widerspruechlicher Rahmen wird GANZ ignoriert. Welche der beiden Grenzen "gewinnt", waere
        /// geraten - beide Antworten liessen sich begruenden, und genau deshalb darf die Entscheidung
        /// hier nicht fallen.
        /// </summary>
        [TestMethod]
        public void ContradictoryBounds_AreIgnoredEntirely()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(
                Bounded(90, min: 365, max: 30), Objection(100), null);

            Assert.AreEqual(100, result.Days, "der Wunsch gilt unveraendert.");
            Assert.IsFalse(result.WasLimited, "und es wird nicht behauptet, es sei begrenzt worden.");
        }

        // --- Grenzfaelle -------------------------------------------------------------------------------

        /// <summary>
        /// Null Tage sind eine sinnvolle Ansage ("sofort nach dem Ende") und duerfen nicht als "nichts
        /// gesagt" durchfallen - sonst raeumte ausgerechnet die schaerfste Einstellung gar nicht auf.
        /// </summary>
        [TestMethod]
        public void ZeroDays_MeansImmediately_NotUnset()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(Definition(0), null, Global(90));

            Assert.IsTrue(result.Applies);
            Assert.AreEqual(0, result.Days);
            Assert.AreEqual(RetentionSource.Definition, result.Source);
        }

        /// <summary>
        /// Eine negative Frist ist keine kuerzere, sondern ein Fehler - und sie darf nicht zu einem
        /// Stichtag in der ZUKUNFT fuehren. Der raeumte Vorgaenge weg, die noch gar nicht geendet haben.
        /// </summary>
        [TestMethod]
        public void ANegativeValue_IsIgnored_NotTurnedIntoAFutureCutoff()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(Definition(-5), null, Global(90));

            Assert.AreEqual(90, result.Days, "es faellt auf die naechste gueltige Stufe zurueck.");
            Assert.AreEqual(RetentionSource.Global, result.Source);

            var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(result.DueBefore(now) < now, "der Stichtag liegt IMMER in der Vergangenheit.");
        }

        [TestMethod]
        public void TheCutoffCountsBackFromNow()
        {
            var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(Definition(30), null, null);

            Assert.AreEqual(new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc), result.DueBefore(now));
        }

        /// <summary>
        /// Die beiden Fristen sind unabhaengig: die Anhang-Inhalte koennen laenger oder kuerzer bleiben
        /// als der Vorgang selbst. Wer sie in einen Wert zusammenzoege, koennte genau das nicht mehr.
        /// </summary>
        [TestMethod]
        public void AttachmentsHaveTheirOwnClock()
        {
            var definition = new WorkflowDefinition
            {
                Id = "wf",
                RetentionDays = 30,
                AttachmentRetentionDays = 180
            };

            Assert.AreEqual(30, WorkflowRetentionPolicy.Archive(definition, null, null).Days);
            Assert.AreEqual(180, WorkflowRetentionPolicy.Attachments(definition, null, null).Days);
        }

        /// <summary>
        /// Ohne Definition wirkt kein Widerspruch. Dann ist naemlich nicht zu entscheiden, ob sie ihn
        /// zulaesst - und "im Zweifel wirkt er" waere bei Fristen die falsche Richtung.
        /// </summary>
        [TestMethod]
        public void WithoutADefinition_NoObjectionApplies()
        {
            EffectiveRetention result = WorkflowRetentionPolicy.Archive(null, Objection(1), Global(90));

            Assert.AreEqual(90, result.Days);
            Assert.AreEqual(RetentionSource.Global, result.Source);
        }
    }
}
