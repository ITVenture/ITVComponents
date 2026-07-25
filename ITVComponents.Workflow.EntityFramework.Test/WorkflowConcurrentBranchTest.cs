using System;
using System.Collections.Generic;
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
    /// Prueft den nebenlaeufigen Zweig-Vortrieb <see cref="WorkflowEngine.RunBranch"/>: Snapshot-Diff des
    /// Zweig-Deltas, optimistischer Commit mit Retry, und der Join-Fire im (serialisierten) Commit gegen
    /// frischen Stand. Nutzt den EF-Store (liefert Kopien - Voraussetzung fuer das Load-Execute-Commit-Modell).
    /// </summary>
    [TestClass]
    public class WorkflowConcurrentBranchTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using var ctx = new WorkflowContext(options);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private EfWorkflowStore NewStore() => new EfWorkflowStore(() => new WorkflowContext(options));

        private static WorkflowEngine EngineOver(IWorkflowStore store)
        {
            var activities = new ActivityRegistry()
                .Register("setA", ctx => ctx.Variables["a"] = 1)
                .Register("setB", ctx => ctx.Variables["b"] = 2)
                .Register("after", ctx =>
                    ctx.Variables["after"] = (ctx.Variables.TryGetValue("after", out object r) ? (int)r : 0) + 1);
            return new WorkflowEngine(store, activities);
        }

        private static WorkflowInstance TwoActiveBranches(IWorkflowStore store)
        {
            store.SaveDefinition(ParallelDefinition());
            var instance = new WorkflowInstance
            {
                DefinitionId = "par",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Running,
                Tokens = new List<Token>
                {
                    new Token { Id = "t1", NodeId = "a", Status = TokenStatus.Active },
                    new Token { Id = "t2", NodeId = "b", Status = TokenStatus.Active }
                }
            };
            store.SaveInstance(instance);
            return instance;
        }

        [TestMethod]
        public void RunBranch_ParallelBranches_JoinFiresOnce_AndCompletes()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            WorkflowInstance instance = TwoActiveBranches(store);

            // Zweig 1: setA -> parkt am Join (keine neuen aktiven Tokens).
            IReadOnlyList<string> new1 = engine.RunBranch(instance.Id, "t1");
            Assert.AreEqual(0, new1.Count, "the first branch just parks at the join.");

            // Zweig 2 (letzter Ankoemmling): setB -> Join feuert am Commit -> Fortsetzung.
            IReadOnlyList<string> new2 = engine.RunBranch(instance.Id, "t2");
            Assert.AreEqual(1, new2.Count, "the last arriver fires the join and spawns exactly one continuation.");

            // Fortsetzung: after -> Ende.
            IReadOnlyList<string> new3 = engine.RunBranch(instance.Id, new2[0]);
            Assert.AreEqual(0, new3.Count);

            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(1, final.Variables["a"], "branch A's write merged.");
            Assert.AreEqual(2, final.Variables["b"], "branch B's write merged.");
            Assert.AreEqual(1, final.Variables["after"], "the join continuation ran exactly once.");
        }

        [TestMethod]
        public void RunBranch_RetriesOnVersionConflict_AndStillMergesOnce()
        {
            EfWorkflowStore inner = NewStore();
            // Erzwingt EINEN Versionskonflikt beim Commit von t2 -> RunBranch muss neu laden + Delta erneut
            // anwenden (ohne die Aktivitaet erneut auszufuehren) und dann committen.
            var store = new ConflictInjectingStore(inner, conflictsToInject: 1);
            WorkflowEngine engine = EngineOver(store);
            WorkflowInstance instance = TwoActiveBranches(store);

            engine.RunBranch(instance.Id, "t1");
            IReadOnlyList<string> new2 = engine.RunBranch(instance.Id, "t2"); // 1 erzwungener Retry
            Assert.AreEqual(1, new2.Count);
            engine.RunBranch(instance.Id, new2[0]);

            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(1, final.Variables["a"]);
            Assert.AreEqual(2, final.Variables["b"]);
            Assert.AreEqual(1, final.Variables["after"], "despite the retry the continuation ran exactly once.");
        }

        private static WorkflowDefinition ParallelDefinition()
        {
            return new WorkflowDefinition
            {
                Id = "par",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "p" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "setA" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "setB" },
                    new ParallelGatewayNode { Id = "j" },
                    new AutomatedActivityNode { Id = "af", ActivityRef = "after" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "p"), Flow("p", "a"), Flow("p", "b"),
                    Flow("a", "j"), Flow("b", "j"), Flow("j", "af"), Flow("af", "e")
                }
            };
        }

        private static SequenceFlow Flow(string from, string to)
            => new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>Ein Store-Decorator, der die ersten n TryCommitInstance-Aufrufe als Konflikt meldet.</summary>
        private sealed class ConflictInjectingStore : IWorkflowStore
        {
            private readonly IWorkflowStore inner;
            private int conflictsToInject;

            public ConflictInjectingStore(IWorkflowStore inner, int conflictsToInject)
            {
                this.inner = inner;
                this.conflictsToInject = conflictsToInject;
            }

            public bool TryCommitInstance(WorkflowInstance instance, int baseVersion)
            {
                if (conflictsToInject > 0)
                {
                    conflictsToInject--;
                    return false; // Konflikt vortaeuschen: NICHT committen (Version bleibt unveraendert).
                }

                return inner.TryCommitInstance(instance, baseVersion);
            }

            public void SaveDefinition(WorkflowDefinition definition) => inner.SaveDefinition(definition);
            public WorkflowDefinition GetDefinition(string id, int? version = null) => inner.GetDefinition(id, version);
            public void SaveInstance(WorkflowInstance instance) => inner.SaveInstance(instance);
            public WorkflowInstance GetInstance(string instanceId) => inner.GetInstance(instanceId);
            public IEnumerable<WorkflowInstance> FindWaitingForSignal(string s, string c = null) => inner.FindWaitingForSignal(s, c);
            public IEnumerable<WorkflowInstance> FindDueTimers(DateTime now) => inner.FindDueTimers(now);
            public IEnumerable<WorkflowInstance> FindRunnable() => inner.FindRunnable();
            public IWorkflowBranchLock TryAcquireBranchLock(string i, string t, string o) => inner.TryAcquireBranchLock(i, t, o);
            public void ReleaseLocksOfOwner(string owner) => inner.ReleaseLocksOfOwner(owner);
        }
    }
}
