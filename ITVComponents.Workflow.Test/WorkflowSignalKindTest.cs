using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die Trennung <b>gerichtete Nachricht</b> gegen <b>Rundruf</b> und den Korrelations-
    /// schluessel, der erst beim Warten entsteht.
    /// </summary>
    [TestClass]
    public class WorkflowSignalKindTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        private void SaveDefinition(string id, WaitNode wait)
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = id,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" }, wait, new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", wait.Id), F(wait.Id, "e") }
            });
        }

        private WorkflowEngine Engine() => new WorkflowEngine(store, new ActivityRegistry());

        [TestMethod]
        public void AMessageWait_IsNotReachedByAnUncorrelatedDelivery()
        {
            // Die eigentliche Verhaltensaenderung: frueher traf ein schlussselloser Aufruf JEDE wartende
            // Instanz - zwei Vorgaenge desselben Musters weckten einander.
            SaveDefinition("wf", new WaitNode { Id = "w", SignalName = "approved" });
            WorkflowEngine engine = Engine();
            engine.StartWorkflow("wf");
            engine.StartWorkflow("wf");

            int reached = engine.DeliverSignal("approved");

            Assert.AreEqual(0, reached, "a message wait needs a correlation key - it is not a broadcast.");
        }

        [TestMethod]
        public void AMessageWait_IsReachedByItsInstanceKey()
        {
            SaveDefinition("wf", new WaitNode { Id = "w", SignalName = "approved" });
            WorkflowEngine engine = Engine();
            WorkflowInstance mine = engine.StartWorkflow("wf", correlationKey: "order-1");
            WorkflowInstance other = engine.StartWorkflow("wf", correlationKey: "order-2");

            Assert.AreEqual(1, engine.DeliverSignal("approved", "order-1"));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(mine.Id).Status);
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(other.Id).Status,
                "the other order must not move.");
        }

        [TestMethod]
        public void ABroadcastWait_IsReachedByEveryone()
        {
            SaveDefinition("wf", new WaitNode
            {
                Id = "w", SignalName = "daily-close", WaitKind = WaitKind.Signal
            });
            WorkflowEngine engine = Engine();
            WorkflowInstance a = engine.StartWorkflow("wf");
            WorkflowInstance b = engine.StartWorkflow("wf");

            Assert.AreEqual(2, engine.BroadcastSignal("daily-close"));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(a.Id).Status);
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(b.Id).Status);
        }

        [TestMethod]
        public void ABroadcast_DoesNotTouchMessageWaits()
        {
            SaveDefinition("msg", new WaitNode { Id = "w", SignalName = "shared" });
            SaveDefinition("sig", new WaitNode
            {
                Id = "w", SignalName = "shared", WaitKind = WaitKind.Signal
            });
            WorkflowEngine engine = Engine();
            WorkflowInstance targeted = engine.StartWorkflow("msg");
            WorkflowInstance broadcast = engine.StartWorkflow("sig");

            Assert.AreEqual(1, engine.BroadcastSignal("shared"));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(broadcast.Id).Status);
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(targeted.Id).Status,
                "a broadcast must not wake a wait point that expects a directed message.");
        }

        [TestMethod]
        public void TheCorrelationOfTheWaitPoint_IsResolvedWhenParking()
        {
            // Der Punkt der ganzen Uebung: der Schluessel entsteht erst im Prozess (hier aus einer
            // Variablen), nicht beim Anlegen der Instanz.
            SaveDefinition("wf", new WaitNode
            {
                Id = "w", SignalName = "delivered", CorrelationExpression = "orderNo"
            });
            WorkflowEngine engine = Engine();
            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "orderNo", "A-4711" } });

            Token parked = store.GetInstance(inst.Id).Tokens.Single(t => t.Status == TokenStatus.Waiting);
            Assert.AreEqual("A-4711", parked.WaitingCorrelation);

            Assert.AreEqual(1, engine.DeliverSignal("delivered", "A-4711"));
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
        }

        [TestMethod]
        public void TheWaitPointKeyBeatsTheInstanceKey()
        {
            SaveDefinition("wf", new WaitNode
            {
                Id = "w", SignalName = "delivered", CorrelationExpression = "orderNo"
            });
            WorkflowEngine engine = Engine();
            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "orderNo", "A-4711" } }, correlationKey: "case-1");

            Assert.AreEqual(0, engine.DeliverSignal("delivered", "case-1"),
                "once the wait point declares its own key, the instance key no longer addresses it - " +
                "otherwise two wait points of the same instance could not be told apart.");
            Assert.AreEqual(1, engine.DeliverSignal("delivered", "A-4711"));
        }

        [TestMethod]
        public void ABrokenCorrelationExpression_Faults()
        {
            SaveDefinition("wf", new WaitNode
            {
                Id = "w", SignalName = "delivered", CorrelationExpression = "this is not valid ***"
            });

            WorkflowInstance inst = Engine().StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status,
                "a wait point whose key cannot be computed would never be reachable - better loud than silent.");
        }

        [TestMethod]
        public void ATargetedDeliveryWithoutKey_StillReachesItsInstance()
        {
            // Die Instanz ist bereits benannt - DAS ist die Adressierung. Der Schluessel wird erst
            // gebraucht, wenn dieselbe Instanz an mehreren Stellen auf denselben Namen wartet.
            SaveDefinition("wf", new WaitNode { Id = "w", SignalName = "approved" });
            WorkflowEngine engine = Engine();
            WorkflowInstance inst = engine.StartWorkflow("wf");

            Assert.IsTrue(engine.SignalWorkflow(inst.Id, "approved"));
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
        }
    }
}
