using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft den EF-Core-basierten <see cref="EfWorkflowStore"/> gegen eine echte (In-Memory-)
    /// SQLite-Datenbank: Persistenz, Serialisierungs-Round-Trip, die Signal-/Timer-Abfragen und -
    /// als Kernbeweis - das Fortsetzen einer Instanz aus der Datenbank ueber einen frischen Store
    /// und eine frische Engine (Prozess-Neustart).
    /// </summary>
    [TestClass]
    public class EfWorkflowStoreTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;

        [TestInitialize]
        public void Setup()
        {
            // In-Memory-SQLite lebt nur, solange die Verbindung offen ist - daher wird sie hier
            // gehalten und alle Kontexte teilen sie sich (dieselbe Datenbank).
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using var ctx = new WorkflowContext(options);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            connection?.Dispose();
        }

        private EfWorkflowStore NewStore()
        {
            return new EfWorkflowStore(() => new WorkflowContext(options));
        }

        [TestMethod]
        public void InstanceResumesAfterReloadFromDatabase()
        {
            // Erste "Prozess-Lebensdauer": Workflow starten, er haelt am Wartepunkt und wird
            // persistiert.
            var store1 = NewStore();
            var activities1 = new ActivityRegistry().Register("finish", ctx => ctx.Variables["done"] = true);
            store1.SaveDefinition(WaitDefinition());
            WorkflowInstance started = new WorkflowEngine(store1, activities1).StartWorkflow("wait");

            Assert.AreEqual(WorkflowStatus.Waiting, started.Status);

            // Zweite "Prozess-Lebensdauer": frischer Store + frische Engine ueber dieselbe DB. Der
            // Zustand kommt ausschliesslich aus der Datenbank.
            var store2 = NewStore();
            var activities2 = new ActivityRegistry().Register("finish", ctx => ctx.Variables["done"] = true);
            var engine2 = new WorkflowEngine(store2, activities2);

            Assert.IsTrue(engine2.SignalWorkflow(started.Id, "approve"));

            WorkflowInstance reloaded = store2.GetInstance(started.Id);
            Assert.AreEqual(WorkflowStatus.Completed, reloaded.Status);
            Assert.AreEqual(true, reloaded.Variables["done"]);
        }

        [TestMethod]
        public void VariableTypesSurviveRoundTrip()
        {
            var store = NewStore();
            store.SaveDefinition(WaitDefinition());

            var engine = new WorkflowEngine(store,
                new ActivityRegistry().Register("finish", ctx => { }));
            WorkflowInstance started = engine.StartWorkflow("wait",
                new Dictionary<string, object> { { "amount", 150 }, { "label", "abc" } });

            // Aus der DB frisch geladen muessen int und string ihre Typen behalten.
            WorkflowInstance reloaded = store.GetInstance(started.Id);
            Assert.IsInstanceOfType<int>(reloaded.Variables["amount"], "int must round-trip as int, not long/JsonElement.");
            Assert.AreEqual(150, reloaded.Variables["amount"]);
            Assert.AreEqual("abc", reloaded.Variables["label"]);
        }

        [TestMethod]
        public void WaitingAndTimerQueriesFindTheRightInstances()
        {
            var store = NewStore();
            store.SaveDefinition(WaitDefinition());
            store.SaveDefinition(TimerDefinition());

            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("finish", ctx => { }));
            WorkflowInstance waiting = engine.StartWorkflow("wait", correlationKey: "K1");
            WorkflowInstance timing = engine.StartWorkflow("timer");

            List<WorkflowInstance> bySignal = store.FindWaitingForSignal("approve").ToList();
            Assert.AreEqual(1, bySignal.Count);
            Assert.AreEqual(waiting.Id, bySignal[0].Id);

            Assert.AreEqual(1, store.FindWaitingForSignal("approve", "K1").Count());
            Assert.AreEqual(0, store.FindWaitingForSignal("approve", "other").Count());

            Assert.AreEqual(0, store.FindDueTimers(DateTime.UtcNow).Count(), "Timer is not due yet.");
            List<WorkflowInstance> due = store.FindDueTimers(DateTime.UtcNow.AddHours(2)).ToList();
            Assert.AreEqual(1, due.Count);
            Assert.AreEqual(timing.Id, due[0].Id);
        }

        [TestMethod]
        public void DueTimerTriggeredThroughStoreCompletes()
        {
            var store = NewStore();
            store.SaveDefinition(TimerDefinition());
            var engine = new WorkflowEngine(store, new ActivityRegistry());
            WorkflowInstance timing = engine.StartWorkflow("timer");
            Assert.AreEqual(WorkflowStatus.Waiting, timing.Status);

            // Ueber den Store die faelligen Timer aufnehmen - wie es der Hintergrunddienst spaeter tut.
            engine.TriggerDueTimers(DateTime.UtcNow.AddHours(2));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(timing.Id).Status);
        }

        [TestMethod]
        public void DefinitionRoundTripsWithNodeTypes()
        {
            var store = NewStore();
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "shape",
                Version = 3,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ExclusiveGatewayNode { Id = "g", DefaultFlowId = "d" },
                    new ParallelGatewayNode { Id = "p" },
                    new WaitNode { Id = "w", SignalName = "x" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { new SequenceFlow { Id = "d", SourceId = "g", TargetId = "e" } }
            });

            WorkflowDefinition def = store.GetDefinition("shape");

            Assert.AreEqual(3, def.Version);
            Assert.IsInstanceOfType<ExclusiveGatewayNode>(def.GetNode("g"));
            Assert.IsInstanceOfType<ParallelGatewayNode>(def.GetNode("p"));
            Assert.AreEqual("x", ((WaitNode)def.GetNode("w")).SignalName);
            Assert.AreEqual("d", ((ExclusiveGatewayNode)def.GetNode("g")).DefaultFlowId);
        }

        [TestMethod]
        public void ActivityBindingsRoundTrip()
        {
            var store = NewStore();
            var activity = new AutomatedActivityNode { Id = "a", ActivityRef = "step" };
            activity.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "count", Kind = ParameterBindingKind.Literal, Literal = 7
            });
            activity.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "who", Kind = ParameterBindingKind.Variable, Source = "user"
            });
            activity.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "sum", Kind = ParameterBindingKind.Expression, Source = "a + b"
            });
            activity.Outputs.Add(new ActivityOutputBinding { Parameter = "result", Variable = "answer" });
            activity.ScopeMode = ActivityScopeMode.Replace;
            activity.RetainVariables.Add("tenant");

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "bindings",
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, activity, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->a", SourceId = "s", TargetId = "a" },
                    new SequenceFlow { Id = "a->e", SourceId = "a", TargetId = "e" }
                }
            });

            var reloaded = (AutomatedActivityNode)store.GetDefinition("bindings").GetNode("a");

            Assert.AreEqual(3, reloaded.Inputs.Count);
            ActivityInputBinding count = reloaded.Inputs.Single(i => i.Parameter == "count");
            Assert.AreEqual(ParameterBindingKind.Literal, count.Kind);
            Assert.IsInstanceOfType<int>(count.Literal, "the literal object value must round-trip as int.");
            Assert.AreEqual(7, count.Literal);
            Assert.AreEqual(ParameterBindingKind.Variable, reloaded.Inputs.Single(i => i.Parameter == "who").Kind);
            Assert.AreEqual("a + b", reloaded.Inputs.Single(i => i.Parameter == "sum").Source);

            Assert.AreEqual(1, reloaded.Outputs.Count);
            Assert.AreEqual("answer", reloaded.Outputs[0].Variable);

            Assert.AreEqual(ActivityScopeMode.Replace, reloaded.ScopeMode, "the scope mode must round-trip.");
            CollectionAssert.AreEquivalent(new[] { "tenant" }, reloaded.RetainVariables);
        }

        [TestMethod]
        public void DataFlowRunsThroughTheStore()
        {
            var store = NewStore();
            var activity = new AutomatedActivityNode { Id = "a", ActivityRef = "add" };
            activity.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "x", Kind = ParameterBindingKind.Variable, Source = "seed"
            });
            activity.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "y", Kind = ParameterBindingKind.Literal, Literal = 5
            });
            activity.Outputs.Add(new ActivityOutputBinding { Parameter = "sum", Variable = "total" });

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "dataflow",
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, activity, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->a", SourceId = "s", TargetId = "a" },
                    new SequenceFlow { Id = "a->e", SourceId = "a", TargetId = "e" }
                }
            });

            var activities = new ActivityRegistry().Register("add",
                ctx => ctx.Outputs["sum"] = (int)ctx.Inputs["x"] + (int)ctx.Inputs["y"]);
            WorkflowInstance instance = new WorkflowEngine(store, activities)
                .StartWorkflow("dataflow", new Dictionary<string, object> { { "seed", 10 } });

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(15, store.GetInstance(instance.Id).Variables["total"]);
        }

        [TestMethod]
        public void TryCommit_OptimisticConcurrency_FirstWinsSecondConflicts()
        {
            var store = NewStore();
            var inst = new WorkflowInstance
            {
                DefinitionId = "d", DefinitionVersion = 1, Status = WorkflowStatus.Running
            };
            store.SaveInstance(inst);

            // Zwei unabhaengige Ladevorgaenge (EF liefert Kopien) beim selben Stand.
            WorkflowInstance a = store.GetInstance(inst.Id);
            WorkflowInstance b = store.GetInstance(inst.Id);
            Assert.AreEqual(a.Version, b.Version);

            a.Variables["x"] = 1;
            Assert.IsTrue(store.TryCommitInstance(a, a.Version), "the first commit at the shared version wins.");

            b.Variables["y"] = 2;
            Assert.IsFalse(store.TryCommitInstance(b, b.Version),
                "the second commit at the now-stale version must conflict.");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.IsTrue(final.Variables.ContainsKey("x"), "the winning commit persisted.");
            Assert.IsFalse(final.Variables.ContainsKey("y"), "the conflicting commit was rejected.");
        }

        [TestMethod]
        public void MultipleTokens_RoundTripAsRows_AndInPlaceMerge()
        {
            var store = NewStore();
            var instance = new WorkflowInstance
            {
                DefinitionId = "d",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Waiting,
                Tokens = new List<Token>
                {
                    new Token { Id = "t1", NodeId = "w1", Status = TokenStatus.Waiting, WaitingSignal = "go" },
                    new Token { Id = "t2", NodeId = "n2", Status = TokenStatus.Active },
                    new Token { Id = "t3", NodeId = "e", Status = TokenStatus.Consumed }
                }
            };
            store.SaveInstance(instance);

            WorkflowInstance reloaded = store.GetInstance(instance.Id);
            Assert.AreEqual(3, reloaded.Tokens.Count, "each token must round-trip as its own row.");
            Assert.AreEqual("go", reloaded.Tokens.Single(t => t.Id == "t1").WaitingSignal);
            Assert.AreEqual(TokenStatus.Active, reloaded.Tokens.Single(t => t.Id == "t2").Status);

            // In-Place-Merge: ein Token verschwindet, eines wechselt den Status.
            instance.Tokens.RemoveAll(t => t.Id == "t3");
            instance.Tokens.Single(t => t.Id == "t2").Status = TokenStatus.Consumed;
            store.SaveInstance(instance);

            WorkflowInstance again = store.GetInstance(instance.Id);
            Assert.AreEqual(2, again.Tokens.Count, "the removed token must be gone from the rows.");
            Assert.AreEqual(TokenStatus.Consumed, again.Tokens.Single(t => t.Id == "t2").Status);
        }

        [TestMethod]
        public void BranchLock_ContendedAcrossStores_ReleasesAndReAcquires()
        {
            // Zwei getrennte Stores ueber DIESELBE DB = zwei Prozesse. Der Lock muss prozessuebergreifend
            // greifen.
            var storeA = NewStore();
            var storeB = NewStore();

            Stores.IWorkflowBranchLock a = storeA.TryAcquireBranchLock("inst", "tok", "runner-A");
            Assert.IsNotNull(a);

            Assert.IsNull(storeB.TryAcquireBranchLock("inst", "tok", "runner-B"),
                "another process must not lock the same held branch.");

            a.Dispose();

            using Stores.IWorkflowBranchLock b = storeB.TryAcquireBranchLock("inst", "tok", "runner-B");
            Assert.IsNotNull(b, "after release the branch is free for the other process.");
        }

        [TestMethod]
        public void BranchLock_ReleaseLocksOfOwner_ResetsOnlyThatOwner()
        {
            var store = NewStore();
            Assert.IsNotNull(store.TryAcquireBranchLock("i1", "t", "runner-A"));
            Assert.IsNotNull(store.TryAcquireBranchLock("i2", "t", "runner-A"));
            Assert.IsNotNull(store.TryAcquireBranchLock("i3", "t", "runner-B"));

            store.ReleaseLocksOfOwner("runner-A");

            Assert.IsNotNull(store.TryAcquireBranchLock("i1", "t", "runner-C"), "A's lock was reset.");
            Assert.IsNotNull(store.TryAcquireBranchLock("i2", "t", "runner-C"), "A's lock was reset.");
            Assert.IsNull(store.TryAcquireBranchLock("i3", "t", "runner-C"), "B's lock must be untouched.");
        }

        [TestMethod]
        public void WaitingForTarget_RoundTripsAndIsDiscoverable()
        {
            var store = NewStore();
            var instance = new WorkflowInstance
            {
                DefinitionId = "d",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Waiting,
                Tokens = new List<Token>
                {
                    new Token { Id = "t", NodeId = "r", Status = TokenStatus.WaitingForTarget, WaitingTarget = "backend" }
                }
            };
            store.SaveInstance(instance);

            // Round-Trip: Status und Zielname ueberleben die Token-Zeile.
            Token reloaded = store.GetInstance(instance.Id).Tokens.Single();
            Assert.AreEqual(TokenStatus.WaitingForTarget, reloaded.Status);
            Assert.AreEqual("backend", reloaded.WaitingTarget);

            // Discovery: nur die passende Zielmenge findet den Zweig.
            Assert.AreEqual(1, store.FindBranchesWaitingForTarget(new[] { "backend" }).Count());
            Assert.AreEqual(1, store.FindBranchesWaitingForTarget(new[] { "web", "backend" }).Count());
            Assert.AreEqual(0, store.FindBranchesWaitingForTarget(new[] { "web" }).Count());
            Assert.AreEqual(0, store.FindBranchesWaitingForTarget(new string[0]).Count(), "an empty target set finds nothing.");
        }

        [TestMethod]
        public void Handoff_ParkedOnOneHost_ResumedAndExecutedOnTargetHost_ThroughStore()
        {
            store_SaveHandoffDefinition(NewStore());

            // Host A (web) treibt den Start-Zweig voran und parkt den Backend-Schritt.
            var web = new WorkflowEngine(NewStore(),
                new ActivityRegistry().Register("mark", ctx => ctx.Variables["marked"] = true),
                hostTargets: new[] { "web" });
            WorkflowInstance inst = web.StartWorkflow("ho");
            Assert.AreEqual(WorkflowStatus.Waiting, NewStore().GetInstance(inst.Id).Status);
            Assert.AreEqual(TokenStatus.WaitingForTarget, NewStore().GetInstance(inst.Id).Tokens.Single().Status);

            // Host B (backend) - frischer Store + Engine (anderer Prozess) - nimmt den Zweig auf und fuehrt ihn aus.
            var backendStore = NewStore();
            var backend = new WorkflowEngine(backendStore,
                new ActivityRegistry().Register("mark", ctx => ctx.Variables["marked"] = true),
                hostTargets: new[] { "backend" });

            List<WorkflowInstance> found = backendStore.FindBranchesWaitingForTarget(backend.HostTargets).ToList();
            CollectionAssert.Contains(found.Select(i => i.Id).ToList(), inst.Id);

            IReadOnlyList<string> ids = backend.ReactivateForTargets(inst.Id, backend.HostTargets);
            Assert.AreEqual(1, ids.Count);
            backend.Advance(backendStore.GetInstance(inst.Id));

            WorkflowInstance final = NewStore().GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(true, final.Variables["marked"], "the backend host executed the handed-off activity.");
        }

        private static void store_SaveHandoffDefinition(EfWorkflowStore store)
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "ho",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "r", ActivityRef = "mark", ExecutionTarget = "backend" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->r", SourceId = "s", TargetId = "r" },
                    new SequenceFlow { Id = "r->e", SourceId = "r", TargetId = "e" }
                }
            });
        }

        private static WorkflowDefinition WaitDefinition()
        {
            return new WorkflowDefinition
            {
                Id = "wait",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "approve" },
                    new AutomatedActivityNode { Id = "f", ActivityRef = "finish" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->f", SourceId = "w", TargetId = "f" },
                    new SequenceFlow { Id = "f->e", SourceId = "f", TargetId = "e" }
                }
            };
        }

        private static WorkflowDefinition TimerDefinition()
        {
            return new WorkflowDefinition
            {
                Id = "timer",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new TimerNode { Id = "t", DueExpression = "'System.TimeSpan'.FromHours(1)" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->t", SourceId = "s", TargetId = "t" },
                    new SequenceFlow { Id = "t->e", SourceId = "t", TargetId = "e" }
                }
            };
        }
    }
}
