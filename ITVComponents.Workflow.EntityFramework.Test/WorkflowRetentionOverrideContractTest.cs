using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Retention;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Die Ablage der <b>Widersprueche gegen die Aufbewahrungsfristen</b> - gefahren gegen <b>beide</b>
    /// Speicher-Fassungen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wie bei <see cref="WorkflowTriggerLifecycleContractTest"/>: was doppelt gepflegt bleibt, muss
    /// nachweislich dasselbe tun. Die eine Fassung arbeitet auf Domaenenobjekten in einem Dictionary,
    /// die andere auf EF-Zeilen mit <c>IgnoreQueryFilters</c> und <c>SaveChanges</c> - ein gruener
    /// In-Memory-Test allein bewiese fuer den Betrieb nichts.
    /// </para>
    /// <para>
    /// <b>Was dieser Test NICHT beweist:</b> die Eindeutigkeit auf Datenbank-Ebene, wenn eine der drei
    /// Spalten null ist. SQLite behandelt NULLs im eindeutigen Index als VERSCHIEDEN (wie PostgreSQL,
    /// anders als SQL Server) - dass hier trotzdem keine zweite Zeile entsteht, traegt der Store mit
    /// seinem Lookup vor dem Einfuegen. Der Index ist der Riegel der Datenbank dahinter; fuer
    /// PostgreSQL setzt die Migration dafuer <c>NULLS NOT DISTINCT</c>.
    /// </para>
    /// </remarks>
    [TestClass]
    public class WorkflowRetentionOverrideContractTest
    {
        private const string Memory = "memory";
        private const string Ef = "ef";

        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using var ctx = new WorkflowContext(options);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private IWorkflowStore NewStore(string kind)
            => kind == Ef
                ? new EfWorkflowStore(() => new WorkflowContext(options))
                : new InMemoryWorkflowStore();

        /// <summary>Ein Widerspruch des Mandanten <paramref name="tenantId"/> gegen die Definition.</summary>
        private static WorkflowRetentionOverride Objection(string ownerTenantId, string tenantId,
            int? days = 90, int? attachmentDays = null, string setBy = "admin")
            => new WorkflowRetentionOverride
            {
                OwnerTenantId = ownerTenantId,
                DefinitionId = "wf",
                TenantId = tenantId,
                RetentionDays = days,
                AttachmentRetentionDays = attachmentDays,
                SetBy = setBy
            };

        // --- Anlegen und Wiederfinden ------------------------------------------------------------------

        [TestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void ASavedObjection_IsFoundOnBothReadingPaths(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveRetentionOverride(Objection(null, "t1", days: 90, attachmentDays: 30));

            WorkflowRetentionOverride byTenant = store.GetRetentionOverrides("t1").Single();
            WorkflowRetentionOverride byDefinition = store.GetRetentionOverridesForDefinition(null, "wf")
                .Single();

            Assert.AreEqual(90, byTenant.RetentionDays, $"[{kind}] the archive deadline comes back.");
            Assert.AreEqual(30, byTenant.AttachmentRetentionDays,
                $"[{kind}] and so does the attachment deadline - they are two deadlines, not one.");
            Assert.AreEqual("admin", byTenant.SetBy, $"[{kind}] who set it is part of the record.");
            Assert.AreEqual("t1", byDefinition.TenantId,
                $"[{kind}] the run reads by definition and needs to know whose objection it is.");
        }

        [TestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void SavingTwice_ChangesTheOneRow_InsteadOfAddingASecond(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveRetentionOverride(Objection(null, "t1", days: 90));
            store.SaveRetentionOverride(Objection(null, "t1", days: 180, setBy: "somebody-else"));

            WorkflowRetentionOverride only = store.GetRetentionOverrides("t1").Single();

            Assert.AreEqual(180, only.RetentionDays, $"[{kind}] the newer wish counts.");
            Assert.AreEqual("somebody-else", only.SetBy,
                $"[{kind}] and the record says who changed it - not who set it first.");
        }

        // --- Die Identitaet ----------------------------------------------------------------------------

        [TestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void TheOwnerIsPartOfTheIdentity_PublicAndOwnDefinitionAreNotTheSame(string kind)
        {
            // Der Fall, fuer den der Besitzer in der Identitaet steht: derselbe Mandant, derselbe
            // Definitions-Name - einmal die oeffentliche Definition, einmal seine eigene. Ohne den
            // Besitzer waere das EINE Zeile, und sein Widerspruch gegen die eine wirkte still auch
            // gegen die andere, obwohl die zwei Definitionen verschiedene Rahmen setzen und die eine
            // ihn vielleicht gar nicht erlaubt.
            IWorkflowStore store = NewStore(kind);
            store.SaveRetentionOverride(Objection(null, "t1", days: 90));
            store.SaveRetentionOverride(Objection("t1", "t1", days: 7));

            IReadOnlyList<WorkflowRetentionOverride> mine = store.GetRetentionOverrides("t1");

            Assert.AreEqual(2, mine.Count, $"[{kind}] two definitions, two objections.");
            Assert.AreEqual(90, store.GetRetentionOverridesForDefinition(null, "wf").Single().RetentionDays,
                $"[{kind}] the public definition keeps its own.");
            Assert.AreEqual(7, store.GetRetentionOverridesForDefinition("t1", "wf").Single().RetentionDays,
                $"[{kind}] and so does the tenant's own.");
        }

        [TestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void EachTenantHasItsOwnObjection_AndSeesOnlyIt(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveRetentionOverride(Objection(null, "t1", days: 90));
            store.SaveRetentionOverride(Objection(null, "t2", days: 365));

            Assert.AreEqual(90, store.GetRetentionOverrides("t1").Single().RetentionDays,
                $"[{kind}] a tenant sees what it set itself.");
            Assert.AreEqual(365, store.GetRetentionOverrides("t2").Single().RetentionDays,
                $"[{kind}] and the other one its own.");
            Assert.AreEqual(2, store.GetRetentionOverridesForDefinition(null, "wf").Count,
                $"[{kind}] the retention run takes the definition and needs both at once.");
        }

        [TestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void ATenantlessOperation_CanObjectToo(string kind)
        {
            // Im Ein-Mandanten-Betrieb traegt alles null. Eine Ablage, die das nicht wiederfindet, waere
            // dort wirkungslos - und zwar lautlos.
            IWorkflowStore store = NewStore(kind);
            store.SaveRetentionOverride(Objection(null, null, days: 30));

            Assert.AreEqual(30, store.GetRetentionOverrides(null).Single().RetentionDays,
                $"[{kind}] null is a tenant like any other here.");
            Assert.AreEqual(30, store.GetRetentionOverridesForDefinition(null, "wf").Single().RetentionDays,
                $"[{kind}] and the run finds it as well.");
        }

        // --- Die Ruecknahme ----------------------------------------------------------------------------

        [TestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void Withdrawing_KeepsTheRow_AndLetsTheDefaultApplyAgain(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveRetentionOverride(Objection(null, "t1", days: 90));
            store.SaveRetentionOverride(Objection(null, "t1", days: null, setBy: "the-one-who-withdrew"));

            WorkflowRetentionOverride withdrawn = store.GetRetentionOverrides("t1").Single();

            Assert.IsNull(withdrawn.RetentionDays,
                $"[{kind}] the deadline is gone - null must be written, not fallen back from.");
            Assert.AreEqual("the-one-who-withdrew", withdrawn.SetBy,
                $"[{kind}] the row stays exactly for this: who took the deadline back, and when.");

            EffectiveRetention effective = WorkflowRetentionPolicy.Archive(
                new WorkflowDefinition
                {
                    TechnicalName = "wf", Version = 1, RetentionDays = 400, AllowTenantRetentionOverride = true
                },
                withdrawn, null);

            Assert.AreEqual(RetentionSource.Definition, effective.Source,
                $"[{kind}] a withdrawn objection is 'nothing said' - the definition's default applies again.");
            Assert.AreEqual(400, effective.Days, $"[{kind}] and with its own value.");
        }

        // --- Was die Ablage beisteuert -----------------------------------------------------------------

        [TestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void TheStoreStampsTheTime_ButNeverOverwritesOne(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            DateTime before = DateTime.UtcNow.AddSeconds(-1);
            store.SaveRetentionOverride(Objection(null, "t1"));

            DateTime stamped = store.GetRetentionOverrides("t1").Single().SetUtc;

            Assert.IsTrue(stamped >= before, $"[{kind}] the store brings the clock the rule deliberately lacks.");
            Assert.AreEqual(DateTimeKind.Utc, stamped.Kind,
                $"[{kind}] a field named Utc must say so itself - otherwise the next ToUniversalTime shifts it.");

            DateTime backdated = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            WorkflowRetentionOverride carried = Objection(null, "t2");
            carried.SetUtc = backdated;
            store.SaveRetentionOverride(carried);

            Assert.AreEqual(backdated, store.GetRetentionOverrides("t2").Single().SetUtc,
                $"[{kind}] a time the caller brings along stays - the import of an existing state is one.");
        }

        [TestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void AnObjectionWithoutADefinition_IsRefused(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowRetentionOverride nameless = Objection(null, "t1");
            nameless.DefinitionId = null;

            Assert.ThrowsExactly<ArgumentException>(() => store.SaveRetentionOverride(nameless),
                $"[{kind}] without a definition it is not decidable whose deadline is contradicted.");
            Assert.ThrowsExactly<ArgumentNullException>(() => store.SaveRetentionOverride(null),
                $"[{kind}] and nothing at all is not an objection either.");
        }
    }
}
