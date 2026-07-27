using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft das portable JSON-Export-/Import-Format (<see cref="WorkflowJson"/>): eine reichhaltige
    /// Definition (alle Knotentypen inkl. Subworkflow-Aufruf, Fehler-Ausgang, Datenfluss-Bindungen und
    /// typerhaltender Konfiguration) ueberlebt den Round-Trip, und der Diskriminator ist typnamen-unabhaengig.
    /// </summary>
    [TestClass]
    public class WorkflowJsonTest
    {
        private static WorkflowDefinition RichDefinition()
        {
            var activity = new AutomatedActivityNode
            {
                Id = "a", Name = "Do it", ActivityRef = "step",
                ExecutionTarget = "backend",
                ScopeMode = ActivityScopeMode.Replace,
                ErrorFlowId = "a->err", ErrorVariable = "err", AttemptVariable = "tries",
                Diagram = new DiagramShape { X = 10, Y = 20, Width = 140, Height = 54 }
            };
            activity.Configuration["retries"] = 3;                 // typerhaltender object-Wert
            activity.RetainVariables.Add("tenant");
            activity.Inputs.Add(new ActivityInputBinding { Parameter = "x", Kind = ParameterBindingKind.Variable, Source = "seed" });
            activity.Inputs.Add(new ActivityInputBinding { Parameter = "y", Kind = ParameterBindingKind.Literal, Literal = 7 });
            activity.Outputs.Add(new ActivityOutputBinding { Parameter = "sum", Variable = "total" });

            var call = new CallWorkflowNode { Id = "c", SubDefinitionId = "sub", SubDefinitionVersion = 2 };
            call.Inputs.Add(new ActivityInputBinding { Parameter = "n", Kind = ParameterBindingKind.Variable, Source = "total" });
            call.Outputs.Add(new ActivityOutputBinding { Parameter = "res", Variable = "answer" });

            return new WorkflowDefinition
            {
                Id = "rich", Version = 3, Name = "Rich", TenantId = "t1",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    activity,
                    call,
                    new WaitNode { Id = "w", SignalName = "go", CorrelationExpression = "id" },
                    new TimerNode { Id = "ti", DueExpression = "'System.DateTime'.UtcNow" },
                    new ExclusiveGatewayNode { Id = "x", DefaultFlowId = "x->e" },
                    new ParallelGatewayNode { Id = "p" },
                    new EndNode { Id = "e" },
                    new EndNode { Id = "err" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->a", SourceId = "s", TargetId = "a" },
                    new SequenceFlow { Id = "a->c", SourceId = "a", TargetId = "c" },
                    new SequenceFlow { Id = "a->err", SourceId = "a", TargetId = "err" },
                    new SequenceFlow { Id = "x->e", SourceId = "x", TargetId = "e", Condition = "n > 1" }
                }
            };
        }

        [TestMethod]
        public void Definition_RoundTrips_ThroughExportImport()
        {
            WorkflowDefinition original = RichDefinition();

            string json = WorkflowJson.ExportDefinition(original);
            WorkflowDefinition copy = WorkflowJson.ImportDefinition(json);

            Assert.AreEqual("rich", copy.Id);
            Assert.AreEqual(3, copy.Version);
            Assert.AreEqual("t1", copy.TenantId);

            // Polymorphe Knotentypen bleiben erhalten.
            Assert.IsInstanceOfType<AutomatedActivityNode>(copy.GetNode("a"));
            Assert.IsInstanceOfType<CallWorkflowNode>(copy.GetNode("c"));
            Assert.IsInstanceOfType<WaitNode>(copy.GetNode("w"));
            Assert.IsInstanceOfType<TimerNode>(copy.GetNode("ti"));
            Assert.IsInstanceOfType<ExclusiveGatewayNode>(copy.GetNode("x"));
            Assert.IsInstanceOfType<ParallelGatewayNode>(copy.GetNode("p"));

            var a = (AutomatedActivityNode)copy.GetNode("a");
            Assert.AreEqual("backend", a.ExecutionTarget);
            Assert.AreEqual(ActivityScopeMode.Replace, a.ScopeMode);
            Assert.AreEqual("a->err", a.ErrorFlowId);
            Assert.AreEqual("tries", a.AttemptVariable);
            Assert.IsInstanceOfType<int>(a.Configuration["retries"], "the object config value round-trips as int, not JsonElement.");
            Assert.AreEqual(3, a.Configuration["retries"]);
            CollectionAssert.AreEquivalent(new[] { "tenant" }, a.RetainVariables);
            Assert.AreEqual(2, a.Inputs.Count);
            Assert.IsInstanceOfType<int>(a.Inputs.Single(i => i.Parameter == "y").Literal, "the literal object round-trips as int.");
            Assert.AreEqual("total", a.Outputs.Single().Variable);
            Assert.AreEqual(10, a.Diagram.X, "diagram coordinates survive.");

            var c = (CallWorkflowNode)copy.GetNode("c");
            Assert.AreEqual("sub", c.SubDefinitionId);
            Assert.AreEqual(2, c.SubDefinitionVersion);
            Assert.AreEqual("n", c.Inputs.Single().Parameter);
            Assert.AreEqual("answer", c.Outputs.Single().Variable);

            Assert.AreEqual("n > 1", copy.Flows.Single(f => f.Id == "x->e").Condition);
        }

        [TestMethod]
        public void ExportJson_UsesStableKindDiscriminators_NotDotNetTypeNames()
        {
            string json = WorkflowJson.ExportDefinition(RichDefinition());

            StringAssert.Contains(json, "\"kind\": \"activity\"");
            StringAssert.Contains(json, "\"kind\": \"call\"");
            Assert.IsFalse(json.Contains("AutomatedActivityNode"),
                "the portable format must not embed .NET type names (rename-safe / cross-system).");
        }

        [TestMethod]
        public void ImportDefinition_EmptyOrBlank_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => WorkflowJson.ImportDefinition("  "));
        }
    }
}
