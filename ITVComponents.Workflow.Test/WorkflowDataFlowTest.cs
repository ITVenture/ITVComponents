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
    /// Prueft den Datenfluss (Phase 2): die Engine loest die Eingabe-Bindungen eines
    /// Aktivitaets-Knotens vor der Ausfuehrung auf (Konstante/Variable/Ausdruck) und bildet die
    /// deklarierten Ausgaben danach auf Instanz-Variablen ab.
    /// </summary>
    [TestClass]
    public class WorkflowDataFlowTest
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
        public void LiteralInput_IsPassedThrough()
        {
            object seen = null;
            activities.Register("echo", ctx => seen = ctx.Inputs["x"]);

            store.SaveDefinition(OneActivity("lit", "echo", node =>
                node.Inputs.Add(new ActivityInputBinding
                {
                    Parameter = "x",
                    Kind = ParameterBindingKind.Literal,
                    Literal = 42
                })));

            WorkflowInstance instance = engine.StartWorkflow("lit");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(42, seen);
        }

        [TestMethod]
        public void VariableInput_ResolvesFromInstanceVariables()
        {
            object seen = null;
            activities.Register("echo", ctx => seen = ctx.Inputs["x"]);

            store.SaveDefinition(OneActivity("varin", "echo", node =>
                node.Inputs.Add(new ActivityInputBinding
                {
                    Parameter = "x",
                    Kind = ParameterBindingKind.Variable,
                    Source = "foo"
                })));

            WorkflowInstance instance = engine.StartWorkflow("varin",
                new Dictionary<string, object> { { "foo", "hello" } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual("hello", seen);
        }

        [TestMethod]
        public void VariableInput_MissingVariable_ResolvesToNull_NoFault()
        {
            bool ran = false;
            object seen = "unset";
            activities.Register("echo", ctx => { ran = true; seen = ctx.Inputs["x"]; });

            store.SaveDefinition(OneActivity("missing", "echo", node =>
                node.Inputs.Add(new ActivityInputBinding
                {
                    Parameter = "x",
                    Kind = ParameterBindingKind.Variable,
                    Source = "doesNotExist"
                })));

            WorkflowInstance instance = engine.StartWorkflow("missing");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status,
                "A missing source variable is a normal case (null), not a fault.");
            Assert.IsTrue(ran);
            Assert.IsNull(seen);
        }

        [TestMethod]
        public void ExpressionInput_IsEvaluatedAgainstVariables()
        {
            object seen = null;
            activities.Register("echo", ctx => seen = ctx.Inputs["x"]);

            store.SaveDefinition(OneActivity("expr", "echo", node =>
                node.Inputs.Add(new ActivityInputBinding
                {
                    Parameter = "x",
                    Kind = ParameterBindingKind.Expression,
                    Source = "a + b"
                })));

            WorkflowInstance instance = engine.StartWorkflow("expr",
                new Dictionary<string, object> { { "a", 3 }, { "b", 4 } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(7, seen);
        }

        [TestMethod]
        public void ExpressionInput_BadExpression_FaultsWithDistinctMessage()
        {
            activities.Register("echo", ctx => { });

            store.SaveDefinition(OneActivity("badexpr", "echo", node =>
                node.Inputs.Add(new ActivityInputBinding
                {
                    Parameter = "x",
                    Kind = ParameterBindingKind.Expression,
                    Source = "this is ) not valid ("
                })));

            WorkflowInstance instance = engine.StartWorkflow("badexpr");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
            StringAssert.Contains(instance.FaultMessage, "Input binding");
        }

        [TestMethod]
        public void Output_IsMappedToConfiguredVariable()
        {
            activities.Register("produce", ctx => ctx.Outputs["messageId"] = "MSG-1");

            store.SaveDefinition(OneActivity("out", "produce", node =>
                node.Outputs.Add(new ActivityOutputBinding
                {
                    Parameter = "messageId",
                    Variable = "sentId"
                })));

            WorkflowInstance instance = engine.StartWorkflow("out");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual("MSG-1", instance.Variables["sentId"]);
        }

        [TestMethod]
        public void Output_NotSetByActivity_MapsToNull()
        {
            // Die Aktivitaet setzt den Ausgabeparameter NICHT - die Variable wird trotzdem (mit null)
            // geschrieben: ein bewusstes, beobachtbares Ergebnis.
            activities.Register("silent", ctx => { });

            store.SaveDefinition(OneActivity("nout", "silent", node =>
                node.Outputs.Add(new ActivityOutputBinding
                {
                    Parameter = "messageId",
                    Variable = "sentId"
                })));

            WorkflowInstance instance = engine.StartWorkflow("nout");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.IsTrue(instance.Variables.ContainsKey("sentId"));
            Assert.IsNull(instance.Variables["sentId"]);
        }

        [TestMethod]
        public void OutputThenVariableInput_ChainsBetweenSteps()
        {
            // Szenario a) + b): Schritt x legt seine Ausgabe in Variable 'foo' ab; Schritt y bekommt
            // 'foo' als Eingabe 'bar' - der Datenfluss zwischen zwei Schritten ueber eine Variable.
            activities.Register("stepX", ctx => ctx.Outputs["result"] = 21);
            object barSeen = null;
            activities.Register("stepY", ctx => barSeen = ctx.Inputs["bar"]);

            var x = new AutomatedActivityNode { Id = "x", ActivityRef = "stepX" };
            x.Outputs.Add(new ActivityOutputBinding { Parameter = "result", Variable = "foo" });

            var y = new AutomatedActivityNode { Id = "y", ActivityRef = "stepY" };
            y.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "bar",
                Kind = ParameterBindingKind.Variable,
                Source = "foo"
            });

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "chain",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    x,
                    y,
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "x"), Flow("x", "y"), Flow("y", "e") }
            });

            WorkflowInstance instance = engine.StartWorkflow("chain");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(21, instance.Variables["foo"]);
            Assert.AreEqual(21, barSeen);
        }

        [TestMethod]
        public void Consolidate_ReplacesScopeWithOutputsOnly()
        {
            // Konsolidierung: liest n Variablen, gibt m aus - danach besteht der Scope NUR aus den m.
            activities.Register("consolidate", ctx => ctx.Outputs["summary"] = 42);

            store.SaveDefinition(OneActivity("cons", "consolidate", node =>
            {
                node.ScopeMode = ActivityScopeMode.Replace;
                node.Outputs.Add(new ActivityOutputBinding { Parameter = "summary", Variable = "summary" });
            }));

            WorkflowInstance instance = engine.StartWorkflow("cons",
                new Dictionary<string, object> { { "a", 1 }, { "b", 2 }, { "junk", "x" } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(1, instance.Variables.Count, "the scope must contain only the declared output.");
            Assert.AreEqual(42, instance.Variables["summary"]);
        }

        [TestMethod]
        public void Consolidate_RetainsWhitelistedVariables()
        {
            activities.Register("consolidate", ctx => ctx.Outputs["summary"] = 42);

            store.SaveDefinition(OneActivity("cons2", "consolidate", node =>
            {
                node.ScopeMode = ActivityScopeMode.Replace;
                node.Outputs.Add(new ActivityOutputBinding { Parameter = "summary", Variable = "summary" });
                node.RetainVariables.Add("tenant");
            }));

            WorkflowInstance instance = engine.StartWorkflow("cons2",
                new Dictionary<string, object> { { "tenant", "acme" }, { "junk", "x" } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            CollectionAssert.AreEquivalent(new[] { "tenant", "summary" }, instance.Variables.Keys.ToList());
            Assert.AreEqual("acme", instance.Variables["tenant"], "the retained variable must survive.");
            Assert.AreEqual(42, instance.Variables["summary"]);
        }

        [TestMethod]
        public void ExtendMode_KeepsExistingVariables()
        {
            // Gegenprobe: Standard (Extend) laesst den bestehenden Scope unangetastet.
            activities.Register("add", ctx => ctx.Outputs["summary"] = 42);

            store.SaveDefinition(OneActivity("ext", "add", node =>
            {
                node.ScopeMode = ActivityScopeMode.Extend;
                node.Outputs.Add(new ActivityOutputBinding { Parameter = "summary", Variable = "summary" });
            }));

            WorkflowInstance instance = engine.StartWorkflow("ext",
                new Dictionary<string, object> { { "a", 1 } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(1, instance.Variables["a"], "existing variables stay in Extend mode.");
            Assert.AreEqual(42, instance.Variables["summary"]);
        }

        private static WorkflowDefinition OneActivity(string id, string activityRef,
            System.Action<AutomatedActivityNode> configure)
        {
            var node = new AutomatedActivityNode { Id = "n", ActivityRef = activityRef };
            configure(node);
            return new WorkflowDefinition
            {
                Id = id,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    node,
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "n"), Flow("n", "e") }
            };
        }

        private static SequenceFlow Flow(string from, string to)
        {
            return new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };
        }
    }
}
