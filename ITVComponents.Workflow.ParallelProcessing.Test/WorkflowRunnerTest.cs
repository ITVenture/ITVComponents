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
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.ParallelProcessing.Test
{
    /// <summary>
    /// Prueft den <see cref="WorkflowTaskWorker"/> deterministisch (ohne Threads): ein Auftrag wird
    /// direkt verarbeitet.
    /// </summary>
    [TestClass]
    public class WorkflowTaskWorkerTest
    {
        private InMemoryWorkflowStore store;
        private WorkflowEngine engine;
        private WorkflowTaskWorker worker;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            var activities = new ActivityRegistry().Register("finish", ctx => ctx.Variables["done"] = true);
            engine = new WorkflowEngine(store, activities);
            worker = new WorkflowTaskWorker(engine, store, new InstanceGate());
        }

        [TestMethod]
        public void SignalTaskCompletesInstance()
        {
            store.SaveDefinition(WorkflowDefinitions.Wait("approve"));
            WorkflowInstance instance = engine.StartWorkflow("wait");
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);

            worker.Process(new WorkflowTask(instance.Id, WorkflowTrigger.Signal, "approve"));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(instance.Id).Status);
        }

        [TestMethod]
        public void TimerTaskCompletesDueInstance()
        {
            store.SaveDefinition(WorkflowDefinitions.Timer("'System.DateTime'.UtcNow"));
            WorkflowInstance instance = engine.StartWorkflow("timer");
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);

            worker.Process(new WorkflowTask(instance.Id, WorkflowTrigger.Timer));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(instance.Id).Status);
        }

        [TestMethod]
        public void AdvanceTaskLeavesWaitingInstanceUntouched()
        {
            store.SaveDefinition(WorkflowDefinitions.Wait("approve"));
            WorkflowInstance instance = engine.StartWorkflow("wait");

            worker.Process(new WorkflowTask(instance.Id, WorkflowTrigger.Advance));

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
            var activities = new ActivityRegistry().Register("finish", ctx => ctx.Variables["done"] = true);
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
            WorkflowInstance instance = engine.StartWorkflow("wait");
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);

            runner.Signal(instance.Id, "approve");

            bool ok = WaitUntil(() => store.GetInstance(instance.Id).Status == WorkflowStatus.Completed);
            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.IsTrue(ok, $"Status={final.Status}, Fault={final.FaultMessage}, Tokens={final.Tokens.Count}");
        }

        [TestMethod]
        public void RunnerDispatchesDueTimer()
        {
            store.SaveDefinition(WorkflowDefinitions.Timer("'System.DateTime'.UtcNow"));
            WorkflowInstance instance = engine.StartWorkflow("timer");
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);

            // Kein Signal, kein Enqueue - allein der periodische Poll nimmt den faelligen Timer auf.
            Assert.IsTrue(WaitUntil(() => store.GetInstance(instance.Id).Status == WorkflowStatus.Completed),
                "The runner's poll should have picked up the due timer and completed the instance.");
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
                Id = "wait",
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
                Id = "timer",
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
    }
}
