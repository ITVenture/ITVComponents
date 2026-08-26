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
    /// Prueft die <b>Zweig-Scopes</b>: ein AND-Split gibt jedem Strang eine eigene Kopie des
    /// Variablen-Stacks, der zugehoerige Join fuehrt sie wieder zusammen. Damit koennen sich parallele
    /// Zweige nicht mehr gegenseitig ueberschreiben - und der Join entscheidet (optional per Mapping),
    /// was die parallele Region als Ergebnis liefert.
    /// </summary>
    [TestClass]
    public class WorkflowBranchScopeTest
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
        public void BranchesGetTheirOwnCopyOfTheScope()
        {
            // Beide Zweige schreiben denselben Namen - frueher ein Wettlauf (oder ein Fault), jetzt
            // arbeitet jeder in seiner Kopie.
            var seenByB = new List<object>();
            activities.Register("setA", ctx => ctx.Variables["shared"] = "A");
            activities.Register("readB", ctx =>
            {
                seenByB.Add(ctx.Variables.TryGetValue("shared", out object v) ? v : null);
                ctx.Variables["shared"] = "B";
            });

            store.SaveDefinition(Fork("copy", "setA", "readB"));

            WorkflowInstance instance = engine.StartWorkflow("copy",
                new Dictionary<string, object> { { "shared", "start" } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            CollectionAssert.AreEqual(new object[] { "start" }, seenByB,
                "the second branch sees the state at the split, not what its sibling wrote.");
        }

        [TestMethod]
        public void JoinMergesWhatTheBranchesWrote()
        {
            activities.Register("setA", ctx => ctx.Variables["a"] = 1);
            activities.Register("setB", ctx => ctx.Variables["b"] = 2);

            store.SaveDefinition(Fork("merge", "setA", "setB"));

            WorkflowInstance instance = engine.StartWorkflow("merge",
                new Dictionary<string, object> { { "seed", 0 } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            CollectionAssert.AreEquivalent(new[] { "seed", "a", "b" }, instance.Variables.Keys.ToList(),
                "without a declared join result everything the branches wrote flows up (as before).");
            Assert.AreEqual(1, instance.Variables["a"]);
            Assert.AreEqual(2, instance.Variables["b"]);
        }

        [TestMethod]
        public void InstanceScopeStaysAtTheSplitStateWhileTheRegionIsOpen()
        {
            activities.Register("setA", ctx => ctx.Variables["a"] = 1);

            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "open",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "x", ActivityRef = "setA" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new ParallelGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "split"), Flow("split", "x"), Flow("split", "w"),
                    Flow("x", "join"), Flow("w", "join"), Flow("join", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("open");

            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);
            Assert.IsFalse(instance.Variables.ContainsKey("a"),
                "as long as the region is open the branch result is not yet in the instance scope.");
            Assert.AreEqual(1, instance.Tokens.Single(t => t.Status == TokenStatus.Joining).Variables["a"]);

            engine.SignalWorkflow(instance.Id, "go");

            Assert.AreEqual(1, store.GetInstance(instance.Id).Variables["a"]);
        }

        [TestMethod]
        public void JoinMapping_DeclaresWhatComesOutOfTheRegion()
        {
            activities.Register("setA", ctx =>
            {
                ctx.Variables["scratchA"] = "noise";
                ctx.Variables["a"] = 1;
            });
            activities.Register("setB", ctx =>
            {
                ctx.Variables["scratchB"] = "noise";
                ctx.Variables["b"] = 2;
            });

            WorkflowDefinition def = Fork("declared", "setA", "setB");
            var join = (ParallelGatewayNode)def.GetNode("join");
            join.Outputs.Add(new ActivityOutputBinding { Parameter = "a", Variable = "resultA" });
            join.Outputs.Add(new ActivityOutputBinding { Parameter = "b", Variable = "resultB" });
            store.SaveDefinition(def);

            WorkflowInstance instance = engine.StartWorkflow("declared",
                new Dictionary<string, object> { { "seed", 0 } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            CollectionAssert.AreEquivalent(new[] { "seed", "resultA", "resultB" },
                instance.Variables.Keys.ToList(),
                "with a declared join result exactly that leaves the region - the branch scratch stays inside.");
            Assert.AreEqual(1, instance.Variables["resultA"]);
            Assert.AreEqual(2, instance.Variables["resultB"]);
        }

        [TestMethod]
        public void JoinMapping_Replace_ConsolidatesTheScope()
        {
            activities.Register("setA", ctx => ctx.Variables["a"] = 1);
            activities.Register("setB", ctx => ctx.Variables["b"] = 2);

            WorkflowDefinition def = Fork("cons", "setA", "setB");
            var join = (ParallelGatewayNode)def.GetNode("join");
            join.ScopeMode = ActivityScopeMode.Replace;
            join.RetainVariables.Add("corr");
            join.Outputs.Add(new ActivityOutputBinding { Parameter = "a", Variable = "total" });
            store.SaveDefinition(def);

            WorkflowInstance instance = engine.StartWorkflow("cons",
                new Dictionary<string, object> { { "corr", "K-1" }, { "seed", 0 } });

            CollectionAssert.AreEquivalent(new[] { "total", "corr" }, instance.Variables.Keys.ToList(),
                "a consolidating join leaves exactly its result plus the retained variables.");
            Assert.AreEqual(1, instance.Variables["total"]);
        }

        [TestMethod]
        public void BranchLocalConsolidation_DoesNotClearTheOuterScope()
        {
            // Replace INNERHALB eines Zweigs raeumt nur dessen Kopie ab - der Stand vom Split (und damit
            // der Beitrag des Geschwisterzweigs) bleibt unberuehrt.
            activities.Register("consolidate", ctx => { });
            activities.Register("setB", ctx => ctx.Variables["b"] = 2);

            WorkflowDefinition def = Fork("branchcons", "consolidate", "setB");
            var a = (AutomatedActivityNode)def.GetNode("a");
            a.ScopeMode = ActivityScopeMode.Replace;
            a.Outputs.Add(new ActivityOutputBinding { Parameter = "any", Variable = "onlyThis" });
            store.SaveDefinition(def);

            WorkflowInstance instance = engine.StartWorkflow("branchcons",
                new Dictionary<string, object> { { "seed", 42 } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(42, instance.Variables["seed"],
                "the branch-local consolidation must not wipe the outer scope.");
            Assert.AreEqual(2, instance.Variables["b"]);
        }

        [TestMethod]
        public void NestedSplits_MergeBackLevelByLevel()
        {
            activities.Register("setInner1", ctx => ctx.Variables["i1"] = 1);
            activities.Register("setInner2", ctx => ctx.Variables["i2"] = 2);
            activities.Register("setOuter", ctx => ctx.Variables["o"] = 3);

            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "nested",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "outerSplit" },
                    new ParallelGatewayNode { Id = "innerSplit" },
                    new AutomatedActivityNode { Id = "i1", ActivityRef = "setInner1" },
                    new AutomatedActivityNode { Id = "i2", ActivityRef = "setInner2" },
                    new ParallelGatewayNode { Id = "innerJoin" },
                    new AutomatedActivityNode { Id = "o", ActivityRef = "setOuter" },
                    new ParallelGatewayNode { Id = "outerJoin" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "outerSplit"),
                    Flow("outerSplit", "innerSplit"), Flow("outerSplit", "o"),
                    Flow("innerSplit", "i1"), Flow("innerSplit", "i2"),
                    Flow("i1", "innerJoin"), Flow("i2", "innerJoin"),
                    Flow("innerJoin", "outerJoin"), Flow("o", "outerJoin"),
                    Flow("outerJoin", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("nested",
                new Dictionary<string, object> { { "seed", 0 } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            CollectionAssert.AreEquivalent(new[] { "seed", "i1", "i2", "o" },
                instance.Variables.Keys.ToList(),
                "the inner join merges into the outer branch, the outer join into the instance scope.");
            Assert.IsTrue(instance.Tokens.All(t => t.Variables == null),
                "once a region is closed its branch copies are released - they must not pile up per activation.");
        }

        [TestMethod]
        public void DivergentBranchWrites_AreReportedNotSwallowed()
        {
            activities.Register("setA", ctx => ctx.Variables["shared"] = "A");
            activities.Register("setB", ctx => ctx.Variables["shared"] = "B");

            store.SaveDefinition(Fork("clash", "setA", "setB"));

            WorkflowInstance instance = engine.StartWorkflow("clash");

            // Kein Fault mehr (jeder Zweig hatte seine Kopie), aber auch kein stiller Gewinner: der
            // Konflikt steht im Protokoll und ist damit im Monitor sichtbar.
            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            HistoryEntry conflict = instance.History
                .FirstOrDefault(h => h.Event == "BranchMergeConflict");
            Assert.IsNotNull(conflict, "a divergent parallel write must not be merged silently.");
            Assert.AreEqual(HistorySeverity.Warning, conflict.Severity);
            StringAssert.Contains(conflict.Detail, "shared");
        }

        [TestMethod]
        public void SameValueInBothBranches_IsNoConflict()
        {
            activities.Register("setA", ctx => ctx.Variables["shared"] = 7);
            activities.Register("setB", ctx => ctx.Variables["shared"] = 7);

            store.SaveDefinition(Fork("agree", "setA", "setB"));

            WorkflowInstance instance = engine.StartWorkflow("agree");

            Assert.AreEqual(7, instance.Variables["shared"]);
            Assert.IsFalse(instance.History.Any(h => h.Event == "BranchMergeConflict"),
                "branches agreeing on a value is not a conflict.");
        }

        [TestMethod]
        public void SequentialWorkflow_HasNoBranchScopes()
        {
            // Rueckwaerts-Kompatibilitaet: ohne Split gibt es keine Kopien - Tokens arbeiten wie bisher
            // direkt auf dem Instanz-Scope.
            activities.Register("setA", ctx => ctx.Variables["a"] = 1);

            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "seq",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "setA" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "a"), Flow("a", "w"), Flow("w", "e") }
            });

            WorkflowInstance instance = engine.StartWorkflow("seq");

            Assert.AreEqual(1, instance.Variables["a"]);
            Assert.IsTrue(instance.Tokens.All(t => t.Variables == null && t.SplitTokenId == null),
                "outside a parallel region no token carries a branch scope.");
        }

        [TestMethod]
        public void BranchEndingWithoutItsJoin_IsReported()
        {
            // Modellierungsfehler: ein Zweig laeuft am Join vorbei ins Ende. Sein Scope wird nie
            // zusammengefuehrt - das darf nicht still passieren.
            activities.Register("setA", ctx => ctx.Variables["lost"] = "gone");
            activities.Register("noop", _ => { });

            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "leak",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "setA" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "noop" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "split"), Flow("split", "a"), Flow("split", "b"),
                    Flow("a", "e"), Flow("b", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("leak");

            Assert.IsTrue(instance.History.Any(h => h.Event == "BranchScopeDiscarded"
                                                    && h.Severity == HistorySeverity.Warning),
                "a branch that ends without its join loses its variables - that must be logged.");
        }

        // --- Aufbau-Helfer -------------------------------------------------------------------------

        /// <summary>Start → Split → zwei Aktivitaeten → Join → End.</summary>
        private static WorkflowDefinition Fork(string id, string activityA, string activityB)
        {
            return new WorkflowDefinition
            {
                TechnicalName = id,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = activityA },
                    new AutomatedActivityNode { Id = "b", ActivityRef = activityB },
                    new ParallelGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "split"), Flow("split", "a"), Flow("split", "b"),
                    Flow("a", "join"), Flow("b", "join"), Flow("join", "e")
                }
            };
        }

        private static SequenceFlow Flow(string from, string to)
            => new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };
    }
}
