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
                DefinitionKey = store.GetDefinition("par", 1).Key,                DefinitionId = "par",
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

        [TestMethod]
        public void RunBranch_ParallelBranches_HistoryFromBothBranchesMergesAsRows()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            WorkflowInstance instance = TwoActiveBranches(store);

            engine.RunBranch(instance.Id, "t1");
            IReadOnlyList<string> new2 = engine.RunBranch(instance.Id, "t2");
            engine.RunBranch(instance.Id, new2[0]);

            WorkflowInstance final = store.GetInstance(instance.Id);
            // Beide Zweig-Deltas haben ihre Protokoll-Eintraege beigesteuert (append-only gemergt) …
            Assert.IsTrue(final.History.Any(h => h.NodeId == "a"), "branch A's history merged.");
            Assert.IsTrue(final.History.Any(h => h.NodeId == "b"), "branch B's history merged.");
            // … und der Join hat genau einmal gefeuert (kein doppelter Eintrag durch die Nebenlaeufigkeit).
            Assert.AreEqual(1, final.History.Count(h => h.Event == "ParallelJoin"),
                "the join must be logged exactly once.");
        }

        [TestMethod]
        public void RunBranch_BranchScopes_SurviveThePersistedRoundTrip()
        {
            // Der verteilte Weg: jeder Zweig wird EINZELN geladen, ausgefuehrt und committed. Der
            // Zweig-Scope muss diesen Weg mitmachen (eigene Spalte je Token-Zeile), sonst waere die
            // Isolation nur im In-Memory-Vortrieb wirksam.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(ParallelDefinition());

            WorkflowInstance instance = engine.CreateInstance("par");
            string startToken = instance.Tokens.Single().Id;

            IReadOnlyList<string> branches = engine.RunBranch(instance.Id, startToken);
            Assert.AreEqual(2, branches.Count, "the split spawned both branches.");

            WorkflowInstance forked = store.GetInstance(instance.Id);
            Assert.IsTrue(forked.Tokens.Where(t => branches.Contains(t.Id))
                    .All(t => t.Variables != null && t.SplitTokenId == startToken),
                "every branch carries its own (persisted) scope and knows which split it came from.");

            engine.RunBranch(instance.Id, branches[0]);

            WorkflowInstance midway = store.GetInstance(instance.Id);
            Assert.IsFalse(midway.Variables.ContainsKey("a"),
                "while the region is open the branch result stays in the branch.");
            Assert.AreEqual(1, midway.Tokens.Single(t => t.Id == branches[0]).Variables["a"],
                "the branch write came back out of the database.");

            IReadOnlyList<string> continuation = engine.RunBranch(instance.Id, branches[1]);
            Assert.AreEqual(1, continuation.Count, "the last arriver fires the join.");
            engine.RunBranch(instance.Id, continuation[0]);

            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(1, final.Variables["a"], "branch A's write merged at the join.");
            Assert.AreEqual(2, final.Variables["b"], "branch B's write merged at the join.");
            Assert.IsTrue(final.Tokens.All(t => t.Variables == null),
                "after the join no branch scope is left behind.");
        }

        [TestMethod]
        public void RunBranch_ConcurrentWriteToSameVariable_DifferentValue_Faults()
        {
            EfWorkflowStore inner = NewStore();
            inner.SaveDefinition(WriteXDefinition());
            WorkflowInstance instance = ActiveAtWriteX(inner);

            // Zwischen Fork und Commit schreibt ein Geschwister-Zweig x=1; unser Zweig will x=2 schreiben.
            var store = new SiblingWriteInjectingStore(inner, instance.Id, "x", 1);
            var engine = new WorkflowEngine(store,
                new ActivityRegistry().Register("setX", ctx => ctx.Variables["x"] = 2));

            engine.RunBranch(instance.Id, "t");

            WorkflowInstance final = inner.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status, "a conflicting concurrent write must fault.");
            StringAssert.Contains(final.FaultMessage, "x");
            Assert.AreEqual(1, final.Variables["x"], "the sibling's value is kept - our write did NOT silently overwrite it.");
        }

        [TestMethod]
        public void RunBranch_ConcurrentWriteToSameVariable_SameValue_IsNotAConflict()
        {
            EfWorkflowStore inner = NewStore();
            inner.SaveDefinition(WriteXDefinition());
            WorkflowInstance instance = ActiveAtWriteX(inner);

            // Geschwister und wir schreiben denselben Wert (1) - ordnungs-unabhaengig, kein Fault.
            var store = new SiblingWriteInjectingStore(inner, instance.Id, "x", 1);
            var engine = new WorkflowEngine(store,
                new ActivityRegistry().Register("setX", ctx => ctx.Variables["x"] = 1));

            engine.RunBranch(instance.Id, "t");

            WorkflowInstance final = inner.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status, "identical values are not a conflict.");
            Assert.AreEqual(1, final.Variables["x"]);
        }

        [TestMethod]
        public void RunBranch_UserTaskStamp_SurvivesTheBranchCommit()
        {
            // Der Stempel entsteht WAEHREND des Zweig-Vortriebs (das Token parkt an der Aufgabe) und muss
            // deshalb ueber das Zweig-Delta auf den frisch geladenen Stand kommen. Uebertraegt der Merge nur
            // eine Teilmenge der Felder, steht das Token danach zwar korrekt auf dem Aufgaben-Knoten und
            // wartet - aber ohne Aufgabenart. Es ist dann in KEINER Arbeitsliste sichtbar (weder "meine"
            // noch "nicht zugewiesen"), und der Fehler faellt nirgends auf.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(UserTaskDefinition());

            WorkflowInstance instance = engine.CreateInstance("ut",
                new Dictionary<string, object> { { "owner", "anna" } });
            engine.RunBranch(instance.Id, instance.Tokens.Single().Id);

            WorkflowInstance parked = store.GetInstance(instance.Id);
            Token task = parked.Tokens.Single(t => t.Status == TokenStatus.Waiting);
            Assert.AreEqual("u", task.NodeId, "the branch parked on the user task.");
            Assert.AreEqual("ApproveInvoice", task.TaskKey, "the task kind must survive the branch commit.");
            Assert.AreEqual("Invoice.Approve", task.TaskPermission);
            Assert.AreEqual("anna", task.AssignedTo);
            Assert.AreEqual("Rechnung freigeben", task.TaskTitle);
            Assert.IsNotNull(task.TaskCreatedUtc);
            Assert.IsNotNull(task.TaskDueUtc);
        }

        [TestMethod]
        public void RunBranch_BoundaryTimerLink_SurvivesTheBranchCommit()
        {
            // Dasselbe fuer den Fristen-Timer: sein Token entsteht ebenfalls erst im Zweig-Vortrieb. Ohne
            // die Verknuepfung zum Haupt-Token raeumt niemand es wieder ab - die Instanz koennte nie
            // abschliessen.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(UserTaskDefinition(withBoundaryTimer: true));

            WorkflowInstance instance = engine.CreateInstance("ut",
                new Dictionary<string, object> { { "owner", "anna" } });
            engine.RunBranch(instance.Id, instance.Tokens.Single().Id);

            WorkflowInstance parked = store.GetInstance(instance.Id);
            Token owner = parked.Tokens.Single(t => t.NodeId == "u");
            Token timer = parked.Tokens.Single(t => t.NodeId == "bt");
            Assert.AreEqual(owner.Id, timer.BoundaryOwnerTokenId,
                "the timer must still know whose deadline it is watching.");
            Assert.AreEqual(0, timer.BoundaryIteration);
            Assert.IsNotNull(timer.DueUtc);
        }

        private static WorkflowDefinition UserTaskDefinition(bool withBoundaryTimer = false)
        {
            var nodes = new List<WorkflowNode>
            {
                new StartNode { Id = "s" },
                new UserActivityNode
                {
                    Id = "u",
                    TaskKey = "ApproveInvoice",
                    RequiredPermission = "Invoice.Approve",
                    Assignment = "owner",
                    Title = "Rechnung freigeben",
                    DueInHours = 24
                },
                new EndNode { Id = "e" }
            };
            var flows = new List<SequenceFlow> { Flow("s", "u"), Flow("u", "e") };

            if (withBoundaryTimer)
            {
                nodes.Add(new BoundaryTimerNode
                {
                    Id = "bt", AttachedToNodeId = "u", IntervalsInHours = new List<double> { 4 }
                });
                nodes.Add(new SidePathEndNode { Id = "se" });
                flows.Add(Flow("bt", "se"));
            }

            return new WorkflowDefinition { Id = "ut", Version = 1, Nodes = nodes, Flows = flows };
        }

        private static WorkflowInstance ActiveAtWriteX(IWorkflowStore store)
        {
            var instance = new WorkflowInstance
            {
                DefinitionKey = store.GetDefinition("wx", 1).Key,                DefinitionId = "wx",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Running,
                Tokens = new List<Token> { new Token { Id = "t", NodeId = "w", Status = TokenStatus.Active } }
            };
            store.SaveInstance(instance);
            return instance;
        }

        private static WorkflowDefinition WriteXDefinition()
        {
            return new WorkflowDefinition
            {
                Id = "wx",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "w", ActivityRef = "setX" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "w"), Flow("w", "e") }
            };
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
            public WorkflowDefinition GetDefinition(string id, int? version = null, string tenantId = null)
                => inner.GetDefinition(id, version, tenantId);

            public WorkflowDefinition GetDefinition(int definitionKey) => inner.GetDefinition(definitionKey);
            public void SaveInstance(WorkflowInstance instance) => inner.SaveInstance(instance);
            public WorkflowInstance GetInstance(string instanceId) => inner.GetInstance(instanceId);

            public int? GetInstancePriority(string instanceId) => inner.GetInstancePriority(instanceId);
            public IEnumerable<WorkflowInstance> FindWaitingForSignal(string s, string c = null) => inner.FindWaitingForSignal(s, c);
            public IEnumerable<WorkflowInstance> FindWaitingForBroadcast(string s) => inner.FindWaitingForBroadcast(s);
            public IEnumerable<WorkflowInstance> FindDueTimers(DateTime now) => inner.FindDueTimers(now);
            public IEnumerable<WorkflowInstance> ClaimDueTimers(DateTime now, string o, TimeSpan l, int m) => inner.ClaimDueTimers(now, o, l, m);
            public DateTime? PeekNextTimerDueUtc(DateTime now) => inner.PeekNextTimerDueUtc(now);
            public IEnumerable<WorkflowInstance> FindBranchesWaitingForTarget(IEnumerable<string> targets) => inner.FindBranchesWaitingForTarget(targets);
            public IEnumerable<WorkflowInstance> FindRunnable() => inner.FindRunnable();
            public IEnumerable<WorkflowInstance> FindChildInstances(string parentInstanceId) => inner.FindChildInstances(parentInstanceId);
            public IEnumerable<WorkflowInstance> FindFinishedChildrenWithWaitingParent() => inner.FindFinishedChildrenWithWaitingParent();
            public IWorkflowBranchLock TryAcquireBranchLock(string i, string t, string o) => inner.TryAcquireBranchLock(i, t, o);
            public IReadOnlyList<OutgoingMessage> ClaimOutgoingMessages(string owner, TimeSpan lease,
                int maxMessages) => inner.ClaimOutgoingMessages(owner, lease, maxMessages);

            public void CompleteOutgoingMessage(string instanceId, string messageId)
                => inner.CompleteOutgoingMessage(instanceId, messageId);

            public void ReleaseLocksOfOwner(string owner) => inner.ReleaseLocksOfOwner(owner);

            // Die Ausloeser interessieren diesen Decorator nicht - er verstellt das Commit-Verhalten.
            // Durchreichen statt werfen: was er nicht faelscht, soll sich normal verhalten.
            public WorkflowMessageTriggerLookup FindMessageTriggers(string signalName, string originTenantId)
                => inner.FindMessageTriggers(signalName, originTenantId);

            public IReadOnlyList<WorkflowStartTriggerMatch> ClaimDueScheduleTriggers(DateTime now, string o,
                TimeSpan l, int m) => inner.ClaimDueScheduleTriggers(now, o, l, m);

            public void UpdateScheduleActivation(int activationKey, DateTime? nextDueUtc,
                DateTime? lastRunUtc, string lastInstanceId)
                => inner.UpdateScheduleActivation(activationKey, nextDueUtc, lastRunUtc, lastInstanceId);

            public int? ResolveDefinitionKey(string ownerTenantId, string definitionId, int? version = null)
                => inner.ResolveDefinitionKey(ownerTenantId, definitionId, version);

            public IReadOnlyList<WorkflowStartTrigger> FindActivatableTriggers(string tenantId)
                => inner.FindActivatableTriggers(tenantId);

            public IReadOnlyList<WorkflowStartTriggerActivation> GetActivations(string tenantId)
                => inner.GetActivations(tenantId);

            public void SaveActivation(WorkflowStartTriggerActivation activation)
                => inner.SaveActivation(activation);

            public DateTime? PeekNextScheduleDueUtc(DateTime now) => inner.PeekNextScheduleDueUtc(now);

            public bool HasRunningInstance(int definitionKey, string correlationKey)
                => inner.HasRunningInstance(definitionKey, correlationKey);
        }

        /// <summary>
        /// Ein Store-Decorator, der einen nebenlaeufigen Geschwister-Schreibzugriff simuliert: beim ZWEITEN
        /// GetInstance derselben Instanz (= das Neuladen von RunBranch fuers Commit, nachdem der Zweig
        /// gegen den Fork-Stand OHNE die Variable ausgefuehrt hat) committet er zuvor eine Variable - so als
        /// haette ein paralleler Zweig zwischen Fork und Commit geschrieben.
        /// </summary>
        private sealed class SiblingWriteInjectingStore : IWorkflowStore
        {
            private readonly IWorkflowStore inner;
            private readonly string instanceId;
            private readonly string variable;
            private readonly object siblingValue;
            private int getCount;
            private bool injected;

            public SiblingWriteInjectingStore(IWorkflowStore inner, string instanceId, string variable, object siblingValue)
            {
                this.inner = inner;
                this.instanceId = instanceId;
                this.variable = variable;
                this.siblingValue = siblingValue;
            }

            public WorkflowInstance GetInstance(string id)
            {
                WorkflowInstance instance = inner.GetInstance(id);
                if (id == instanceId)
                {
                    getCount++;
                    if (getCount == 2 && !injected)
                    {
                        injected = true;
                        WorkflowInstance sibling = inner.GetInstance(id);
                        sibling.Variables[variable] = siblingValue;
                        inner.SaveInstance(sibling); // committet den Geschwister-Schreibzugriff (Version steigt).
                        instance = inner.GetInstance(id);
                    }
                }

                return instance;
            }

            public int? GetInstancePriority(string id) => inner.GetInstancePriority(id);
            public bool TryCommitInstance(WorkflowInstance instance, int baseVersion) => inner.TryCommitInstance(instance, baseVersion);
            public void SaveDefinition(WorkflowDefinition definition) => inner.SaveDefinition(definition);
            public WorkflowDefinition GetDefinition(string id, int? version = null, string tenantId = null)
                => inner.GetDefinition(id, version, tenantId);

            public WorkflowDefinition GetDefinition(int definitionKey) => inner.GetDefinition(definitionKey);
            public void SaveInstance(WorkflowInstance instance) => inner.SaveInstance(instance);
            public IEnumerable<WorkflowInstance> FindWaitingForSignal(string s, string c = null) => inner.FindWaitingForSignal(s, c);
            public IEnumerable<WorkflowInstance> FindWaitingForBroadcast(string s) => inner.FindWaitingForBroadcast(s);
            public IEnumerable<WorkflowInstance> FindDueTimers(DateTime now) => inner.FindDueTimers(now);
            public IEnumerable<WorkflowInstance> ClaimDueTimers(DateTime now, string o, TimeSpan l, int m) => inner.ClaimDueTimers(now, o, l, m);
            public DateTime? PeekNextTimerDueUtc(DateTime now) => inner.PeekNextTimerDueUtc(now);
            public IEnumerable<WorkflowInstance> FindBranchesWaitingForTarget(IEnumerable<string> targets) => inner.FindBranchesWaitingForTarget(targets);
            public IEnumerable<WorkflowInstance> FindRunnable() => inner.FindRunnable();
            public IEnumerable<WorkflowInstance> FindChildInstances(string parentInstanceId) => inner.FindChildInstances(parentInstanceId);
            public IEnumerable<WorkflowInstance> FindFinishedChildrenWithWaitingParent() => inner.FindFinishedChildrenWithWaitingParent();
            public IWorkflowBranchLock TryAcquireBranchLock(string i, string t, string o) => inner.TryAcquireBranchLock(i, t, o);
            public IReadOnlyList<OutgoingMessage> ClaimOutgoingMessages(string owner, TimeSpan lease,
                int maxMessages) => inner.ClaimOutgoingMessages(owner, lease, maxMessages);

            public void CompleteOutgoingMessage(string instanceId, string messageId)
                => inner.CompleteOutgoingMessage(instanceId, messageId);

            public void ReleaseLocksOfOwner(string owner) => inner.ReleaseLocksOfOwner(owner);

            // Die Ausloeser interessieren diesen Decorator nicht - er verstellt das Commit-Verhalten.
            // Durchreichen statt werfen: was er nicht faelscht, soll sich normal verhalten.
            public WorkflowMessageTriggerLookup FindMessageTriggers(string signalName, string originTenantId)
                => inner.FindMessageTriggers(signalName, originTenantId);

            public IReadOnlyList<WorkflowStartTriggerMatch> ClaimDueScheduleTriggers(DateTime now, string o,
                TimeSpan l, int m) => inner.ClaimDueScheduleTriggers(now, o, l, m);

            public void UpdateScheduleActivation(int activationKey, DateTime? nextDueUtc,
                DateTime? lastRunUtc, string lastInstanceId)
                => inner.UpdateScheduleActivation(activationKey, nextDueUtc, lastRunUtc, lastInstanceId);

            public int? ResolveDefinitionKey(string ownerTenantId, string definitionId, int? version = null)
                => inner.ResolveDefinitionKey(ownerTenantId, definitionId, version);

            public IReadOnlyList<WorkflowStartTrigger> FindActivatableTriggers(string tenantId)
                => inner.FindActivatableTriggers(tenantId);

            public IReadOnlyList<WorkflowStartTriggerActivation> GetActivations(string tenantId)
                => inner.GetActivations(tenantId);

            public void SaveActivation(WorkflowStartTriggerActivation activation)
                => inner.SaveActivation(activation);

            public DateTime? PeekNextScheduleDueUtc(DateTime now) => inner.PeekNextScheduleDueUtc(now);

            public bool HasRunningInstance(int definitionKey, string correlationKey)
                => inner.HasRunningInstance(definitionKey, correlationKey);
        }
    }
}
