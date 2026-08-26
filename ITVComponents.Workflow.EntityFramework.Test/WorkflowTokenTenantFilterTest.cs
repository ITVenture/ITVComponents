using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Der Mandantenfilter an der Token-Zeile - und die Wege, die ihn ausdruecklich NICHT haben duerfen.
    /// </summary>
    /// <remarks>
    /// Die Fehlerart ist hier eine andere als bei Definitionen und Instanzen: dort bedeutet ein Fehler
    /// „sieht zu viel", hier „sieht nichts" - und ein Token, das niemand sieht, ist ein Vorgang, der
    /// stehen bleibt, ohne dass jemand etwas davon merkt. Deshalb pruefen die Tests unten beide
    /// Richtungen: dass der Filter greift, und dass er an den Stellen fehlt, wo er schaden wuerde.
    /// </remarks>
    [TestClass]
    public class WorkflowTokenTenantFilterTest
    {
        private SqliteConnection connection;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using var ctx = new WorkflowContext(options);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private WorkflowContext MakeContext(string tenant)
            => new WorkflowContext(
                new SqliteTestOptionsLoader(connection),
                TestServices.ForTenant(tenant),
                useTenantFilter: true,
                new WorkflowFilterInitializer<WorkflowContext>());

        private EfWorkflowStore StoreFor(string tenant) => new EfWorkflowStore(() => MakeContext(tenant));

        private static WorkflowDefinition PublicDefinition(string id)
            => new WorkflowDefinition { TechnicalName = id, Version = 1, TenantId = null, IsPublic = true, Name = id };

        private string NewInstance(EfWorkflowStore store, string id, params Token[] tokens)
        {
            store.SaveDefinition(PublicDefinition("d"));
            var instance = new WorkflowInstance
            {
                Id = id,
                DefinitionKey = store.GetDefinition("d", 1).Key,
                DefinitionId = "d",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Waiting,
                Tokens = tokens.ToList()
            };
            store.SaveInstance(instance);
            return instance.Id;
        }

        [TestMethod]
        public void TokensOfAnotherTenantAreInvisible()
        {
            NewInstance(StoreFor("acme"), "i1",
                new Token { Id = "t1", NodeId = "w", Status = TokenStatus.Waiting, WaitingSignal = "go" });

            using (WorkflowContext own = MakeContext("acme"))
            {
                Assert.AreEqual(1, own.Tokens.Count(), "the owner reads its own token rows");
            }

            using (WorkflowContext other = MakeContext("beta"))
            {
                // Der eigentliche Zweck: wer ueber db.Tokens einsteigt, sah bisher die Zeilen aller Mandanten.
                Assert.AreEqual(0, other.Tokens.Count(), "another tenant must not read the token rows");
                Assert.AreEqual(1, other.Tokens.IgnoreQueryFilters().Count(),
                    "the row is still there - it is the filter that hides it");
            }
        }

        [TestMethod]
        public void TokenIsStampedWithTheInstanceTenant()
        {
            NewInstance(StoreFor("acme"), "i1", new Token { Id = "t1", NodeId = "n", Status = TokenStatus.Active });

            using WorkflowContext ctx = MakeContext("acme");
            Assert.AreEqual("acme", ctx.Tokens.IgnoreQueryFilters().Single().TenantId,
                "the token carries the tenant of its instance");
        }

        /// <summary>
        /// Der Fall, um den es beim Nachtrag geht: eine Instanz, die seit vor der Spalte parkt, hat
        /// Token-Zeilen ohne Mandanten. Sie muss trotzdem vollstaendig laden - und beim naechsten
        /// Speichern den Mandanten nachziehen.
        /// </summary>
        [TestMethod]
        public void ParkedInstanceWithoutTokenTenantStillLoadsCompletelyAndHealsItself()
        {
            var store = StoreFor("acme");
            NewInstance(store, "i1",
                new Token { Id = "t1", NodeId = "w", Status = TokenStatus.Waiting, WaitingSignal = "go" });

            // Den Altbestand herstellen: Tenant an der Token-Zeile wieder leeren.
            using (WorkflowContext raw = MakeContext("acme"))
            {
                raw.Tokens.IgnoreQueryFilters().ExecuteUpdate(s => s.SetProperty(t => t.TenantId, (string)null));
            }

            WorkflowInstance reloaded = store.GetInstance("i1");
            Assert.IsNotNull(reloaded, "the instance itself is unaffected - its own tenant is set");
            Assert.AreEqual(1, reloaded.Tokens.Count,
                "a token without tenant must still load; otherwise the instance runs on with fewer tokens");

            // Speichern zieht den Mandanten nach - die Engine repariert den Altbestand im Vorbeigehen.
            store.SaveInstance(reloaded);
            using (WorkflowContext ctx = MakeContext("acme"))
            {
                Assert.AreEqual("acme", ctx.Tokens.IgnoreQueryFilters().Single().TenantId,
                    "saving re-stamps the denormalized tenant");
            }
        }

        /// <summary>
        /// Kein Duplikat beim Speichern: faende <c>SaveInstance</c> die bestehende Zeile nicht (weil ein
        /// Filter sie verdeckt), legte es sie neu an und liefe in eine Schluesselverletzung.
        /// </summary>
        [TestMethod]
        public void SavingOverAHiddenTokenRowDoesNotDuplicateIt()
        {
            var store = StoreFor("acme");
            NewInstance(store, "i1", new Token { Id = "t1", NodeId = "n", Status = TokenStatus.Active });

            using (WorkflowContext raw = MakeContext("acme"))
            {
                raw.Tokens.IgnoreQueryFilters().ExecuteUpdate(s => s.SetProperty(t => t.TenantId, "someone-else"));
            }

            WorkflowInstance instance = store.GetInstance("i1");
            store.SaveInstance(instance);

            using WorkflowContext ctx = MakeContext("acme");
            Assert.AreEqual(1, ctx.Tokens.IgnoreQueryFilters().Count(), "the row must be updated, not doubled");
        }

        /// <summary>
        /// Die Zahl steuert, wann der Runner das naechste Mal aufwacht. Mandantenweise beantwortet,
        /// schliefe er an den Terminen aller anderen vorbei.
        /// </summary>
        [TestMethod]
        public void NextTimerDueIsTenantBlind()
        {
            DateTime due = DateTime.UtcNow.AddMinutes(30);
            NewInstance(StoreFor("beta"), "i-beta",
                new Token { Id = "t1", NodeId = "timer", Status = TokenStatus.Waiting, DueUtc = due });

            DateTime? seenFromOtherTenant = StoreFor("acme").PeekNextTimerDueUtc(DateTime.UtcNow);

            Assert.IsNotNull(seenFromOtherTenant, "the runner's scheduling hint must not be tenant-scoped");
            // Auf die Ticks und nicht auf den Wert als Ganzes: SQLite liefert den Zeitpunkt ohne Kind
            // zurueck (Unspecified statt Utc). Das ist eine Eigenheit des Test-Providers und hat mit dem
            // Mandantenfilter nichts zu tun.
            Assert.AreEqual(due.Ticks, seenFromOtherTenant.Value.Ticks);
        }

        /// <summary>
        /// Ein Timer-Anspruch gehoert einem RUNNER, nicht einem Mandanten. Ein halb aufgeraeumter Runner
        /// waere schlechter als ein gar nicht aufgeraeumter.
        /// </summary>
        [TestMethod]
        public void ReleasingRunnerLeasesCrossesTenants()
        {
            NewInstance(StoreFor("beta"), "i-beta",
                new Token { Id = "t1", NodeId = "timer", Status = TokenStatus.Waiting, DueUtc = DateTime.UtcNow });

            using (WorkflowContext raw = MakeContext("beta"))
            {
                raw.Tokens.IgnoreQueryFilters().ExecuteUpdate(s => s
                    .SetProperty(t => t.TimerLeaseOwner, "runner-1#abc")
                    .SetProperty(t => t.TimerLeaseUntilUtc, DateTime.UtcNow.AddMinutes(5)));
            }

            StoreFor("acme").ReleaseLocksOfOwner("runner-1");

            using WorkflowContext ctx = MakeContext("beta");
            Assert.IsNull(ctx.Tokens.IgnoreQueryFilters().Single().TimerLeaseOwner,
                "the lease of that runner must be released regardless of the reading tenant");
        }
    }
}
