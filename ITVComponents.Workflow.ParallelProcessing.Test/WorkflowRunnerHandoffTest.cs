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
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.ParallelProcessing.Test
{
    /// <summary>
    /// Prueft den verteilten Handoff (Phase 3b) mit ZWEI nebenlaeufigen Runnern ueber eine geteilte
    /// (Datei-)SQLite-Datenbank - stellvertretend fuer zwei Prozesse/Hosts. Ein Workflow hopst von einem
    /// "web"-Schritt zu einem "backend"-Schritt: der Web-Runner fuehrt den Web-Schritt aus und parkt den
    /// Backend-Schritt; der Backend-Runner nimmt den geparkten Zweig auf und fuehrt ihn aus.
    /// </summary>
    [TestClass]
    public class WorkflowRunnerHandoffTest
    {
        private string dbFile;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;
        private WorkflowRunner webRunner;
        private WorkflowRunner backendRunner;

        [TestInitialize]
        public void Setup()
        {
            dbFile = Path.GetTempFileName();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite($"DataSource={dbFile}").Options;
            using (var ctx = new WorkflowContext(options))
            {
                ctx.Database.EnsureCreated();
                ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            }

            store = new EfWorkflowStore(() => new WorkflowContext(options));

            // Beide Engines kennen beide Aktivitaeten (harmlos) - ausgefuehrt wird je nur der Schritt, dessen
            // Ziel der jeweilige Host bedient; den anderen parkt die Engine.
            ActivityRegistry Activities() => new ActivityRegistry()
                .Register("stepWeb", ctx => ctx.Variables["web"] = true)
                .Register("stepBackend", ctx => ctx.Variables["backend"] = true);

            var webEngine = new WorkflowEngine(store, Activities(), hostTargets: new[] { "web" });
            var backendEngine = new WorkflowEngine(store, Activities(), hostTargets: new[] { "backend" });

            webRunner = new WorkflowRunner(webEngine, store,
                new WorkflowRunnerOptions { Owner = "web-runner", WorkerCount = 2, PollTimeMs = 100 });
            backendRunner = new WorkflowRunner(backendEngine, store,
                new WorkflowRunnerOptions { Owner = "backend-runner", WorkerCount = 2, PollTimeMs = 100 });

            webRunner.Start();
            backendRunner.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            webRunner?.Dispose();
            backendRunner?.Dispose();
            TryDelete(dbFile);
            TryDelete(dbFile + "-wal");
            TryDelete(dbFile + "-shm");
        }

        [TestMethod]
        public void WorkflowHopsFromWebHostToBackendHostAndCompletes()
        {
            store.SaveDefinition(Hop());

            // Auf dem Web-Runner starten - er fuehrt den Web-Schritt aus und parkt den Backend-Schritt,
            // den erst der Backend-Runner aufnimmt.
            WorkflowInstance instance = webRunner.StartWorkflow("hop");

            bool done = WaitUntil(() => store.GetInstance(instance.Id).Status == WorkflowStatus.Completed);
            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.IsTrue(done, $"Status={final.Status}, Fault={final.FaultMessage}, Tokens={final.Tokens.Count}");
            Assert.AreEqual(true, final.Variables["web"], "the web host ran the web step.");
            Assert.AreEqual(true, final.Variables["backend"], "the backend host ran the handed-off backend step.");
        }

        /// <summary>Start -&gt; stepWeb (Ziel "web") -&gt; stepBackend (Ziel "backend") -&gt; End.</summary>
        private static WorkflowDefinition Hop()
        {
            return new WorkflowDefinition
            {
                TechnicalName = "hop",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "w", ActivityRef = "stepWeb", ExecutionTarget = "web" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "stepBackend", ExecutionTarget = "backend" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->b", SourceId = "w", TargetId = "b" },
                    new SequenceFlow { Id = "b->e", SourceId = "b", TargetId = "e" }
                }
            };
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
                Console.WriteLine($"Could not delete '{path}': {ex.Message}");
            }
        }
    }
}
