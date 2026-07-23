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
