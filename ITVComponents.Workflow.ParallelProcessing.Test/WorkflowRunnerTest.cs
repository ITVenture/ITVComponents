using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ITVComponents.Workflow;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.ParallelProcessing.Test
{
    /// <summary>
    /// Prueft den zweig-granularen <see cref="WorkflowTaskWorker"/> deterministisch (ohne Threads): ein
    /// Auftrag wird verarbeitet, die daraus entstehenden Folge-Zweige werden bis zur Ruhe abgearbeitet
    /// (Drain). Nutzt den EF-Store (Kopien - Voraussetzung fuer RunBranch).
    /// </summary>
    [TestClass]
    public class WorkflowTaskWorkerTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;
        private WorkflowEngine engine;
        private Queue<WorkflowTask> queue;
        private WorkflowTaskWorker worker;

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
            engine = new WorkflowEngine(store,
                new ActivityRegistry().Register("finish", ctx => ctx.Variables["done"] = true));
            queue = new Queue<WorkflowTask>();
            worker = new WorkflowTaskWorker(engine, store, "runner-test", t => queue.Enqueue(t));
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        /// <summary>Verarbeitet den Task und alle daraus entstehenden Folge-Zweige (deterministisch, ohne Threads).</summary>
        private void ProcessAll(WorkflowTask task)
        {
            queue.Enqueue(task);
            int guard = 0;
            while (queue.Count > 0)
            {
                if (++guard > 1000)
                {
                    Assert.Fail("task drain did not terminate.");
                }

                worker.Process(queue.Dequeue());
            }
        }

        /// <summary>Legt die Instanz an und treibt ihren Start-Zweig bis zur ersten Barriere voran.</summary>
        private WorkflowInstance StartAndAdvance(string definitionId)
        {
            WorkflowInstance instance = engine.CreateInstance(definitionId);
            ProcessAll(new WorkflowTask(instance.Id, WorkflowTrigger.Advance, instance.Tokens[0].Id));
            return instance;
        }

        [TestMethod]
        public void SignalTask_ReactivatesAndBranchCompletesInstance()
        {
            store.SaveDefinition(WorkflowDefinitions.Wait("approve"));
            WorkflowInstance instance = StartAndAdvance("wait");
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(instance.Id).Status);

            ProcessAll(new WorkflowTask(instance.Id, WorkflowTrigger.Signal, null, "approve"));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(instance.Id).Status);
        }

        [TestMethod]
        public void TimerTask_ReactivatesAndBranchCompletesInstance()
        {
            store.SaveDefinition(WorkflowDefinitions.Timer("'System.DateTime'.UtcNow"));
            WorkflowInstance instance = StartAndAdvance("timer");
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(instance.Id).Status);

            ProcessAll(new WorkflowTask(instance.Id, WorkflowTrigger.Timer));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(instance.Id).Status);
        }

        [TestMethod]
        public void AdvanceTask_OnWaitingBranch_IsNoOp()
        {
            store.SaveDefinition(WorkflowDefinitions.Wait("approve"));
            WorkflowInstance instance = StartAndAdvance("wait");

            // Der Start-Token wartet jetzt am Wait-Knoten; ein Advance-Task dafuer ist ein Leerlauf.
            worker.Process(new WorkflowTask(instance.Id, WorkflowTrigger.Advance, instance.Tokens[0].Id));

            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(instance.Id).Status);
        }
    }

    /// <summary>
    /// Prueft den <see cref="WorkflowRunner"/> als laufenden Dienst gegen eine echte (Datei-)
    /// SQLite-Datenbank - nebenlaeufig, mit Warten auf das Ergebnis.
    /// </summary>
    [TestClass]
    public class WorkflowRunnerTest
    {
        private string dbFile;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;
        private WorkflowEngine engine;
        private WorkflowRunner runner;

        [TestInitialize]
        public void Setup()
        {
            dbFile = Path.GetTempFileName();
            options = new DbContextOptionsBuilder<WorkflowContext>()
                .UseSqlite($"DataSource={dbFile}").Options;
            using (var ctx = new WorkflowContext(options))
            {
                ctx.Database.EnsureCreated();
                // WAL erlaubt gleichzeitiges Lesen (Poll) und Schreiben (Worker) ohne Sperrfehler.
                ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            }

            store = new EfWorkflowStore(() => new WorkflowContext(options));
            var activities = new ActivityRegistry()
                .Register("finish", ctx => ctx.Variables["done"] = true)
                .Register("setA", ctx => ctx.Variables["a"] = 1)
                .Register("setB", ctx => ctx.Variables["b"] = 2)
                .Register("after", ctx =>
                    ctx.Variables["after"] = (ctx.Variables.TryGetValue("after", out object r) ? (int)r : 0) + 1);
            engine = new WorkflowEngine(store, activities);
            runner = new WorkflowRunner(engine, store,
                new WorkflowRunnerOptions { WorkerCount = 2, PollTimeMs = 100 });
            runner.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            runner?.Dispose();
            TryDelete(dbFile);
            TryDelete(dbFile + "-wal");
            TryDelete(dbFile + "-shm");
        }

        [TestMethod]
        public void RunnerDeliversSignalAndCompletes()
        {
            store.SaveDefinition(WorkflowDefinitions.Wait("approve"));
            // Ueber den Runner starten (nebenlaeufig): der Runner treibt den Start-Zweig bis zum Wait.
            WorkflowInstance instance = runner.StartWorkflow("wait");
            Assert.IsTrue(WaitUntil(() => store.GetInstance(instance.Id)?.Status == WorkflowStatus.Waiting),
                "the runner should advance the start branch to the wait point.");

            runner.Signal(instance.Id, "approve");

            bool ok = WaitUntil(() => store.GetInstance(instance.Id).Status == WorkflowStatus.Completed);
            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.IsTrue(ok, $"Status={final.Status}, Fault={final.FaultMessage}, Tokens={final.Tokens.Count}");
        }

        [TestMethod]
        public void RunnerDispatchesDueTimer()
        {
            store.SaveDefinition(WorkflowDefinitions.Timer("'System.DateTime'.UtcNow"));
            WorkflowInstance instance = runner.StartWorkflow("timer");

            // Der Runner treibt zum Timer, der Poll nimmt ihn (faellig) auf und schliesst ab.
            Assert.IsTrue(WaitUntil(() => store.GetInstance(instance.Id).Status == WorkflowStatus.Completed),
                "The runner's poll should have picked up the due timer and completed the instance.");
        }

        [TestMethod]
        public void RunnerRunsParallelBranchesToCompletion()
        {
            store.SaveDefinition(WorkflowDefinitions.Parallel());
            WorkflowInstance instance = runner.StartWorkflow("par");

            Assert.IsTrue(WaitUntil(() => store.GetInstance(instance.Id).Status == WorkflowStatus.Completed),
                "the runner should advance the parallel branches (concurrently) and complete via the join.");

            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.AreEqual(1, final.Variables["a"], "branch A ran.");
            Assert.AreEqual(2, final.Variables["b"], "branch B ran.");
            Assert.AreEqual(1, final.Variables["after"], "the join continuation ran exactly once.");
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = 15000)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(25);
            }

            return condition();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                // Aufraeumen ist Best-Effort; ein gesperrtes File soll den Test nicht rot faerben.
                Console.WriteLine($"Could not delete '{path}': {ex.Message}");
            }
        }
    }

    /// <summary>Kleine Definitions-Bausteine fuer die Tests.</summary>
    internal static class WorkflowDefinitions
    {
        public static WorkflowDefinition Wait(string signal)
        {
            return new WorkflowDefinition
            {
                TechnicalName = "wait",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = signal },
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

        public static WorkflowDefinition Timer(string dueExpression)
        {
            return new WorkflowDefinition
            {
                TechnicalName = "timer",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new TimerNode { Id = "t", DueExpression = dueExpression },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->t", SourceId = "s", TargetId = "t" },
                    new SequenceFlow { Id = "t->e", SourceId = "t", TargetId = "e" }
                }
            };
        }

        /// <summary>Start -&gt; AND-Split -&gt; (setA | setB) -&gt; AND-Join -&gt; after -&gt; End.</summary>
        public static WorkflowDefinition Parallel()
        {
            return new WorkflowDefinition
            {
                TechnicalName = "par",
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
                    new SequenceFlow { Id = "s->p", SourceId = "s", TargetId = "p" },
                    new SequenceFlow { Id = "p->a", SourceId = "p", TargetId = "a" },
                    new SequenceFlow { Id = "p->b", SourceId = "p", TargetId = "b" },
                    new SequenceFlow { Id = "a->j", SourceId = "a", TargetId = "j" },
                    new SequenceFlow { Id = "b->j", SourceId = "b", TargetId = "j" },
                    new SequenceFlow { Id = "j->af", SourceId = "j", TargetId = "af" },
                    new SequenceFlow { Id = "af->e", SourceId = "af", TargetId = "e" }
                }
            };
        }
    }
}
