using System;
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
    /// Prueft die <b>Signatur</b> einer Definition: die Start-Parameter (<see cref="StartNode.Inputs"/>,
    /// aufgeloest gegen die uebergebenen Startwerte) und das Ergebnis
    /// (<see cref="EndNode.Outputs"/>, das den Variablen-Stack beim Abschluss auf genau dieses Ergebnis
    /// zurueckstellt). Beides ist rein additiv: ohne Deklaration verhaelt sich alles wie bisher.
    /// </summary>
    [TestClass]
    public class WorkflowSignatureTest
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

        // --- Start-Parameter (6a) ------------------------------------------------------------------

        [TestMethod]
        public void StartParameter_Literal_IsAppliedAsDefault()
        {
            store.SaveDefinition(Passthrough("lit", start =>
                start.Inputs.Add(Input("mode", ParameterBindingKind.Literal, literal: "fast"))));

            WorkflowInstance instance = engine.StartWorkflow("lit");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual("fast", instance.Variables["mode"]);
        }

        [TestMethod]
        public void StartParameter_Expression_IsEvaluatedOverPassedValues()
        {
            store.SaveDefinition(Passthrough("expr", start =>
                start.Inputs.Add(Input("sum", ParameterBindingKind.Expression, source: "a + b"))));

            WorkflowInstance instance = engine.StartWorkflow("expr",
                new Dictionary<string, object> { { "a", 3 }, { "b", 4 } });

            Assert.AreEqual(7, instance.Variables["sum"]);
        }

        [TestMethod]
        public void StartParameters_Extend_KeepUndeclaredPassedValues()
        {
            store.SaveDefinition(Passthrough("ext", start =>
                start.Inputs.Add(Input("mode", ParameterBindingKind.Literal, literal: "fast"))));

            WorkflowInstance instance = engine.StartWorkflow("ext",
                new Dictionary<string, object> { { "caller", "MLM" } });

            Assert.AreEqual("fast", instance.Variables["mode"]);
            Assert.AreEqual("MLM", instance.Variables["caller"],
                "Extend is the default - undeclared values passed by the caller must survive.");
        }

        [TestMethod]
        public void StartParameters_Replace_MakeTheSignatureStrict()
        {
            store.SaveDefinition(Passthrough("strict", start =>
            {
                start.ScopeMode = ActivityScopeMode.Replace;
                start.RetainVariables.Add("corr");
                start.Inputs.Add(Input("n", ParameterBindingKind.Variable, source: "input"));
            }));

            WorkflowInstance instance = engine.StartWorkflow("strict",
                new Dictionary<string, object> { { "input", 5 }, { "corr", "K-1" }, { "junk", "x" } });

            Assert.AreEqual(5, instance.Variables["n"], "the declared parameter is bound from the passed value.");
            Assert.AreEqual("K-1", instance.Variables["corr"], "the retained value survives the strict signature.");
            Assert.IsFalse(instance.Variables.ContainsKey("junk"), "undeclared values are dropped.");
            Assert.IsFalse(instance.Variables.ContainsKey("input"),
                "the source name of a rename is not part of the signature either.");
        }

        [TestMethod]
        public void NoStartParameters_BehaveExactlyAsBefore()
        {
            store.SaveDefinition(Passthrough("plain", _ => { }));

            WorkflowInstance instance = engine.StartWorkflow("plain",
                new Dictionary<string, object> { { "a", 1 }, { "b", 2 } });

            CollectionAssert.AreEquivalent(new[] { "a", "b" }, instance.Variables.Keys.ToList());
        }

        [TestMethod]
        public void StartParameter_BadExpression_PreventsTheInstance()
        {
            store.SaveDefinition(Passthrough("bad", start =>
                start.Inputs.Add(Input("x", ParameterBindingKind.Expression, source: "?!("))));

            Assert.ThrowsExactly<InvalidOperationException>(() => engine.StartWorkflow("bad"),
                "a workflow whose parameters cannot be resolved must not start at all.");
            Assert.AreEqual(0, store.FindRunnable().Count(),
                "no half-started instance may be left behind.");
        }

        // --- Ergebnis (6b) -------------------------------------------------------------------------

        [TestMethod]
        public void EndOutputs_RebaseTheScopeToTheResult()
        {
            activities.Register("work", ctx =>
            {
                ctx.Outputs["value"] = 42;
            });

            WorkflowDefinition def = OneActivity("res", "work",
                node => node.Outputs.Add(new ActivityOutputBinding { Parameter = "value", Variable = "total" }));
            ((EndNode)def.GetNode("e")).Outputs.Add(
                new ActivityOutputBinding { Parameter = "total", Variable = "result" });
            store.SaveDefinition(def);

            WorkflowInstance instance = engine.StartWorkflow("res",
                new Dictionary<string, object> { { "scratch", "big intermediate state" } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            CollectionAssert.AreEquivalent(new[] { "result" }, instance.Variables.Keys.ToList(),
                "after the end node the scope IS the declared result.");
            Assert.AreEqual(42, instance.Variables["result"]);
        }

        [TestMethod]
        public void EndOutputs_RetainVariables_SurviveTheRebase()
        {
            activities.Register("work", ctx => ctx.Outputs["value"] = 1);

            WorkflowDefinition def = OneActivity("keep", "work",
                node => node.Outputs.Add(new ActivityOutputBinding { Parameter = "value", Variable = "total" }));
            var end = (EndNode)def.GetNode("e");
            end.Outputs.Add(new ActivityOutputBinding { Parameter = "total", Variable = "result" });
            end.RetainVariables.Add("corr");
            store.SaveDefinition(def);

            WorkflowInstance instance = engine.StartWorkflow("keep",
                new Dictionary<string, object> { { "corr", "K-1" }, { "scratch", 9 } });

            CollectionAssert.AreEquivalent(new[] { "result", "corr" }, instance.Variables.Keys.ToList());
        }

        [TestMethod]
        public void NoEndOutputs_KeepTheWholeStack()
        {
            activities.Register("work", ctx => ctx.Outputs["value"] = 1);

            store.SaveDefinition(OneActivity("all", "work",
                node => node.Outputs.Add(new ActivityOutputBinding { Parameter = "value", Variable = "total" })));

            WorkflowInstance instance = engine.StartWorkflow("all",
                new Dictionary<string, object> { { "scratch", 9 } });

            CollectionAssert.AreEquivalent(new[] { "scratch", "total" }, instance.Variables.Keys.ToList(),
                "without a declared result the whole stack stays the result (previous behaviour).");
        }

        [TestMethod]
        public void EndOutputs_AreTheResultTheCallerSees()
        {
            // Der Kern von 6b: weil das Kind seinen Stack auf das Ergebnis zurueckstellt, bleibt die
            // Aufruferseite (ApplyCallOutputs ueber child.Variables) unveraendert - keine zweite Ablage.
            activities.Register("work", ctx => ctx.Outputs["value"] = 21);

            WorkflowDefinition sub = OneActivity("sub", "work",
                node => node.Outputs.Add(new ActivityOutputBinding { Parameter = "value", Variable = "total" }));
            var subEnd = (EndNode)sub.GetNode("e");
            subEnd.Outputs.Add(new ActivityOutputBinding { Parameter = "total", Variable = "result" });
            store.SaveDefinition(sub);

            var call = new CallWorkflowNode { Id = "n", SubDefinitionId = "sub" };
            call.Outputs.Add(new ActivityOutputBinding { Parameter = "result", Variable = "answer" });
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "parent",
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, call, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow> { Flow("s", "n"), Flow("n", "e") }
            });

            WorkflowInstance parent = engine.StartWorkflow("parent");
            Assert.AreEqual(WorkflowStatus.Waiting, parent.Status, "the caller parks until the child ends.");

            // Das Kind wird sonst vom Runner vorangetrieben - hier von Hand, danach die Zustellung.
            WorkflowInstance child = store.FindChildInstances(parent.Id).Single();
            engine.Advance(child);
            Assert.AreEqual(WorkflowStatus.Completed, child.Status);
            CollectionAssert.AreEquivalent(new[] { "result" }, child.Variables.Keys.ToList());

            engine.DeliverChildCompletion(child.Id);
            engine.Advance(store.GetInstance(parent.Id));

            WorkflowInstance finished = store.GetInstance(parent.Id);
            Assert.AreEqual(WorkflowStatus.Completed, finished.Status);
            Assert.AreEqual(21, finished.Variables["answer"]);
        }

        [TestMethod]
        public void SubWorkflow_AppliesItsOwnStartSignature()
        {
            // Die Signatur haengt an der DEFINITION, nicht am Starter: der Aufruf-Pfad legt die Kind-Instanz
            // selbst an und muss die Start-Parameter genauso anwenden wie ein direkter Start.
            object seen = null;
            activities.Register("echo", ctx => seen = ctx.Inputs["x"]);

            WorkflowDefinition sub = OneActivity("sub", "echo",
                node => node.Inputs.Add(Input("x", ParameterBindingKind.Variable, source: "doubled")));
            ((StartNode)sub.GetNode("s")).Inputs.Add(
                Input("doubled", ParameterBindingKind.Expression, source: "n * 2"));
            store.SaveDefinition(sub);

            var call = new CallWorkflowNode { Id = "n", SubDefinitionId = "sub" };
            call.Inputs.Add(Input("n", ParameterBindingKind.Literal, literal: 4));
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "parent",
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, call, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow> { Flow("s", "n"), Flow("n", "e") }
            });

            WorkflowInstance parent = engine.StartWorkflow("parent");
            engine.Advance(store.FindChildInstances(parent.Id).Single());

            Assert.AreEqual(8, seen, "the sub-workflow's own start parameters were applied to the passed values.");
        }

        // --- Aufbau-Helfer -------------------------------------------------------------------------

        /// <summary>Start → End, ohne Aktivitaet: zeigt genau den Variablen-Stack, den der Start erzeugt.</summary>
        private static WorkflowDefinition Passthrough(string id, Action<StartNode> configure)
        {
            var start = new StartNode { Id = "s" };
            configure(start);
            return new WorkflowDefinition
            {
                TechnicalName = id,
                Nodes = new List<WorkflowNode> { start, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow> { Flow("s", "e") }
            };
        }

        private static WorkflowDefinition OneActivity(string id, string activityRef,
            Action<AutomatedActivityNode> configure)
        {
            var node = new AutomatedActivityNode { Id = "n", ActivityRef = activityRef };
            configure(node);
            return new WorkflowDefinition
            {
                TechnicalName = id,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    node,
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "n"), Flow("n", "e") }
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
        {
            return new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };
        }
    }
}
