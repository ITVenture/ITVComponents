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
    /// Prueft das <b>inklusive Gateway</b> (OR): der Split nimmt alle zutreffenden Ausgaenge, der Join
    /// wartet auf genau die, die der Split angemeldet hat.
    /// </summary>
    [TestClass]
    public class WorkflowInclusiveGatewayTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to, string condition = null) =>
            new SequenceFlow
            {
                Id = $"{from}->{to}", SourceId = from, TargetId = to, Condition = condition
            };

        /// <summary>
        /// Start -&gt; OR-Split -&gt; {a, b, c} -&gt; OR-Join -&gt; after -&gt; Ende. Jeder Zweig haengt an
        /// einer eigenen Bedingung.
        /// </summary>
        private void SaveDefinition(string defaultFlowId = null)
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new InclusiveGatewayNode { Id = "split", DefaultFlowId = defaultFlowId },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "work" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "work" },
                    new AutomatedActivityNode { Id = "c", ActivityRef = "work" },
                    new InclusiveGatewayNode { Id = "join" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "work" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"),
                    F("split", "a", "wantA"), F("split", "b", "wantB"), F("split", "c", "wantC"),
                    F("a", "join"), F("b", "join"), F("c", "join"),
                    F("join", "after"), F("after", "e")
                }
            });
        }

        private WorkflowEngine Engine(List<string> ran = null)
            => new WorkflowEngine(store, new ActivityRegistry().Register("work",
                ctx => (ran ?? new List<string>()).Add(ctx.Node.Id)));

        private static Dictionary<string, object> Wants(bool a, bool b, bool c)
            => new Dictionary<string, object> { { "wantA", a }, { "wantB", b }, { "wantC", c } };

        [TestMethod]
        public void OnlyTheMatchingBranchesRun_AndTheJoinWaitsForExactlyThose()
        {
            SaveDefinition();
            var ran = new List<string>();

            WorkflowInstance inst = Engine(ran).StartWorkflow("wf", Wants(true, false, true));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status,
                "waiting for all three inputs would be a deadlock - the join must know that only two come.");
            CollectionAssert.Contains(ran, "a");
            CollectionAssert.Contains(ran, "c");
            CollectionAssert.DoesNotContain(ran, "b");
        }

        [TestMethod]
        public void WhatFollowsTheJoinRunsExactlyOnce()
        {
            SaveDefinition();
            var ran = new List<string>();

            Engine(ran).StartWorkflow("wf", Wants(true, true, true));

            Assert.AreEqual(1, ran.Count(r => r == "after"),
                "an implicit merge would run everything behind it once per branch - that is what the join " +
                "is for.");
        }

        [TestMethod]
        public void ASingleMatchingBranchAlsoPassesTheJoin()
        {
            SaveDefinition();
            var ran = new List<string>();

            WorkflowInstance inst = Engine(ran).StartWorkflow("wf", Wants(false, true, false));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            CollectionAssert.AreEqual(new[] { "b", "after" }, ran);
        }

        [TestMethod]
        public void TheJoinWaitsWhileABranchIsStillOut()
        {
            // Zweig b parkt an einem Wartepunkt: der Join darf nicht mit den anderen feuern.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new InclusiveGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "work" },
                    new WaitNode { Id = "b", SignalName = "go" },
                    new InclusiveGatewayNode { Id = "join" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "work" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "a", "wantA"), F("split", "b", "wantB"),
                    F("a", "join"), F("b", "join"), F("join", "after"), F("after", "e")
                }
            });
            var ran = new List<string>();
            WorkflowEngine engine = Engine(ran);

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "wantA", true }, { "wantB", true } });

            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(inst.Id).Status);
            CollectionAssert.DoesNotContain(ran, "after", "one branch is still out - the join must wait.");

            engine.SignalWorkflow(inst.Id, "go");

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            CollectionAssert.Contains(ran, "after");
        }

        [TestMethod]
        public void EveryBranchCarriesTheAnnouncedCount()
        {
            // Ein Wartepunkt haelt die Zweige offen, damit der Stempel sichtbar bleibt.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new InclusiveGatewayNode { Id = "split" },
                    new WaitNode { Id = "a", SignalName = "go" },
                    new WaitNode { Id = "b", SignalName = "go" },
                    new WaitNode { Id = "c", SignalName = "go" },
                    new InclusiveGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"),
                    F("split", "a", "wantA"), F("split", "b", "wantB"), F("split", "c", "wantC"),
                    F("a", "join"), F("b", "join"), F("c", "join"), F("join", "e")
                }
            });

            WorkflowInstance inst = Engine().StartWorkflow("wf", Wants(true, false, true));

            List<Token> waiting = store.GetInstance(inst.Id).Tokens
                .Where(t => t.Status == TokenStatus.Waiting).ToList();
            Assert.AreEqual(2, waiting.Count);
            Assert.IsTrue(waiting.All(t => t.SplitBranchCount == 2),
                "the split is the only one that knows how many branches it activated - so it stamps it on.");
            Assert.AreEqual(1, waiting.Select(t => t.SplitTokenId).Distinct().Count(),
                "all branches of one activation share the same origin - that is how the join groups them.");
        }

        [TestMethod]
        public void WithoutAMatch_TheDefaultFlowRuns()
        {
            SaveDefinition(defaultFlowId: "split->c");
            var ran = new List<string>();

            WorkflowInstance inst = Engine(ran).StartWorkflow("wf", Wants(false, false, false));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            CollectionAssert.AreEqual(new[] { "c", "after" }, ran);
        }

        [TestMethod]
        public void TheDefaultFlowStaysOutWhileSomethingElseMatches()
        {
            SaveDefinition(defaultFlowId: "split->c");
            var ran = new List<string>();

            Engine(ran).StartWorkflow("wf", Wants(true, false, true));

            CollectionAssert.DoesNotContain(ran, "c",
                "the default flow is for the case that NOTHING matches - it does not take part otherwise.");
            CollectionAssert.Contains(ran, "a");
        }

        [TestMethod]
        public void WithoutAMatchAndWithoutADefault_ItFaults()
        {
            SaveDefinition();

            WorkflowInstance inst = Engine().StartWorkflow("wf", Wants(false, false, false));

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status,
                "silently taking no path at all would lose the branch without a trace.");
            StringAssert.Contains(final.FaultMessage, "No condition matched");
        }

        [TestMethod]
        public void TheBranchesAreMergedIntoOneScope()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new InclusiveGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "setA" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "setB" },
                    new InclusiveGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "a", "wantA"), F("split", "b", "wantB"),
                    F("a", "join"), F("b", "join"), F("join", "e")
                }
            });
            var activities = new ActivityRegistry()
                .Register("setA", ctx => ctx.Variables["fromA"] = 1)
                .Register("setB", ctx => ctx.Variables["fromB"] = 2);

            WorkflowInstance inst = new WorkflowEngine(store, activities).StartWorkflow("wf",
                new Dictionary<string, object> { { "wantA", true }, { "wantB", true } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(1, final.Variables["fromA"]);
            Assert.AreEqual(2, final.Variables["fromB"],
                "the join merges the branch copies - exactly like the AND join does.");
        }

        [TestMethod]
        public void ATokenArrivingWithoutABranchCount_FaultsInsteadOfHanging()
        {
            // Eine Kante direkt auf den Join gezogen: dieses Token wird nie mitgezaehlt.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "and" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "work" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "work" },
                    new InclusiveGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "and"), F("and", "a"), F("and", "b"),
                    F("a", "join"), F("b", "join"), F("join", "e")
                }
            });

            WorkflowInstance inst = Engine().StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status,
                "a token that can never be counted must say so - otherwise the instance hangs and nothing " +
                "says why.");
            StringAssert.Contains(final.FaultMessage, "without a branch count");
        }

        [TestMethod]
        public void ALoopOverTheSameSplitDoesNotMixUpTheRounds()
        {
            // Nach dem Join geht es ueber ein XOR zurueck auf den Split - zweite Runde, neue Stempel.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "count", ActivityRef = "count" },
                    new InclusiveGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "work" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "work" },
                    new InclusiveGatewayNode { Id = "join" },
                    new ExclusiveGatewayNode { Id = "again", DefaultFlowId = "again->e" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "count"), F("count", "split"),
                    F("split", "a", "wantA"), F("split", "b", "wantB"),
                    F("a", "join"), F("b", "join"), F("join", "again"),
                    F("again", "count", "rounds < 2"), F("again", "e")
                }
            });
            var ran = new List<string>();
            var activities = new ActivityRegistry()
                .Register("work", ctx => ran.Add(ctx.Node.Id))
                .Register("count", ctx =>
                {
                    int rounds = ctx.Variables.TryGetValue("rounds", out object v) ? (int)v : 0;
                    ctx.Variables["rounds"] = rounds + 1;
                    // Runde 1: beide Zweige, Runde 2: nur noch einer.
                    ctx.Variables["wantB"] = rounds == 0;
                });

            WorkflowInstance inst = new WorkflowEngine(store, activities).StartWorkflow("wf",
                new Dictionary<string, object> { { "wantA", true }, { "wantB", true } });

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status,
                "a second round through the same split must not wait for the branches of the first.");
            Assert.AreEqual(2, ran.Count(r => r == "a"));
            Assert.AreEqual(1, ran.Count(r => r == "b"),
                "the second round only activated one branch - and the join knew it.");
        }

        // --- Validierung ---------------------------------------------------------------------

        private static WorkflowDefinition Paired()
        {
            return new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new InclusiveGatewayNode { Id = "split", DefaultFlowId = "split->b" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "work" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "work" },
                    new InclusiveGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "a", "wantA"), F("split", "b"),
                    F("a", "join"), F("b", "join"), F("join", "e")
                }
            };
        }

        [TestMethod]
        public void APairedRegionIsValid()
        {
            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(Paired());

            Assert.IsFalse(issues.Any(i => i.Severity == ValidationSeverity.Error),
                "a properly paired split/join is exactly what this gateway is for: "
                + string.Join(" | ", issues.Select(i => i.Message)));
        }

        [TestMethod]
        public void ABranchThatMissesTheJoin_IsRejected()
        {
            WorkflowDefinition def = Paired();
            // Zweig b geht am Join vorbei direkt ans Ende - der Join wartet dann ewig auf zwei.
            def.Flows.RemoveAll(f => f.Id == "b->join");
            def.Flows.Add(F("b", "e"));

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "split"
                                          && i.Message.Contains("no matching join")),
                "a branch that misses its join is a hang, not a fault - it has to be caught while drawing.");
        }

        [TestMethod]
        public void AJoinWithoutASplitUpstream_IsRejected()
        {
            var def = new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "and" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "work" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "work" },
                    new InclusiveGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "and"), F("and", "a"), F("and", "b"),
                    F("a", "join"), F("b", "join"), F("join", "e")
                }
            };

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "join"
                                          && i.Message.Contains("no matching inclusive split")));
        }

        [TestMethod]
        public void ASplitWithoutAnyCondition_IsRejected()
        {
            WorkflowDefinition def = Paired();
            ((InclusiveGatewayNode)def.Nodes.Single(n => n.Id == "split")).DefaultFlowId = null;
            def.Flows.Single(f => f.Id == "split->a").Condition = null;

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "split"
                                          && i.Message.Contains("That is an AND gateway")),
                "without a single condition every branch is always taken - then it is an AND.");
        }

        [TestMethod]
        public void ASplitThatIsAlsoAJoin_IsRejected()
        {
            WorkflowDefinition def = Paired();
            // Eine zweite Kante in den Split: er waere Join und Split zugleich.
            def.Nodes.Add(new AutomatedActivityNode { Id = "extra", ActivityRef = "work" });
            def.Flows.Add(F("s", "extra"));
            def.Flows.Add(F("extra", "split"));

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "split"
                                          && i.Message.Contains("join and a split at the same time")));
        }

        [TestMethod]
        public void ASplitWithoutADefaultFlow_IsAWarning()
        {
            WorkflowDefinition def = Paired();
            ((InclusiveGatewayNode)def.Nodes.Single(n => n.Id == "split")).DefaultFlowId = null;
            def.Flows.Single(f => f.Id == "split->b").Condition = "wantB";

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsFalse(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "split"));
            Assert.IsTrue(issues.Any(i => i.NodeId == "split" && i.Message.Contains("no default flow")),
                "it is legal, but a run where nothing matches faults - that deserves a word.");
        }
    }
}
