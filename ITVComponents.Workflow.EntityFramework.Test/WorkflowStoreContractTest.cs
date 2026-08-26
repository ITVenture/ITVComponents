using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Faehrt DIESELBEN Szenarien gegen beide <see cref="IWorkflowStore"/>-Fassungen - den
    /// <see cref="InMemoryWorkflowStore"/> und den <see cref="EfWorkflowStore"/> ueber eine echte
    /// (In-Memory-)SQLite-Datenbank. Gegenstand sind die Suchlaeufe, die der Runner zum Wiederaufnehmen
    /// braucht: Signal, Rundruf, faellige Timer.
    /// </summary>
    /// <remarks>
    /// Der Test steht hier und nicht im Test-Projekt des Kerns, weil nur hier beide Fassungen erreichbar
    /// sind. Er ist die Antwort auf eine reale Divergenz: der In-Memory-Store filterte zusaetzlich ueber
    /// <c>instance.Status == Waiting</c>, der EF-Store rein ueber den Token-Zustand. Eine Instanz mit
    /// einem aktiven UND einem wartenden Zweig (Status <c>Running</c> - der Normalfall bei parallelen
    /// Regionen und bei Nachrichten am Schritt) fand deshalb nur der EF-Store. Ein gruener
    /// In-Memory-Test bewies fuer diesen Weg nichts.
    /// <para>
    /// Deswegen die Instanzen von Hand statt ueber die Engine: nur so entsteht genau die Verteilung von
    /// Instanz-Status und Token-Zustaenden, um die es geht.
    /// </para></remarks>
    [TestClass]
    public class WorkflowStoreContractTest
    {
        private const string Memory = "memory";
        private const string Ef = "ef";

        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;

        [TestInitialize]
        public void Setup()
        {
            // In-Memory-SQLite lebt nur, solange die Verbindung offen ist (siehe EfWorkflowStoreTest).
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using var ctx = new WorkflowContext(options);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            connection?.Dispose();
        }

        private IWorkflowStore NewStore(string kind)
            => kind == Ef
                ? new EfWorkflowStore(() => new WorkflowContext(options))
                : new InMemoryWorkflowStore();

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void SignalLookup_FindsTheWaitingBranchOfARunningInstance(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowInstance instance = SaveInstance(store, WorkflowStatus.Running,
                Active("a"),
                WaitingForSignal("w", "approve", WaitKind.Message));

            List<WorkflowInstance> found = store.FindWaitingForSignal("approve").ToList();

            Assert.AreEqual(1, found.Count,
                $"[{kind}] the branch waits for 'approve' - that the sibling branch is still running does "
                + "not make the message undeliverable. Whether it arrives is decided by the wait point, "
                + "not by the instance status.");
            Assert.AreEqual(instance.Id, found[0].Id, $"[{kind}] wrong instance.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void BroadcastLookup_FindsTheWaitingBranchOfARunningInstance(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowInstance instance = SaveInstance(store, WorkflowStatus.Running,
                Active("a"),
                WaitingForSignal("w", "day-closed", WaitKind.Signal));

            List<WorkflowInstance> found = store.FindWaitingForBroadcast("day-closed").ToList();

            Assert.AreEqual(1, found.Count,
                $"[{kind}] a broadcast reaches every wait point of that name - a running sibling branch is "
                + "none of its business.");
            Assert.AreEqual(instance.Id, found[0].Id, $"[{kind}] wrong instance.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void BroadcastLookup_IgnoresDirectedWaitPoints(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            SaveInstance(store, WorkflowStatus.Waiting, WaitingForSignal("w", "day-closed", WaitKind.Message));

            Assert.AreEqual(0, store.FindWaitingForBroadcast("day-closed").Count(),
                $"[{kind}] a directed message wait point is not woken by a broadcast.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void TimerLookup_FindsTheDueTimerOfARunningInstance(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            WorkflowInstance instance = SaveInstance(store, WorkflowStatus.Running,
                Active("a"),
                DueAt("w", now.AddMinutes(-5)));

            List<WorkflowInstance> found = store.FindDueTimers(now).ToList();

            Assert.AreEqual(1, found.Count,
                $"[{kind}] the timer is due on its own branch - the running sibling does not postpone it.");
            Assert.AreEqual(instance.Id, found[0].Id, $"[{kind}] wrong instance.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void TimerLookup_SkipsSuspendedInstances(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            WorkflowInstance instance = SaveInstance(store, WorkflowStatus.Waiting,
                DueAt("w", now.AddMinutes(-5)));
            instance.Suspended = true;
            store.SaveInstance(instance);

            Assert.AreEqual(0, store.FindDueTimers(now).Count(),
                $"[{kind}] a suspended instance keeps its due timers, but nobody may drive it on. This is "
                + "the one instance-level condition that DOES belong in the timer lookup.");
        }

        /// <summary>
        /// Gefaultete Instanzen gehoeren nicht in die <b>gepollten</b> Suchlaeufe. Die Entscheidung selbst
        /// trifft <c>WorkflowEngine.MayResumeOnEvent</c> - hier steht sie, damit der Runner eine
        /// gefaultete Instanz nicht bei jedem Takt aufgreift und wieder abweist. Ein faelliger Timer
        /// bleibt faellig; die Meldung darueber schriebe sich sonst endlos fort.
        /// </summary>
        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void TimerLookup_SkipsFaultedInstances(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            SaveInstance(store, WorkflowStatus.Faulted, DueAt("w", now.AddMinutes(-5)));

            Assert.AreEqual(0, store.FindDueTimers(now).Count(),
                $"[{kind}] a faulted instance keeps its armed timers for the retry, but the poll must not "
                + "offer it up on every cycle.");
            Assert.AreEqual(0, store.ClaimDueTimers(now, "runner", TimeSpan.FromMinutes(1), 10).Count(),
                $"[{kind}] the same goes for the claiming poll - otherwise it burns a maxInstances slot.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void TargetLookup_SkipsFaultedInstances(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            var parked = new Token
            {
                Id = "w",
                NodeId = "n-w",
                Status = TokenStatus.WaitingForTarget,
                WaitingTarget = "backend"
            };
            SaveInstance(store, WorkflowStatus.Faulted, parked);

            Assert.AreEqual(0, store.FindBranchesWaitingForTarget(new[] { "backend" }).Count(),
                $"[{kind}] the target handoff is polled as well - same reason as with the timers.");
        }

        /// <summary>
        /// Die Gegenprobe zu den beiden davor: der <b>Signal</b>-Suchlauf laesst gefaultete Instanzen
        /// bewusst durch. Er wird nicht gepollt, sondern laeuft je eintreffender Nachricht - und dass eine
        /// Nachricht eine gefaultete Instanz nicht erreicht hat, ist eine Meldung, die man haben will.
        /// Abgewiesen wird sie danach in der Engine.
        /// </summary>
        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void SignalLookup_StillFindsFaultedInstances(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            SaveInstance(store, WorkflowStatus.Faulted, WaitingForSignal("w", "approve", WaitKind.Message));

            Assert.AreEqual(1, store.FindWaitingForSignal("approve").Count(),
                $"[{kind}] the store must keep finding the branch - after a retry it has to be reachable, "
                + "and the refusal belongs in the engine, where it can be reported.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void SignalLookup_PrefersTheKeyAtTheWaitPointOverTheOneAtTheInstance(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            Token waiting = WaitingForSignal("w", "approve", WaitKind.Message);
            waiting.WaitingCorrelation = "order-7";
            WorkflowInstance instance = SaveInstance(store, WorkflowStatus.Waiting, waiting);
            instance.CorrelationKey = "instance-key";
            store.SaveInstance(instance);

            Assert.AreEqual(1, store.FindWaitingForSignal("approve", "order-7").Count(),
                $"[{kind}] the key at the wait point is the more specific one and must match.");
            Assert.AreEqual(0, store.FindWaitingForSignal("approve", "instance-key").Count(),
                $"[{kind}] once the wait point carries its own key, the instance key no longer selects it.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void SignalLookup_FallsBackToTheKeyAtTheInstance(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowInstance instance = SaveInstance(store, WorkflowStatus.Waiting,
                WaitingForSignal("w", "approve", WaitKind.Message));
            instance.CorrelationKey = "order-7";
            store.SaveInstance(instance);

            Assert.AreEqual(1, store.FindWaitingForSignal("approve", "order-7").Count(),
                $"[{kind}] without a key at the wait point the instance key decides.");
            Assert.AreEqual(1, store.FindWaitingForSignal("approve", instance.Id).Count(),
                $"[{kind}] the instance id addresses the instance just as well.");
            Assert.AreEqual(0, store.FindWaitingForSignal("approve", "other").Count(),
                $"[{kind}] a foreign key must not select it.");
        }

        private static Token Active(string id)
            => new Token { Id = id, NodeId = "n-" + id, Status = TokenStatus.Active };

        private static Token WaitingForSignal(string id, string signalName, WaitKind kind)
            => new Token
            {
                Id = id,
                NodeId = "n-" + id,
                Status = TokenStatus.Waiting,
                WaitingSignal = signalName,
                WaitingKind = kind
            };

        private static Token DueAt(string id, DateTime dueUtc)
            => new Token { Id = id, NodeId = "n-" + id, Status = TokenStatus.Waiting, DueUtc = dueUtc };

        /// <summary>
        /// Legt eine Definition (falls noetig) und darauf eine Instanz im gewuenschten Zustand ab. Die
        /// Definition ist reine Pflicht: seit die Instanz einen echten Fremdschluessel auf die
        /// Definitionszeile traegt, gibt es sie ohne nicht mehr.
        /// </summary>
        private static WorkflowInstance SaveInstance(IWorkflowStore store, WorkflowStatus status,
            params Token[] tokens)
        {
            WorkflowDefinition definition = store.GetDefinition("d", 1)
                ?? new WorkflowDefinition { TechnicalName = "d", Version = 1, Name = "d" };
            if (definition.Key == 0)
            {
                store.SaveDefinition(definition);
            }

            var instance = new WorkflowInstance
            {
                DefinitionKey = definition.Key,
                DefinitionId = definition.TechnicalName,
                DefinitionVersion = definition.Version,
                Status = status,
                Tokens = tokens.ToList()
            };
            store.SaveInstance(instance);
            return instance;
        }
    }
}
