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
    /// Prueft das <b>Mapping auf der Verbindung</b> (<see cref="SequenceFlow.Inputs"/>): wie der
    /// Variablen-Stack aussieht, wenn ein Token ueber diese Kante ankommt. Es ergaenzt das Mapping am
    /// Knoten - die Kante normalisiert (typisch, wenn mehrere Pfade denselben Knoten mit verschiedenen
    /// Variablennamen erreichen), die Aktivitaet zieht daraus ihre Parameter. Rein additiv: ohne
    /// deklariertes Mapping verhaelt sich alles wie bisher.
    /// </summary>
    [TestClass]
    public class WorkflowFlowMappingTest
    {
        private InMemoryWorkflowStore store;
        private ActivityRegistry activities;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            activities = new ActivityRegistry();
            engine = new WorkflowEngine(store, activities);
        }

        [TestMethod]
        public void FlowMapping_IsAppliedWhenTheTokenTakesTheConnection()
        {
            SequenceFlow flow = Flow("s", "e");
            flow.Inputs.Add(Input("greeting", ParameterBindingKind.Literal, literal: "hello"));
            store.SaveDefinition(Passthrough("map", flow));

            WorkflowInstance instance = engine.StartWorkflow("map");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual("hello", instance.Variables["greeting"]);
        }

        [TestMethod]
        public void NoFlowMapping_BehavesExactlyAsBefore()
        {
            store.SaveDefinition(Passthrough("plain", Flow("s", "e")));

            WorkflowInstance instance = engine.StartWorkflow("plain",
                new Dictionary<string, object> { { "a", 1 } });

            CollectionAssert.AreEquivalent(new[] { "a" }, instance.Variables.Keys.ToList());
        }

        [TestMethod]
        public void FlowMapping_NormalisesBranchesOntoTheSameNames()
        {
            // Der eigentliche Zweck: zwei Pfade erreichen denselben Knoten mit unterschiedlich benannten
            // Werten - die Kante bringt beide auf den Namen, den der Zielknoten erwartet.
            object seen = null;
            activities.Register("echo", ctx => seen = ctx.Inputs["value"]);

            var target = new AutomatedActivityNode { Id = "t", ActivityRef = "echo" };
            target.Inputs.Add(Input("value", ParameterBindingKind.Variable, source: "amount"));

            SequenceFlow small = FlowIf("x", "t", "n < 10");
            small.Inputs.Add(Input("amount", ParameterBindingKind.Variable, source: "smallValue"));
            SequenceFlow large = FlowIf("x", "t", "n >= 10");
            large.Inputs.Add(Input("amount", ParameterBindingKind.Variable, source: "largeValue"));

            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "norm",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ExclusiveGatewayNode { Id = "x" },
                    target,
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "x"), small, large, Flow("t", "e") }
            });

            engine.StartWorkflow("norm", new Dictionary<string, object>
            {
                { "n", 50 }, { "smallValue", "S" }, { "largeValue", "L" }
            });

            Assert.AreEqual("L", seen, "the taken connection decided which value arrives as 'amount'.");
        }

        [TestMethod]
        public void FlowMapping_OfTheNotTakenConnection_DoesNotRun()
        {
            // Reihenfolge: erst waehlt die Condition die Kante, dann greift DEREN Mapping.
            activities.Register("noop", _ => { });

            SequenceFlow taken = FlowIf("x", "a", "true");
            taken.Inputs.Add(Input("route", ParameterBindingKind.Literal, literal: "taken"));
            SequenceFlow other = FlowIf("x", "b", "false");
            other.Inputs.Add(Input("route", ParameterBindingKind.Literal, literal: "other"));

            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "pick",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ExclusiveGatewayNode { Id = "x" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "noop" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "noop" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "x"), taken, other, Flow("a", "e"), Flow("b", "e") }
            });

            WorkflowInstance instance = engine.StartWorkflow("pick");

            Assert.AreEqual("taken", instance.Variables["route"]);
        }

        [TestMethod]
        public void FlowMapping_Replace_ConsolidatesTheScope()
        {
            SequenceFlow flow = Flow("s", "e");
            flow.ScopeMode = ActivityScopeMode.Replace;
            flow.RetainVariables.Add("corr");
            flow.Inputs.Add(Input("total", ParameterBindingKind.Expression, source: "a + b"));
            store.SaveDefinition(Passthrough("cons", flow));

            WorkflowInstance instance = engine.StartWorkflow("cons", new Dictionary<string, object>
            {
                { "a", 1 }, { "b", 2 }, { "corr", "K-1" }, { "scratch", "big" }
            });

            CollectionAssert.AreEquivalent(new[] { "total", "corr" }, instance.Variables.Keys.ToList(),
                "after a consolidating connection the scope is the mapping plus the retained variables.");
            Assert.AreEqual(3, instance.Variables["total"]);
        }

        [TestMethod]
        public void FlowMapping_AppliesOnTheBranchesOfAParallelSplit()
        {
            // Der Split spawnt seine Zweige ueber einen anderen Pfad als jede andere Node (SpawnOutgoing
            // statt MoveToken) - das Mapping muss trotzdem gelten, sonst haengt seine Wirkung am Quellknoten.
            activities.Register("noop", _ => { });

            SequenceFlow toA = Flow("split", "a");
            toA.Inputs.Add(Input("branchA", ParameterBindingKind.Literal, literal: 1));
            SequenceFlow toB = Flow("split", "b");
            toB.Inputs.Add(Input("branchB", ParameterBindingKind.Literal, literal: 2));

            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "par",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "noop" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "noop" },
                    new ParallelGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "split"), toA, toB,
                    Flow("a", "join"), Flow("b", "join"), Flow("join", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("par");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(1, instance.Variables["branchA"]);
            Assert.AreEqual(2, instance.Variables["branchB"]);
        }

        [TestMethod]
        public void FlowMapping_BadExpression_FaultsTheInstance()
        {
            SequenceFlow flow = Flow("s", "e");
            flow.Inputs.Add(Input("x", ParameterBindingKind.Expression, source: "?!("));
            store.SaveDefinition(Passthrough("bad", flow));

            WorkflowInstance instance = engine.StartWorkflow("bad");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
            StringAssert.Contains(instance.FaultMessage, flow.Id,
                "the faulting connection must be named - otherwise it is invisible in the monitor.");
        }

        // --- Aufbau-Helfer -------------------------------------------------------------------------

        /// <summary>Start → End ueber genau die uebergebene Kante.</summary>
        private static WorkflowDefinition Passthrough(string id, SequenceFlow flow)
        {
            return new WorkflowDefinition
            {
                TechnicalName = id,
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow> { flow }
            };
        }

        private static ActivityInputBinding Input(string parameter, ParameterBindingKind kind,
            string source = null, object literal = null)
        {
            return new ActivityInputBinding
            {
                Parameter = parameter, Kind = kind, Source = source, Literal = literal
            };
        }

        private static SequenceFlow Flow(string from, string to)
            => new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        private static SequenceFlow FlowIf(string from, string to, string condition)
        {
            SequenceFlow flow = Flow(from, to);
            flow.Condition = condition;
            return flow;
        }
    }
}
