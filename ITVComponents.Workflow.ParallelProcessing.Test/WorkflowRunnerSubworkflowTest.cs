using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    /// Prueft Subworkflows ueber den laufenden <see cref="WorkflowRunner"/> (der reale, nebenlaeufige
    /// Ausfuehrungspfad): ein Elternprozess ruft einen Subworkflow auf, parkt, und der Runner treibt Kind
    /// UND Eltern selbstaendig bis zum Ende - inklusive Ergebnis-Rueckgabe an den Elternprozess.
    /// </summary>
    [TestClass]
    public class WorkflowRunnerSubworkflowTest
    {
        private string dbFile;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;
        private WorkflowRunner runner;

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
            var engine = new WorkflowEngine(store, new ActivityRegistry()
                .Register("compute", ctx => ctx.Variables["result"] = (int)ctx.Variables["n"] * 10));
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
        public void RunnerDrivesSubworkflowAndReturnsResultToParent()
        {
            store.SaveDefinition(SubDef());
            store.SaveDefinition(MainDef());

            WorkflowInstance parent = runner.StartWorkflow("main", new Dictionary<string, object> { { "value", 7 } });

            bool done = WaitUntil(() => store.GetInstance(parent.Id).Status == WorkflowStatus.Completed);
            WorkflowInstance final = store.GetInstance(parent.Id);
            Assert.IsTrue(done, $"Status={final.Status}, Fault={final.FaultMessage}");
            Assert.AreEqual(70, final.Variables["answer"], "the sub-workflow result flowed back to the parent.");
            Assert.AreEqual(WorkflowStatus.Completed, store.FindChildInstances(parent.Id).Single().Status);
        }

        private static WorkflowDefinition MainDef()
        {
            var call = new CallWorkflowNode { Id = "call", SubDefinitionId = "sub" };
            call.Inputs.Add(new ActivityInputBinding { Parameter = "n", Kind = ParameterBindingKind.Variable, Source = "value" });
            call.Outputs.Add(new ActivityOutputBinding { Parameter = "result", Variable = "answer" });
            return new WorkflowDefinition
            {
                TechnicalName = "main",
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, call, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->call", SourceId = "s", TargetId = "call" },
                    new SequenceFlow { Id = "call->e", SourceId = "call", TargetId = "e" }
                }
            };
        }

        private static WorkflowDefinition SubDef()
        {
            return new WorkflowDefinition
            {
                TechnicalName = "sub",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "c", ActivityRef = "compute" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->c", SourceId = "s", TargetId = "c" },
                    new SequenceFlow { Id = "c->e", SourceId = "c", TargetId = "e" }
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
