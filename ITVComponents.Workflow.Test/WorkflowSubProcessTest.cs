using System;
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
    /// Prueft den eingebetteten <see cref="SubProcessNode"/>: eigene Knoten im selben Graphen, eigener
    /// Scope, aber KEINE eigene Instanz.
    /// </summary>
    [TestClass]
    public class WorkflowSubProcessTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Start -&gt; [Subprozess: iStart -&gt; inner -&gt; iEnd] -&gt; after -&gt; Ende.
        /// </summary>
        private SubProcessNode SaveDefinition(Action<SubProcessNode> configure = null,
            WorkflowNode innerNode = null)
        {
            var sub = new SubProcessNode { Id = "sub", Name = "Check" };
            configure?.Invoke(sub);
            innerNode ??= new AutomatedActivityNode { Id = "inner", ActivityRef = "work" };
            innerNode.ParentNodeId = "sub";

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    sub,
                    new StartNode { Id = "iStart", ParentNodeId = "sub" },
                    innerNode,
                    new EndNode { Id = "iEnd", ParentNodeId = "sub" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "work" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "sub"), F("sub", "after"), F("after", "e"),
                    F("iStart", innerNode.Id), F(innerNode.Id, "iEnd")
                }
            });
            return sub;
        }

        private WorkflowEngine Engine(List<string> ran = null)
            => new WorkflowEngine(store, new ActivityRegistry().Register("work",
                ctx => (ran ?? new List<string>()).Add(ctx.Node.Id)));

        [TestMethod]
        public void TheSectionRuns_AndTheOuterBranchContinuesAfterIt()
        {
            SaveDefinition();
            var ran = new List<string>();

            WorkflowInstance inst = Engine(ran).StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            CollectionAssert.AreEqual(new[] { "inner", "after" }, ran,
                "the section runs first, then the outer branch carries on.");
        }

        [TestMethod]
        public void ItStaysOneInstance()
        {
            SaveDefinition();

            WorkflowInstance inst = Engine().StartWorkflow("wf");

            Assert.AreEqual(0, store.FindChildInstances(inst.Id).Count(),
                "an embedded section is NOT a subworkflow - it must not create a child instance.");
        }

        [TestMethod]
        public void TheOuterTokenParksWhileTheSectionRuns()
        {
            // Innen ein Wartepunkt: dadurch bleibt der Abschnitt offen und der aeussere Zustand sichtbar.
            SaveDefinition(innerNode: new WaitNode { Id = "inner", SignalName = "go" });

            WorkflowInstance inst = Engine().StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Token outer = final.Tokens.Single(t => t.NodeId == "sub");
            Assert.AreEqual(TokenStatus.Waiting, outer.Status,
                "the outer token parks on the sub-process node - that is what lets a deadline hang on it.");
            Token inner = final.Tokens.Single(t => t.NodeId == "inner");
            Assert.AreEqual(outer.Id, inner.SubProcessOwnerTokenId,
                "every token inside carries the section it belongs to.");
        }

        [TestMethod]
        public void TheSectionHasItsOwnScope_AndOnlyTheMappedResultLeaves()
        {
            SaveDefinition(sub => sub.Outputs.Add(
                new ActivityOutputBinding { Parameter = "innerResult", Variable = "outerResult" }));
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("work", ctx =>
            {
                if (ctx.Node.Id == "inner")
                {
                    ctx.Variables["innerResult"] = "computed";
                    ctx.Variables["scratch"] = "should stay inside";
                }
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual("computed", final.Variables["outerResult"], "the mapped result comes out.");
            Assert.IsFalse(final.Variables.ContainsKey("scratch"),
                "what the section computed for itself stays inside - that is the point of its own scope.");
        }

        [TestMethod]
        public void ADeadlineCanHangOnTheWholeSection()
        {
            // DER Gewinn gegenueber dem Aufruf einer eigenen Definition: eine Frist ueber MEHRERE Schritte.
            SubProcessNode sub = SaveDefinition(innerNode: new WaitNode { Id = "inner", SignalName = "go" });
            WorkflowDefinition definition = store.GetDefinition("wf");
            definition.Nodes.Add(new BoundaryTimerNode
            {
                Id = "deadline",
                AttachedToNodeId = "sub",
                Interrupting = true,
                Deadlines = new List<BoundaryDeadline>
                {
                    new BoundaryDeadline { Expression = "'System.DateTime'.UtcNow" }
                }
            });
            definition.Nodes.Add(new SidePathEndNode { Id = "escalated" });
            definition.Flows.Add(F("deadline", "escalated"));
            definition.RebuildIndex();
            store.SaveDefinition(definition);

            WorkflowEngine engine = Engine();
            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.IsTrue(BoundaryTimerNode.CanHost(store.GetDefinition("wf").GetNode("sub")),
                "a sub-process parks, so a deadline may hang on it.");

            engine.TriggerTimers(store.GetInstance(inst.Id), DateTime.UtcNow.AddSeconds(1));

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.IsFalse(final.Tokens.Any(t => t.NodeId == "inner"
                                                 && t.Status != TokenStatus.Consumed),
                "when the deadline interrupts the section, its inner tokens must be gone too.");
        }

        [TestMethod]
        public void AnInnerEnd_DoesNotDeclareTheWorkflowResult()
        {
            // Das Ende INNEN beendet den Abschnitt. Wuerde es als Ergebnis-Knoten der Instanz zaehlen,
            // bestimmte ein Abschnitt das Ergebnis des ganzen Workflows.
            var innerEnd = new EndNode { Id = "iEnd", ParentNodeId = "sub" };
            innerEnd.Outputs.Add(new ActivityOutputBinding { Parameter = "x", Variable = "sectionOnly" });
            SaveDefinition();
            WorkflowDefinition definition = store.GetDefinition("wf");
            definition.Nodes.RemoveAll(n => n.Id == "iEnd");
            definition.Nodes.Add(innerEnd);
            definition.RebuildIndex();
            store.SaveDefinition(definition);

            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("work",
                ctx => ctx.Variables["x"] = 1));
            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.IsFalse(final.Variables.ContainsKey("sectionOnly"),
                "the section's own end declared a result - it must not become the result of the WORKFLOW.");
        }

        [TestMethod]
        public void EachLevelNeedsItsOwnStartAndEnd()
        {
            var definition = new WorkflowDefinition
            {
                Id = "bad",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new SubProcessNode { Id = "sub" },
                    // Kein Start und kein Ende im Abschnitt.
                    new AutomatedActivityNode { Id = "inner", ActivityRef = "w", ParentNodeId = "sub" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "sub"), F("sub", "e") }
            };

            var issues = WorkflowDefinitionValidator.Validate(definition).ToList();

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error
                                          && i.Message.Contains("no start node inside")),
                "a section without a start could never begin.");
        }

        [TestMethod]
        public void TheOuterLevelIsStillCheckedSeparately()
        {
            // Der Start IM Abschnitt darf den Start der Definition nicht ersetzen - und umgekehrt darf er
            // nicht als zweiter Start der obersten Ebene gemeldet werden.
            SaveDefinition();

            var issues = WorkflowDefinitionValidator.Validate(store.GetDefinition("wf")).ToList();

            Assert.IsFalse(issues.Any(i => i.Severity == ValidationSeverity.Error
                                           && i.Message.Contains("exactly one start node")),
                "one start per level - the inner one must not be counted against the outer level.");
        }
    }
}
