using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using ITVComponents.Workflow.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft den <see cref="SendMessageNode"/> - das Gegenstueck zum Wartepunkt - und vor allem, dass
    /// <b>nach</b> dem Commit des sendenden Zweigs zugestellt wird.
    /// </summary>
    [TestClass]
    public class WorkflowSendMessageTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>Der Empfaenger: wartet auf „CustomerPaid" mit Korrelation aus seinen Variablen.</summary>
        private void SaveReceiver()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "ordering",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode
                    {
                        Id = "await",
                        SignalName = "CustomerPaid",
                        WaitKind = WaitKind.Message,
                        CorrelationExpression = "orderId + \"_\" + customerId"
                    },
                    new AutomatedActivityNode { Id = "book", ActivityRef = "work" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "await"), F("await", "book"), F("book", "e") }
            });
        }

        /// <summary>Der Sender: schickt die Nachricht mit demselben Schluessel.</summary>
        private void SaveSender(string correlation = "orderId + \"_\" + customerId")
        {
            var send = new SendMessageNode
            {
                Id = "send",
                SignalName = "CustomerPaid",
                WaitKind = WaitKind.Message,
                CorrelationExpression = correlation
            };
            send.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "paymentId", Kind = ParameterBindingKind.Variable, Source = "paymentId"
            });

            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "payment",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s2" },
                    send,
                    new EndNode { Id = "e2" }
                },
                Flows = new List<SequenceFlow> { F("s2", "send"), F("send", "e2") }
            });
        }

        private WorkflowEngine Engine(List<string> ran = null)
            => new WorkflowEngine(store, new ActivityRegistry().Register("work",
                ctx => (ran ?? new List<string>()).Add(ctx.Node.Id)));

        private static Dictionary<string, object> Order(string order = "4711", string customer = "C1")
            => new Dictionary<string, object> { { "orderId", order }, { "customerId", customer } };

        [TestMethod]
        public void TheMessageReachesTheMatchingInstance()
        {
            SaveReceiver();
            SaveSender();
            var ran = new List<string>();
            WorkflowEngine engine = Engine(ran);

            WorkflowInstance order = engine.StartWorkflow("ordering", Order());
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(order.Id).Status);

            Dictionary<string, object> payment = Order();
            payment["paymentId"] = "P-9";
            engine.StartWorkflow("payment", payment);

            WorkflowInstance final = store.GetInstance(order.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            CollectionAssert.Contains(ran, "book");
            Assert.AreEqual("P-9", final.Variables["paymentId"],
                "the payload lands in the variables of the receiving branch.");
        }

        [TestMethod]
        public void AnotherOrderIsNotWokenUp()
        {
            SaveReceiver();
            SaveSender();
            WorkflowEngine engine = Engine();

            WorkflowInstance mine = engine.StartWorkflow("ordering", Order("4711"));
            WorkflowInstance other = engine.StartWorkflow("ordering", Order("4712"));

            engine.StartWorkflow("payment", Order("4711"));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(mine.Id).Status);
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(other.Id).Status,
                "a message is directed - the correlation is what keeps two cases of the same shape apart.");
        }

        [TestMethod]
        public void TheSenderIsCommittedBeforeTheReceiverRuns()
        {
            SaveReceiver();
            SaveSender();

            // Die Aktivitaet des EMPFAENGERS schaut nach, wie der Sender in der Ablage steht. Das ist der
            // Kern der Sache: laeuft der Empfaenger zu frueh, ist der Sender dort noch nicht fertig.
            WorkflowStatus senderStatusSeenByReceiver = WorkflowStatus.Running;
            string senderId = null;
            var activities = new ActivityRegistry().Register("work",
                ctx => senderStatusSeenByReceiver = store.GetInstance(senderId).Status);
            var engine = new WorkflowEngine(store, activities);

            engine.StartWorkflow("ordering", Order());
            Dictionary<string, object> payment = Order();
            payment["paymentId"] = "P-1";

            WorkflowInstance sender = engine.StartWorkflow("payment", payment);
            senderId = sender.Id;

            // Erneut senden, jetzt mit bekannter Sender-Id.
            engine.StartWorkflow("ordering", Order("4713"));
            WorkflowInstance second = engine.StartWorkflow("payment",
                new Dictionary<string, object>
                {
                    { "orderId", "4713" }, { "customerId", "C1" }, { "paymentId", "P-2" }
                });
            senderId = second.Id;

            Assert.AreEqual(WorkflowStatus.Completed, senderStatusSeenByReceiver,
                "delivered after the commit - the receiver must never run on a state of the sender that " +
                "is not in the store yet.");
        }

        [TestMethod]
        public void SendingFromAnActivityIsAlsoDeferred()
        {
            SaveReceiver();
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "manual",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s3" },
                    new AutomatedActivityNode { Id = "notify", ActivityRef = "notify" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "mark" },
                    new EndNode { Id = "e3" }
                },
                Flows = new List<SequenceFlow> { F("s3", "notify"), F("notify", "after"), F("after", "e3") }
            });

            var order = new List<string>();
            WorkflowEngine engine = null;
            var activities = new ActivityRegistry()
                .Register("notify", ctx =>
                {
                    order.Add("send");
                    // Aus der Aktivitaet heraus - auch das wird gepuffert, bis der Zweig durch ist.
                    engine.DeliverSignal("CustomerPaid", "4711_C1");
                    order.Add("after-send");
                })
                .Register("mark", ctx => order.Add("sender-continues"))
                .Register("work", ctx => order.Add("receiver-runs"));
            engine = new WorkflowEngine(store, activities);

            engine.StartWorkflow("ordering", Order());
            engine.StartWorkflow("manual");

            CollectionAssert.AreEqual(
                new[] { "send", "after-send", "sender-continues", "receiver-runs" }, order,
                "the sending activity returns, the sending branch finishes - and only then does the " +
                "receiver run. Delivered inline it would have run between 'send' and 'after-send'.");
        }

        [TestMethod]
        public void ABroadcastReachesEveryWaitingInstance()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "listener",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "SystemPaused", WaitKind = WaitKind.Signal },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "w"), F("w", "e") }
            });
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "pauser",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s2" },
                    new SendMessageNode { Id = "send", SignalName = "SystemPaused", WaitKind = WaitKind.Signal },
                    new EndNode { Id = "e2" }
                },
                Flows = new List<SequenceFlow> { F("s2", "send"), F("send", "e2") }
            });
            WorkflowEngine engine = Engine();

            WorkflowInstance a = engine.StartWorkflow("listener");
            WorkflowInstance b = engine.StartWorkflow("listener");

            engine.StartWorkflow("pauser");

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(a.Id).Status);
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(b.Id).Status);
        }

        [TestMethod]
        public void NobodyWaiting_IsNotAFailureOfTheSender()
        {
            SaveSender();
            WorkflowEngine engine = Engine();

            WorkflowInstance sender = engine.StartWorkflow("payment", Order());

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(sender.Id).Status,
                "a message nobody waits for is a normal case - the sender has done its part. It is " +
                "reported in the system log, because after the commit it can no longer flow back.");
        }

        [TestMethod]
        public void AFailingCorrelation_FaultsTheSender()
        {
            SaveSender(correlation: "orderId.ThisDoesNotExist()");
            WorkflowEngine engine = Engine();

            WorkflowInstance sender = engine.StartWorkflow("payment", Order());

            WorkflowInstance final = store.GetInstance(sender.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status,
                "a message whose key cannot be computed would arrive nowhere - and that would only be " +
                "noticed when the other side stays silent.");
        }

        // --- Validierung ---------------------------------------------------------------------

        private static WorkflowDefinition WithSend(SendMessageNode send)
        {
            return new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" }, send, new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", send.Id), F(send.Id, "e") }
            };
        }

        [TestMethod]
        public void AMessageWithoutCorrelation_IsRejected()
        {
            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(
                WithSend(new SendMessageNode { Id = "send", SignalName = "X", WaitKind = WaitKind.Message }));

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "send"
                                          && i.Message.Contains("degrade to a broadcast")),
                "silently waking every wait point of that name is the failure mode here.");
        }

        [TestMethod]
        public void ABroadcastWithoutCorrelation_IsFine()
        {
            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(
                WithSend(new SendMessageNode { Id = "send", SignalName = "X", WaitKind = WaitKind.Signal }));

            Assert.IsFalse(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "send"),
                "a broadcast is meant to reach everyone - it needs no key: "
                + string.Join(" | ", issues.Select(i => i.Message)));
        }

        [TestMethod]
        public void ASendWithoutASignalName_IsRejected()
        {
            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(
                WithSend(new SendMessageNode { Id = "send", WaitKind = WaitKind.Signal }));

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "send"
                                          && i.Message.Contains("no signal name")));
        }

        [TestMethod]
        public void ASendDoesNotSplitTheFlow()
        {
            WorkflowDefinition def = WithSend(new SendMessageNode
            {
                Id = "send", SignalName = "X", WaitKind = WaitKind.Signal
            });
            def.Nodes.Add(new AutomatedActivityNode { Id = "extra", ActivityRef = "work" });
            def.Flows.Add(F("send", "extra"));
            def.Flows.Add(F("extra", "e"));

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "send"
                                          && i.Message.Contains("does not split the flow")));
        }
    }
}
