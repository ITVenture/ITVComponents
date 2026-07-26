using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft Subworkflows (Phase: CallWorkflow): ein Elternprozess ruft einen anderen Workflow mit
    /// Ein-/Ausgabewerten auf, parkt bis zu dessen Ende und uebernimmt dann das Ergebnis. Getrieben ueber
    /// <see cref="WorkflowEngine.RunBranch"/> (EF-Store, Kopien) - der Baum wird deterministisch bis zur
    /// Ruhe abgearbeitet, wie es der Runner nebenlaeufig tut.
    /// </summary>
    [TestClass]
    public class WorkflowSubworkflowTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;
        private WorkflowEngine engine;

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
            engine = new WorkflowEngine(store, new ActivityRegistry()
                .Register("compute", ctx => ctx.Variables["result"] = (int)ctx.Variables["n"] * 10)
                .Register("boom", _ => throw new InvalidOperationException("child failed")));
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        /// <summary>Elternprozess: Start -&gt; CallWorkflow(sub, n&lt;-value, result-&gt;answer) -&gt; End.</summary>
        private static WorkflowDefinition MainDef(string subId)
        {
            var call = new CallWorkflowNode { Id = "call", SubDefinitionId = subId };
            call.Inputs.Add(new ActivityInputBinding { Parameter = "n", Kind = ParameterBindingKind.Variable, Source = "value" });
            call.Outputs.Add(new ActivityOutputBinding { Parameter = "result", Variable = "answer" });
            return new WorkflowDefinition
            {
                Id = "main",
                Version = 1,
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, call, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->call", SourceId = "s", TargetId = "call" },
                    new SequenceFlow { Id = "call->e", SourceId = "call", TargetId = "e" }
                }
            };
        }

        private static WorkflowDefinition SubDef(string activityRef)
        {
            return new WorkflowDefinition
            {
                Id = "sub",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "c", ActivityRef = activityRef },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->c", SourceId = "s", TargetId = "c" },
                    new SequenceFlow { Id = "c->e", SourceId = "c", TargetId = "e" }
                }
            };
        }

        /// <summary>Treibt den ganzen Prozessbaum (Eltern + Subworkflows) deterministisch bis zur Ruhe.</summary>
        private void DriveTree(string rootId)
        {
            int guard = 0;
            while (true)
            {
                if (++guard > 1000)
                {
                    Assert.Fail("the process tree did not settle.");
                }

                List<WorkflowInstance> tree = CollectTree(rootId);
                // Wie der Runner (FindRunnable): nur laufende Instanzen vorantreiben. Eine gefaultete Instanz
                // kann ihr aktives (fehlerhaftes) Token behalten - sie wird NICHT erneut ausgefuehrt.
                var next = tree
                    .Where(i => i.Status == WorkflowStatus.Running)
                    .SelectMany(i => i.ActiveTokens.Select(t => (InstanceId: i.Id, TokenId: t.Id)))
                    .FirstOrDefault();
                if (next.InstanceId == null)
                {
                    break; // keine aktiven Tokens mehr im Baum - fertig (Kind-Completion liefert RunBranch selbst).
                }

                engine.RunBranch(next.InstanceId, next.TokenId);
            }
        }

        private List<WorkflowInstance> CollectTree(string rootId)
        {
            var result = new List<WorkflowInstance>();
            var queue = new Queue<string>();
            queue.Enqueue(rootId);
            while (queue.Count > 0)
            {
                WorkflowInstance inst = store.GetInstance(queue.Dequeue());
                if (inst == null)
                {
                    continue;
                }

                result.Add(inst);
                foreach (WorkflowInstance child in store.FindChildInstances(inst.Id))
                {
                    queue.Enqueue(child.Id);
                }
            }

            return result;
        }

        [TestMethod]
        public void Subworkflow_RunsWithInputs_AndMapsOutputsBackToParent()
        {
            store.SaveDefinition(SubDef("compute"));
            store.SaveDefinition(MainDef("sub"));

            WorkflowInstance parent = engine.CreateInstance("main", new Dictionary<string, object> { { "value", 5 } });
            DriveTree(parent.Id);

            WorkflowInstance finalParent = store.GetInstance(parent.Id);
            Assert.AreEqual(WorkflowStatus.Completed, finalParent.Status, "the parent completes after the sub-workflow.");
            Assert.AreEqual(50, finalParent.Variables["answer"], "the sub-workflow's output mapped back to the parent.");

            WorkflowInstance child = store.FindChildInstances(parent.Id).Single();
            Assert.AreEqual(WorkflowStatus.Completed, child.Status);
            Assert.AreEqual(5, child.Variables["n"], "the input was passed into the child.");
            Assert.AreEqual(parent.Id, child.ParentInstanceId);
            Assert.AreEqual(parent.Id, child.RootInstanceId, "the child inherits the parent's tree root.");
            Assert.AreEqual(1, child.CallDepth);
        }

        [TestMethod]
        public void Subworkflow_Faulted_FaultsTheCallingNode()
        {
            store.SaveDefinition(SubDef("boom"));
            store.SaveDefinition(MainDef("sub"));

            WorkflowInstance parent = engine.CreateInstance("main", new Dictionary<string, object> { { "value", 1 } });
            DriveTree(parent.Id);

            WorkflowInstance finalParent = store.GetInstance(parent.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, finalParent.Status, "a faulted sub-workflow faults the caller (default).");
            StringAssert.Contains(finalParent.FaultMessage, "sub-workflow", StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void Subworkflow_HistoryIsVisibleUnderTheParentTreeRoot()
        {
            store.SaveDefinition(SubDef("compute"));
            store.SaveDefinition(MainDef("sub"));

            WorkflowInstance parent = engine.CreateInstance("main", new Dictionary<string, object> { { "value", 3 } });
            DriveTree(parent.Id);
            WorkflowInstance child = store.FindChildInstances(parent.Id).Single();

            // Der User-Wunsch: das Protokoll des GESAMTEN Prozessbaums ist ueber die Root mit EINER Abfrage
            // lesbar - Eltern- UND Kind-Eintraege tragen dieselbe RootInstanceId.
            using WorkflowContext ctx = new WorkflowContext(options);
            List<HistoryEntryRow> tree = ctx.HistoryEntries
                .Where(h => h.RootInstanceId == parent.Id).ToList();

            CollectionAssert.Contains(tree.Select(h => h.InstanceId).Distinct().ToList(), parent.Id);
            CollectionAssert.Contains(tree.Select(h => h.InstanceId).Distinct().ToList(), child.Id);
            Assert.IsTrue(tree.Any(h => h.InstanceId == child.Id && h.Event == "Started"),
                "the child's own history is aggregated under the parent's tree root.");
        }

        [TestMethod]
        public void Subworkflow_NestedTwoLevels_RootIsPassedThrough_AndResultBubblesUp()
        {
            // leaf: result = n * 10 (Eingabe n, Ausgabe result)
            store.SaveDefinition(SubDef("compute")); // Id "sub", ActivityRef "compute" -> result = n*10

            // mid: ruft "sub" (n<-x, result->y) -> y = x*10
            var midCall = new CallWorkflowNode { Id = "call", SubDefinitionId = "sub" };
            midCall.Inputs.Add(new ActivityInputBinding { Parameter = "n", Kind = ParameterBindingKind.Variable, Source = "x" });
            midCall.Outputs.Add(new ActivityOutputBinding { Parameter = "result", Variable = "y" });
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "mid",
                Version = 1,
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, midCall, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->call", SourceId = "s", TargetId = "call" },
                    new SequenceFlow { Id = "call->e", SourceId = "call", TargetId = "e" }
                }
            });

            // top: ruft "mid" (x<-seed, y->outcome) -> outcome = seed*10
            var topCall = new CallWorkflowNode { Id = "call", SubDefinitionId = "mid" };
            topCall.Inputs.Add(new ActivityInputBinding { Parameter = "x", Kind = ParameterBindingKind.Variable, Source = "seed" });
            topCall.Outputs.Add(new ActivityOutputBinding { Parameter = "y", Variable = "outcome" });
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "top",
                Version = 1,
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, topCall, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->call", SourceId = "s", TargetId = "call" },
                    new SequenceFlow { Id = "call->e", SourceId = "call", TargetId = "e" }
                }
            });

            WorkflowInstance top = engine.CreateInstance("top", new Dictionary<string, object> { { "seed", 4 } });
            DriveTree(top.Id);

            WorkflowInstance finalTop = store.GetInstance(top.Id);
            Assert.AreEqual(WorkflowStatus.Completed, finalTop.Status);
            Assert.AreEqual(40, finalTop.Variables["outcome"], "the grandchild's result bubbled up through two levels.");

            WorkflowInstance mid = store.FindChildInstances(top.Id).Single();
            WorkflowInstance leaf = store.FindChildInstances(mid.Id).Single();
            Assert.AreEqual(2, leaf.CallDepth, "the grandchild sits two levels deep.");
            Assert.AreEqual(top.Id, mid.RootInstanceId);
            Assert.AreEqual(top.Id, leaf.RootInstanceId, "the tree root is passed through to the grandchild.");

            // Aggregierte History des GESAMTEN Baums (Top + Mid + Leaf) ueber die eine Root.
            using WorkflowContext ctx = new WorkflowContext(options);
            List<string> treeInstances = ctx.HistoryEntries
                .Where(h => h.RootInstanceId == top.Id).Select(h => h.InstanceId).Distinct().ToList();
            CollectionAssert.Contains(treeInstances, top.Id);
            CollectionAssert.Contains(treeInstances, mid.Id);
            CollectionAssert.Contains(treeInstances, leaf.Id);
        }

        [TestMethod]
        public void CancelParent_CascadesToRunningChild()
        {
            // Kind, das an einem Wartepunkt haengt (laeuft nicht von selbst zu Ende).
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "sub",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->e", SourceId = "w", TargetId = "e" }
                }
            });
            store.SaveDefinition(MainDef("sub"));

            WorkflowInstance parent = engine.CreateInstance("main", new Dictionary<string, object> { { "value", 1 } });
            DriveTree(parent.Id); // Eltern parkt am Call, Kind wartet am Signal
            WorkflowInstance child = store.FindChildInstances(parent.Id).Single();
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(child.Id).Status);

            Assert.IsTrue(engine.CancelWorkflow(parent.Id));

            Assert.AreEqual(WorkflowStatus.Cancelled, store.GetInstance(parent.Id).Status);
            Assert.AreEqual(WorkflowStatus.Cancelled, store.GetInstance(child.Id).Status,
                "cancelling the parent cascades to the running child.");
        }
    }
}
