using System.Linq;
using System.Threading;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft das Rennen beim Erwerb einer Zweig-Sperre: der Schluessel ist beim Einfuegen belegt, beim
    /// Nachsehen aber schon wieder frei.
    /// </summary>
    /// <remarks>
    /// Genau dieses Fenster hat im Betrieb einen PK-Verstoss auf <c>BranchLocks</c> nach oben
    /// durchschlagen lassen - der sah nach einem Datenbank-Problem aus, war aber gewoehnliche
    /// Nebenlaeufigkeit: zwischen dem fehlgeschlagenen Einfuegen und der Pruefung „ist der Zweig
    /// gesperrt?" hatte der Besitzer freigegeben. Der Test stellt das ueber die Kontext-Factory
    /// deterministisch nach; ueber echte Threads waere es nicht zuverlaessig zu treffen.
    /// </remarks>
    [TestClass]
    public class WorkflowBranchLockRaceTest
    {
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

        [TestMethod]
        public void AReleaseBetweenInsertAndCheck_IsNotAnError()
        {
            // Ein anderer Besitzer haelt die Sperre.
            var holder = new EfWorkflowStore(() => new WorkflowContext(options));
            Assert.IsNotNull(holder.TryAcquireBranchLock("inst", "tok", "runner-A"));

            // Die Factory des zweiten Bewerbers: der ZWEITE Kontext ist der Pruef-Kontext des
            // Fehlerzweigs - kurz davor gibt der Besitzer frei. Genau das Fenster.
            int contexts = 0;
            var racing = new EfWorkflowStore(() =>
            {
                var ctx = new WorkflowContext(options);
                if (Interlocked.Increment(ref contexts) == 2)
                {
                    ctx.BranchLocks.Where(l => l.InstanceId == "inst" && l.TokenId == "tok")
                        .ExecuteDelete();
                }

                return ctx;
            });

            IWorkflowBranchLock acquired = racing.TryAcquireBranchLock("inst", "tok", "runner-B");

            Assert.IsNotNull(acquired,
                "the key was taken on insert and free on check - that is concurrency, not a failure. " +
                "The second attempt has to get the lock instead of throwing the primary key violation.");
            using (acquired)
            {
                using var check = new WorkflowContext(options);
                Assert.AreEqual("runner-B",
                    check.BranchLocks.Single(l => l.InstanceId == "inst" && l.TokenId == "tok").Owner);
            }
        }

        [TestMethod]
        public void APermanentlyHeldLock_IsStillReportedAsTaken()
        {
            var holder = new EfWorkflowStore(() => new WorkflowContext(options));
            using IWorkflowBranchLock a = holder.TryAcquireBranchLock("inst", "tok", "runner-A");
            Assert.IsNotNull(a);

            var other = new EfWorkflowStore(() => new WorkflowContext(options));

            Assert.IsNull(other.TryAcquireBranchLock("inst", "tok", "runner-B"),
                "the retry must not turn a genuinely held lock into an acquisition.");
        }
    }
}
