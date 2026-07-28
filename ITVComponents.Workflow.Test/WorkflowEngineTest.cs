using System;
using System.Collections.Generic;
using System.Linq;
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
        public void ParallelSplitAndJoinRunsBothBranches()
        {
            activities.Register("setX", ctx => ctx.Variables["x"] = 1);
            activities.Register("setY", ctx => ctx.Variables["y"] = 1);
            activities.Register("after", ctx =>
                ctx.Variables["ran"] = (ctx.Variables.TryGetValue("ran", out object r) ? (int)r : 0) + 1);

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "par",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "setX" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "setY" },
                    new ParallelGatewayNode { Id = "join" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "after" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "split"),
                    Flow("split", "a"), Flow("split", "b"),
                    Flow("a", "join"), Flow("b", "join"),
                    Flow("join", "after"), Flow("after", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("par");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(1, instance.Variables["x"]);
            Assert.AreEqual(1, instance.Variables["y"]);
            Assert.AreEqual(1, instance.Variables["ran"], "The join must fire exactly once, not per branch.");
        }

        [TestMethod]
        public void ParallelJoinWaitsForWaitingBranch()
        {
            activities.Register("setX", ctx => ctx.Variables["x"] = 1);

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "parwait",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "setX" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new ParallelGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "split"),
                    Flow("split", "a"), Flow("split", "w"),
                    Flow("a", "join"), Flow("w", "join"),
                    Flow("join", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("parwait");

            // Ein Zweig ist am Join geparkt, der andere wartet auf das Signal -> Instanz ruht.
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);

            // Solange die parallele Region offen ist, steht das Ergebnis des fertigen Zweigs in DESSEN
            // Zweig-Scope - der Instanz-Scope bleibt auf dem Stand des Splits.
            Assert.IsFalse(instance.Variables.ContainsKey("x"),
                "a branch writes into its own scope, not into the instance scope.");
            Token parked = instance.Tokens.Single(t => t.Status == TokenStatus.Joining);
            Assert.AreEqual(1, parked.Variables["x"]);

            engine.SignalWorkflow(instance.Id, "go");

            WorkflowInstance done = store.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Completed, done.Status);
            Assert.AreEqual(1, done.Variables["x"], "the join merges the branch results into the scope.");
        }

        [TestMethod]
        public void ParallelBranchDivertedFromJoinDeadlocks()
        {
            activities.Register("a", ctx => { });

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "deadlock",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "a" },
                    new ExclusiveGatewayNode { Id = "xor", DefaultFlowId = "xor->e2" },
                    new ParallelGatewayNode { Id = "join" },
                    new EndNode { Id = "e1" },
                    new EndNode { Id = "e2" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "split"),
                    Flow("split", "a"), Flow("split", "xor"),
                    Flow("a", "join"),
                    // Der zweite Zweig biegt am XOR weg vom Join ab: der Join wartet ewig auf ihn.
                    FlowIf("xor", "join", "false"),
                    Flow("xor", "e2"),
                    Flow("join", "e1")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("deadlock");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
            StringAssert.Contains(instance.FaultMessage, "Deadlock");
        }

        [TestMethod]
        public void CombinedJoinAndSplitForksAgain()
        {
            foreach (string name in new[] { "a", "b", "c", "d" })
            {
                string captured = name;
                activities.Register(captured, ctx => ctx.Variables[captured] = true);
            }

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "combined",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split1" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "a" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "b" },
                    new ParallelGatewayNode { Id = "gw" }, // 2 in, 2 out: joint UND splittet erneut
                    new AutomatedActivityNode { Id = "c", ActivityRef = "c" },
                    new AutomatedActivityNode { Id = "d", ActivityRef = "d" },
                    new ParallelGatewayNode { Id = "join2" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "split1"),
                    Flow("split1", "a"), Flow("split1", "b"),
                    Flow("a", "gw"), Flow("b", "gw"),
                    Flow("gw", "c"), Flow("gw", "d"),
                    Flow("c", "join2"), Flow("d", "join2"),
                    Flow("join2", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("combined");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            foreach (string name in new[] { "a", "b", "c", "d" })
            {
                Assert.AreEqual(true, instance.Variables[name], $"Activity '{name}' should have run.");
            }
        }

        [TestMethod]
        public void CancelWorkflowConsumesTokensAndSetsCancelled()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "cancel",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "w"), Flow("w", "e") }
            });

            WorkflowInstance instance = engine.StartWorkflow("cancel");
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);

            Assert.IsTrue(engine.CancelWorkflow(instance.Id));

            WorkflowInstance reloaded = store.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Cancelled, reloaded.Status);
            Assert.IsTrue(reloaded.Tokens.All(t => t.Status == TokenStatus.Consumed));

            // Ein bereits abgebrochener Workflow laesst sich nicht erneut abbrechen.
            Assert.IsFalse(engine.CancelWorkflow(instance.Id));
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
