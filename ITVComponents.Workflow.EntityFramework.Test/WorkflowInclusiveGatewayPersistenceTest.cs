using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft die angemeldete Zweig-Zahl des inklusiven Gateways ueber eine <b>Park-Grenze</b> hinweg.
    /// Muss gegen den EF-Store laufen: der In-Memory-Store liefert dieselbe Objekt-Referenz zurueck und
    /// wuerde eine fehlende Spalte verdecken - der Join haenge dann erst im Betrieb.
    /// </summary>
    [TestClass]
    public class WorkflowInclusiveGatewayPersistenceTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;

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
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private static SequenceFlow F(string from, string to, string condition = null) =>
            new SequenceFlow
            {
                Id = $"{from}->{to}", SourceId = from, TargetId = to, Condition = condition
            };

        [TestMethod]
        public void TheAnnouncedBranchCountSurvivesAPark()
        {
            // Zwei von drei Zweigen aktiv, beide parken an einem Wartepunkt (= Commit in die Ablage).
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new InclusiveGatewayNode { Id = "split" },
                    new WaitNode { Id = "a", SignalName = "go" },
                    new WaitNode { Id = "b", SignalName = "go" },
                    new WaitNode { Id = "c", SignalName = "go" },
                    new InclusiveGatewayNode { Id = "join" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "work" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"),
                    F("split", "a", "wantA"), F("split", "b", "wantB"), F("split", "c", "wantC"),
                    F("a", "join"), F("b", "join"), F("c", "join"),
                    F("join", "after"), F("after", "e")
                }
            });
            var ran = new List<string>();
            var engine = new WorkflowEngine(store,
                new ActivityRegistry().Register("work", ctx => ran.Add(ctx.Node.Id)));

            WorkflowInstance inst = engine.StartWorkflow("wf", new Dictionary<string, object>
            {
                { "wantA", true }, { "wantB", false }, { "wantC", true }
            });

            List<Token> waiting = store.GetInstance(inst.Id).Tokens
                .Where(t => t.Status == TokenStatus.Waiting).ToList();
            Assert.AreEqual(2, waiting.Count);
            Assert.IsTrue(waiting.All(t => t.SplitBranchCount == 2),
                "without this column the join would never know how many to wait for - and hang.");

            engine.SignalWorkflow(inst.Id, "go");
            engine.Advance(store.GetInstance(inst.Id));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            Assert.AreEqual(1, ran.Count(r => r == "after"));
        }
    }
}
