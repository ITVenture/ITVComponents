using System;
using System.Collections.Generic;
using ITVComponents.Workflow;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die Workflow-Engine (Phase 0/1): sequenzielle Ausfuehrung, automatische Schritte,
    /// exklusive Gateways mit CScript-Bedingungen, Wartepunkte (Signal/Timer), Fehlerbehandlung und
    /// das Anhalten/Fortsetzen ueber den Store.
    /// </summary>
    [TestClass]
    public class WorkflowEngineTest
    {
        private InMemoryWorkflowStore store;
        private ActivityRegistry activities;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            activities = new ActivityRegistry();
            // Standard-Auswerter ist CScript - die Gateway-Tests pruefen die echte Integration.
            engine = new WorkflowEngine(store, activities);
        }

        [TestMethod]
        public void SequentialActivitiesRunToCompletion()
        {
            activities.Register("setA", ctx => ctx.Variables["a"] = 2);
            activities.Register("mulB", ctx => ctx.Variables["result"] = (int)ctx.Variables["a"] * 3);

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "seq",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "n1", ActivityRef = "setA" },
                    new AutomatedActivityNode { Id = "n2", ActivityRef = "mulB" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "n1"), Flow("n1", "n2"), Flow("n2", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("seq");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(6, instance.Variables["result"]);
        }

        [TestMethod]
        public void ExclusiveGatewayRoutesByCScriptCondition()
        {
            activities.Register("big", ctx => ctx.Variables["route"] = "big");
            activities.Register("small", ctx => ctx.Variables["route"] = "small");
            store.SaveDefinition(GatewayDefinition());

            WorkflowInstance high = engine.StartWorkflow("gw",
                new Dictionary<string, object> { { "amount", 150 } });
            Assert.AreEqual(WorkflowStatus.Completed, high.Status);
            Assert.AreEqual("big", high.Variables["route"]);

            WorkflowInstance low = engine.StartWorkflow("gw",
                new Dictionary<string, object> { { "amount", 50 } });
            Assert.AreEqual(WorkflowStatus.Completed, low.Status);
            Assert.AreEqual("small", low.Variables["route"], "Below the threshold the default flow must win.");
        }

        [TestMethod]
        public void WaitNodeSuspendsAndSignalResumes()
        {
            activities.Register("finish", ctx => ctx.Variables["done"] = true);
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wait",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "approve" },
                    new AutomatedActivityNode { Id = "f", ActivityRef = "finish" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "w"), Flow("w", "f"), Flow("f", "e") }
            });

            WorkflowInstance instance = engine.StartWorkflow("wait");

            // Der Workflow haelt am Wartepunkt: Status Waiting, ein Token wartet auf das Signal.
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);
            Assert.IsFalse(instance.Variables.ContainsKey("done"));

            // Fortsetzen ueber den Store (wie nach einem Neuladen): per Instanz-Id signalisieren.
            bool delivered = engine.SignalWorkflow(instance.Id, "approve");

            Assert.IsTrue(delivered);
            WorkflowInstance reloaded = store.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Completed, reloaded.Status);
            Assert.AreEqual(true, reloaded.Variables["done"]);
        }

        [TestMethod]
        public void SignalDeliveredByCorrelationKey()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "corr",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "w"), Flow("w", "e") }
            });

            engine.StartWorkflow("corr", correlationKey: "order-42");

            int resumed = engine.DeliverSignal("go", "order-42");

            Assert.AreEqual(1, resumed);
        }

        [TestMethod]
        public void TimerSuspendsUntilDue()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "timer",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    // TimeSpan-Ausdruck: 1 Stunde ab Betreten. CScript liefert den TimeSpan.
                    new TimerNode { Id = "t", DueExpression = "'System.TimeSpan'.FromHours(1)" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "t"), Flow("t", "e") }
            });

            WorkflowInstance instance = engine.StartWorkflow("timer");
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);

            // Noch nicht faellig: nichts passiert.
            Assert.IsFalse(engine.TriggerTimers(instance, DateTime.UtcNow));
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(instance.Id).Status);

            // Nach der Faelligkeit laeuft er zu Ende.
            engine.TriggerDueTimers(DateTime.UtcNow.AddHours(2));
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(instance.Id).Status);
        }

        [TestMethod]
        public void FailingActivityFaultsTheInstance()
        {
            activities.Register("boom", ctx => throw new InvalidOperationException("kaputt"));
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "fault",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "boom" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "b"), Flow("b", "e") }
            });

            WorkflowInstance instance = engine.StartWorkflow("fault");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
            StringAssert.Contains(instance.FaultMessage, "boom");
        }

        [TestMethod]
        public void GatewayWithoutMatchAndNoDefaultFaults()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "nomatch",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ExclusiveGatewayNode { Id = "g" },
                    new EndNode { Id = "e1" },
                    new EndNode { Id = "e2" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "g"),
                    FlowIf("g", "e1", "false"),
                    FlowIf("g", "e2", "false")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("nomatch");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
        }

        [TestMethod]
        public void ParallelGatewayIsRejectedUntilPhase2()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "par",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "p" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "p"), Flow("p", "e") }
            });

            WorkflowInstance instance = engine.StartWorkflow("par");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
            StringAssert.Contains(instance.FaultMessage, "phase 2");
        }

        private static WorkflowDefinition GatewayDefinition()
        {
            return new WorkflowDefinition
            {
                Id = "gw",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ExclusiveGatewayNode { Id = "g", DefaultFlowId = "toSmall" },
                    new AutomatedActivityNode { Id = "big", ActivityRef = "big" },
                    new AutomatedActivityNode { Id = "small", ActivityRef = "small" },
                    new EndNode { Id = "e1" },
                    new EndNode { Id = "e2" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "g"),
                    FlowIf("g", "big", "amount > 100"),
                    new SequenceFlow { Id = "toSmall", SourceId = "g", TargetId = "small" },
                    Flow("big", "e1"),
                    Flow("small", "e2")
                }
            };
        }

        private static SequenceFlow Flow(string from, string to)
        {
            return new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };
        }

        private static SequenceFlow FlowIf(string from, string to, string condition)
        {
            return new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to, Condition = condition };
        }
    }
}
