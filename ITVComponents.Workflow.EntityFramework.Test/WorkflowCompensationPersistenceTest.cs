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
    /// Prueft die zur Ruecknahme vorgemerkten Schritte ueber eine <b>Park-Grenze</b> hinweg. Muss zwingend
    /// gegen den EF-Store laufen: der In-Memory-Store liefert dieselbe Objekt-Referenz zurueck und wuerde
    /// jeden Verlust beim Ablegen verdecken.
    /// </summary>
    [TestClass]
    public class WorkflowCompensationPersistenceTest
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

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Start -&gt; reserve -&gt; Wartepunkt (= Park + Commit) -&gt; undo -&gt; Ende, mit einem
        /// Rueckabwicklungs-Pfad an reserve.
        /// </summary>
        private void SaveDefinition()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "reserve", ActivityRef = "work" },
                    new WaitNode { Id = "wait", SignalName = "cancel" },
                    new CompensateNode { Id = "undo" },
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "cReserve", AttachedToNodeId = "reserve" },
                    new AutomatedActivityNode { Id = "unreserve", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "reserve"), F("reserve", "wait"), F("wait", "undo"), F("undo", "e"),
                    F("cReserve", "unreserve"), F("unreserve", "cEnd")
                }
            });
        }

        [TestMethod]
        public void ThePendingUndoStepsSurviveAPark()
        {
            SaveDefinition();
            var seen = new Dictionary<string, object>();
            var ran = new List<string>();
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("work", ctx =>
            {
                ran.Add(ctx.Node.Id);
                if (ctx.Node.Id == "reserve")
                {
                    ctx.Variables["ticket"] = "R-1";
                }
                else
                {
                    seen["ticket"] = ctx.Variables.TryGetValue("ticket", out object v) ? v : null;
                }
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance parked = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Waiting, parked.Status);
            Assert.AreEqual(1, parked.Compensations.Count,
                "the armed step must come back out of the store - otherwise nothing would be undone after " +
                "a park.");
            Assert.AreEqual("reserve", parked.Compensations[0].NodeId);

            engine.SignalWorkflow(inst.Id, "cancel");
            engine.Advance(store.GetInstance(inst.Id));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            CollectionAssert.Contains(ran, "unreserve");
            Assert.AreEqual("R-1", seen["ticket"],
                "the snapshot of the compensated step survives the store round-trip typed, not as a JSON node.");
            Assert.IsTrue(store.GetInstance(inst.Id).Compensations.Single().Compensated,
                "an undone step stays marked as undone - a second trigger must not run it again.");
        }
    }
}
