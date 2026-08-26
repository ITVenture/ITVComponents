using System.Collections.Generic;
using ITVComponents.Workflow;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.ParallelProcessing.Test
{
    /// <summary>
    /// Prueft, dass der nebenlaeufige Zweig-Vortrieb jede Instanz unter IHREM Tenant ausfuehrt:
    /// <see cref="WorkflowEngine.RunBranch"/> setzt den ambienten Tenant-Kontext aus der Instanz, sodass
    /// tenant-abhaengige Aktivitaeten die richtigen Daten sehen.
    /// </summary>
    [TestClass]
    public class WorkflowRunnerTenantTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;
        private WorkflowEngine engine;

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
            engine = new WorkflowEngine(store, new ActivityRegistry()
                .Register("record", ctx => ctx.Variables["seenTenant"] = WorkflowExecutionScope.CurrentTenant));
            store.SaveDefinition(RecordDef());
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private WorkflowInstance ActiveAtRecord(string tenant)
        {
            var inst = new WorkflowInstance
            {
                DefinitionKey = store.GetDefinition("rec", 1).Key,
                DefinitionId = "rec",
                DefinitionVersion = 1,
                TenantId = tenant,
                Status = WorkflowStatus.Running,
                Tokens = new List<Token> { new Token { Id = "t", NodeId = "r", Status = TokenStatus.Active } }
            };
            store.SaveInstance(inst);
            return inst;
        }

        [TestMethod]
        public void RunBranch_ExecutesActivityUnderInstanceTenant()
        {
            WorkflowInstance a = ActiveAtRecord("tenantA");
            WorkflowInstance b = ActiveAtRecord("tenantB");

            engine.RunBranch(a.Id, "t");
            engine.RunBranch(b.Id, "t");

            Assert.AreEqual("tenantA", store.GetInstance(a.Id).Variables["seenTenant"],
                "instance A's activity must run under tenantA.");
            Assert.AreEqual("tenantB", store.GetInstance(b.Id).Variables["seenTenant"],
                "instance B's activity must run under tenantB.");
            Assert.IsFalse(WorkflowExecutionScope.HasTenant, "the tenant scope must not leak after RunBranch.");
        }

        [TestMethod]
        public void RunBranch_TenantFreeInstance_ActivitySeesNoTenant()
        {
            WorkflowInstance c = ActiveAtRecord(null);

            engine.RunBranch(c.Id, "t");

            Assert.IsNull(store.GetInstance(c.Id).Variables["seenTenant"]);
        }

        private static WorkflowDefinition RecordDef()
        {
            return new WorkflowDefinition
            {
                TechnicalName = "rec",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "r", ActivityRef = "record" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->r", SourceId = "s", TargetId = "r" },
                    new SequenceFlow { Id = "r->e", SourceId = "r", TargetId = "e" }
                }
            };
        }
    }
}
