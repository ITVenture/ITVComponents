using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft die <b>Zustell-Garantie</b>: eine ausgehende Nachricht wird im selben Commit vorgemerkt wie
    /// der Zweig, der sie ausgeloest hat - und ueberlebt damit einen Absturz zwischen Commit und
    /// Zustellung.
    /// </summary>
    /// <remarks>
    /// Muss gegen den EF-Store laufen: der In-Memory-Store haelt dieselbe Objekt-Referenz und koennte
    /// nicht zeigen, dass die Vormerkung tatsaechlich in der Ablage steht.
    /// </remarks>
    [TestClass]
    public class WorkflowOutboxTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using (var ctx = new WorkflowContext(options))
            {
                ctx.Database.EnsureCreated();
            }

            store = new EfWorkflowStore(() => new WorkflowContext(options));
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        private void SaveReceiver()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "ordering",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode
                    {
                        Id = "await", SignalName = "CustomerPaid", WaitKind = WaitKind.Message,
                        CorrelationExpression = "orderId"
                    },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "await"), F("await", "e") }
            });
        }

        private void SaveSender(bool waitForDelivery)
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "payment",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s2" },
                    new SendMessageNode
                    {
                        Id = "send",
                        SignalName = "CustomerPaid",
                        WaitKind = WaitKind.Message,
                        CorrelationExpression = "orderId",
                        WaitForDelivery = waitForDelivery,
                        ReachedVariable = "reached"
                    },
                    new EndNode { Id = "e2" }
                },
                Flows = new List<SequenceFlow> { F("s2", "send"), F("send", "e2") }
            });
        }

        private WorkflowEngine Engine() => new WorkflowEngine(store, new ActivityRegistry());

        private static Dictionary<string, object> Order(string id = "4711")
            => new Dictionary<string, object> { { "orderId", id } };

        private int OutboxRows()
        {
            using var ctx = new WorkflowContext(options);
            return ctx.Outbox.Count();
        }

        [TestMethod]
        public void AfterDelivery_NothingStaysQueued()
        {
            SaveReceiver();
            SaveSender(waitForDelivery: false);
            WorkflowEngine engine = Engine();

            WorkflowInstance order = engine.StartWorkflow("ordering", Order());
            engine.StartWorkflow("payment", Order());

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(order.Id).Status);
            Assert.AreEqual(0, OutboxRows(),
                "the sender delivers right after its commit and clears its own note.");
        }

        [TestMethod]
        public void ADeliveryThatFails_StaysQueuedAndIsCaughtUpLater()
        {
            SaveReceiver();
            SaveSender(waitForDelivery: false);

            // Der Empfaenger wartet noch NICHT - die Nachricht erreicht niemanden, aber der Sender ist
            // durch. Danach kommt der Empfaenger und der Nachhol-Lauf holt es nach: genau die Reihenfolge,
            // die ohne Vormerkung die Nachricht verloren haette.
            WorkflowEngine engine = Engine();
            engine.StartWorkflow("payment", Order());
            Assert.AreEqual(0, OutboxRows(), "nobody waited - but the note is cleared, it WAS delivered.");

            // Jetzt der harte Fall: eine Vormerkung, die nie zugestellt wurde (abgestuerzter Prozess).
            WorkflowInstance order = engine.StartWorkflow("ordering", Order());
            using (var ctx = new WorkflowContext(options))
            {
                ctx.Outbox.Add(new WorkflowOutboxRow
                {
                    InstanceId = order.Id,
                    Id = "left-behind",
                    SignalName = "CustomerPaid",
                    CorrelationKey = "4711",
                    CreatedUtc = DateTime.UtcNow
                });
                ctx.SaveChanges();
            }

            int delivered = engine.DeliverPendingMessages("runner-A", TimeSpan.FromMinutes(1));

            Assert.AreEqual(1, delivered);
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(order.Id).Status,
                "the catch-up run is what makes the guarantee real - without it the message is lost.");
            Assert.AreEqual(0, OutboxRows());
        }

        [TestMethod]
        public void AClaimedMessageIsInvisibleToAnotherRunner()
        {
            SaveReceiver();
            WorkflowInstance order = Engine().StartWorkflow("ordering", Order());
            using (var ctx = new WorkflowContext(options))
            {
                ctx.Outbox.Add(new WorkflowOutboxRow
                {
                    InstanceId = order.Id, Id = "m1", SignalName = "X", CreatedUtc = DateTime.UtcNow
                });
                ctx.SaveChanges();
            }

            IReadOnlyList<OutgoingMessage> first =
                store.ClaimOutgoingMessages("runner-A", TimeSpan.FromMinutes(5), 10);
            IReadOnlyList<OutgoingMessage> second =
                store.ClaimOutgoingMessages("runner-B", TimeSpan.FromMinutes(5), 10);

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(0, second.Count,
                "two runners must not catch up on the same message at the same time.");
        }

        [TestMethod]
        public void AnExpiredClaimIsTakenOverByAnotherRunner()
        {
            SaveReceiver();
            WorkflowInstance order = Engine().StartWorkflow("ordering", Order());
            using (var ctx = new WorkflowContext(options))
            {
                ctx.Outbox.Add(new WorkflowOutboxRow
                {
                    InstanceId = order.Id, Id = "m1", SignalName = "X", CreatedUtc = DateTime.UtcNow,
                    ClaimedBy = "dead-runner", ClaimedUntil = DateTime.UtcNow.AddMinutes(-1)
                });
                ctx.SaveChanges();
            }

            IReadOnlyList<OutgoingMessage> claimed =
                store.ClaimOutgoingMessages("runner-B", TimeSpan.FromMinutes(5), 10);

            Assert.AreEqual(1, claimed.Count,
                "a crashed runner holds nothing - its expired claim must not block the message forever.");
        }

        [TestMethod]
        public void WaitingForDelivery_BringsTheNumberOfReceiversBack()
        {
            SaveReceiver();
            SaveSender(waitForDelivery: true);
            WorkflowEngine engine = Engine();

            engine.StartWorkflow("ordering", Order());
            engine.StartWorkflow("ordering", Order());   // zwei warten auf denselben Schluessel
            WorkflowInstance sender = engine.StartWorkflow("payment", Order());

            WorkflowInstance final = store.GetInstance(sender.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status,
                "the sender parks, gets committed, the message goes out - and then it carries on.");
            Assert.AreEqual(2, final.Variables["reached"],
                "that is the whole point of waiting: the count is only known after the delivery.");
            Assert.AreEqual(0, OutboxRows());
        }

        [TestMethod]
        public void WaitingForDelivery_NobodyThere_IsVisibleInTheProcess()
        {
            SaveSender(waitForDelivery: true);
            WorkflowEngine engine = Engine();

            WorkflowInstance sender = engine.StartWorkflow("payment", Order());

            WorkflowInstance final = store.GetInstance(sender.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(0, final.Variables["reached"],
                "„nobody was waiting\" is now a value in the process, not just a line in the log - that is "
                + "what waiting buys.");
        }
    }
}
