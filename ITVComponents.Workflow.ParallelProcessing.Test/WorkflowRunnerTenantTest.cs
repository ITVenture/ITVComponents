using System.Collections.Generic;
using System.IO;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.ParallelProcessing.Test
{
    /// <summary>
    /// Prueft den tenant-uebergreifenden Betrieb: EIN Worker/Store treibt Instanzen verschiedener
    /// Tenants voran, jede aber unter IHREM Tenant (der Store selbst bleibt filterfrei, damit er die
    /// Instanzen aller Tenants findet). Nachweis ueber eine Aktivitaet, die den ambienten Tenant liest.
    /// </summary>
    [TestClass]
    public class WorkflowRunnerTenantTest
    {
        private string dbFile;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;
        private WorkflowEngine engine;
        private WorkflowTaskWorker worker;

        [TestInitialize]
        public void Setup()
        {
            dbFile = Path.GetTempFileName();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite($"DataSource={dbFile}").Options;
            using (var ctx = new WorkflowContext(options))
            {
                ctx.Database.EnsureCreated();
            }

            // Store filterfrei (options-only-Kontext) - findet Instanzen aller Tenants.
            store = new EfWorkflowStore(() => new WorkflowContext(options));
            // Die Aktivitaet haelt fest, welchen Tenant der ambiente Scope beim Ausfuehren traegt.
            var activities = new ActivityRegistry()
                .Register("record", ctx => ctx.Variables["seenTenant"] = WorkflowExecutionScope.CurrentTenant);
            engine = new WorkflowEngine(store, activities);
            worker = new WorkflowTaskWorker(engine, store) { Runtime = engine.Runtime };
        }

        [TestCleanup]
        public void Cleanup()
        {
            TryDelete(dbFile);
            TryDelete(dbFile + "-wal");
            TryDelete(dbFile + "-shm");
        }

        [TestMethod]
        public void EachInstanceIsAdvancedUnderItsOwnTenant()
        {
            store.SaveDefinition(WaitThenRecord());

            // Start je unter dem eigenen Tenant -> die Instanz wird mit diesem Tenant gestempelt und
            // haelt am Wartepunkt (die record-Aktivitaet liegt HINTER dem Wait, laeuft also noch nicht).
            WorkflowInstance a;
            using (WorkflowExecutionScope.UseTenant("tenantA"))
            {
                a = engine.StartWorkflow("rec");
            }

            WorkflowInstance b;
            using (WorkflowExecutionScope.UseTenant("tenantB"))
            {
                b = engine.StartWorkflow("rec");
            }

            Assert.AreEqual(WorkflowStatus.Waiting, a.Status);
            Assert.AreEqual("tenantA", store.GetInstance(a.Id).TenantId, "the instance must be stamped with its tenant.");
            Assert.AreEqual("tenantB", store.GetInstance(b.Id).TenantId);

            // Wichtig: HIER ist KEIN Scope aktiv. Der Worker muss den Tenant selbst aus der Instanz
            // ableiten und setzen, bevor er sie vorantreibt.
            Assert.IsFalse(WorkflowExecutionScope.HasTenant);

            worker.Process(new WorkflowTask(a.Id, WorkflowTrigger.Signal, "go"));
            worker.Process(new WorkflowTask(b.Id, WorkflowTrigger.Signal, "go"));

            WorkflowInstance doneA = store.GetInstance(a.Id);
            WorkflowInstance doneB = store.GetInstance(b.Id);
            Assert.AreEqual(WorkflowStatus.Completed, doneA.Status);
            Assert.AreEqual(WorkflowStatus.Completed, doneB.Status);
            Assert.AreEqual("tenantA", doneA.Variables["seenTenant"], "activity of instance A must run under tenantA.");
            Assert.AreEqual("tenantB", doneB.Variables["seenTenant"], "activity of instance B must run under tenantB.");
        }

        [TestMethod]
        public void WithoutTheWorker_NoTenantIsInScope()
        {
            // Gegenprobe: treibt man dieselbe Instanz OHNE den Worker voran (kein Scope), sieht die
            // Aktivitaet keinen Tenant - der per-Instanz-Tenant kommt tatsaechlich vom Worker.
            store.SaveDefinition(WaitThenRecord());
            WorkflowInstance c;
            using (WorkflowExecutionScope.UseTenant("tenantC"))
            {
                c = engine.StartWorkflow("rec");
            }

            Assert.IsFalse(WorkflowExecutionScope.HasTenant);
            engine.SignalWorkflow(c.Id, "go");

            Assert.IsNull(store.GetInstance(c.Id).Variables["seenTenant"],
                "without the worker's per-instance scope there is no ambient tenant.");
        }

        private static WorkflowDefinition WaitThenRecord()
        {
            return new WorkflowDefinition
            {
                Id = "rec",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new AutomatedActivityNode { Id = "r", ActivityRef = "record" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->r", SourceId = "w", TargetId = "r" },
                    new SequenceFlow { Id = "r->e", SourceId = "r", TargetId = "e" }
                }
            };
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
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Could not delete '{path}': {ex.Message}");
            }
        }
    }
}
