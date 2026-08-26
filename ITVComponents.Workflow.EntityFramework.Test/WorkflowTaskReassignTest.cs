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
    /// Prueft das <b>Umtragen</b> einer Benutzer-Aufgabe
    /// (<see cref="WorkflowEngine.ReassignUserTask"/>) - Vertretung, Delegation, Zuruecklegen.
    /// </summary>
    /// <remarks>
    /// Bewusst ueber den EF-Store und nicht ueber den In-Memory-Store: der liefert dieselbe Referenz
    /// zurueck, die die Engine gerade veraendert hat. Ein Test darauf waere auch dann gruen, wenn gar
    /// nichts committet wurde - und genau das ist hier die Frage, denn der Zustaendige gehoert dem Token
    /// und wird erst mit dem Commit auf die Aufgaben-Zeile geschrieben.
    /// </remarks>
    [TestClass]
    public class WorkflowTaskReassignTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using var ctx = new WorkflowContext(options);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        [TestMethod]
        public void Reassign_MovesTheTaskToTheNewOwner_AndSurvivesReload()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            Token task = ParkedTask(store, engine, out string instanceId);

            UserTaskAssignmentStatus status =
                engine.ReassignUserTask(instanceId, task.Id, "berta", "chef", "Urlaubsvertretung");

            Assert.AreEqual(UserTaskAssignmentStatus.Reassigned, status);
            Assert.AreEqual("berta", Reload(store, instanceId, task.Id).AssignedTo,
                "the new owner must be readable from the store, not just from the object in hand.");
        }

        [TestMethod]
        public void Reassign_WritesThroughToTheTaskRow()
        {
            // Die Arbeitsliste liest die Token-ZEILE, nicht die Instanz. Ginge der neue Zustaendige nur in
            // den Instanz-Zustand, staende die Aufgabe weiter in der Liste des Vorgaengers - und der neue
            // saehe sie nie.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            Token task = ParkedTask(store, engine, out string instanceId);

            engine.ReassignUserTask(instanceId, task.Id, "berta");

            using var ctx = new WorkflowContext(options);
            TokenRow row = ctx.Tokens.Single(t => t.InstanceId == instanceId && t.TokenId == task.Id);
            Assert.AreEqual("berta", row.AssignedTo);
            Assert.AreEqual("ApproveInvoice", row.TaskKey, "the task must stay a task.");
            Assert.AreEqual((int)TokenStatus.Waiting, row.Status, "reassigning must not move the branch.");
        }

        [TestMethod]
        public void Reassign_ToNull_PutsTheTaskBackIntoThePool()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            Token task = ParkedTask(store, engine, out string instanceId);

            UserTaskAssignmentStatus status = engine.ReassignUserTask(instanceId, task.Id, null);

            Assert.AreEqual(UserTaskAssignmentStatus.Reassigned, status);
            Assert.IsNull(Reload(store, instanceId, task.Id).AssignedTo);
        }

        [TestMethod]
        public void Reassign_EmptyName_IsTheSameAsThePool()
        {
            // Der Dialog liefert fuer ein geleertes Feld einen Leerstring. Wuerde er als Benutzername
            // uebernommen, laege die Aufgabe bei niemandem und taeuchte in keiner Liste mehr auf.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            Token task = ParkedTask(store, engine, out string instanceId);

            engine.ReassignUserTask(instanceId, task.Id, "   ");

            Assert.IsNull(Reload(store, instanceId, task.Id).AssignedTo);
        }

        [TestMethod]
        public void Reassign_ToTheSameOwner_ReportsUnchanged_AndDoesNotCommit()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            Token task = ParkedTask(store, engine, out string instanceId);
            int versionBefore = store.GetInstance(instanceId).Version;

            UserTaskAssignmentStatus status = engine.ReassignUserTask(instanceId, task.Id, "anna");

            Assert.AreEqual(UserTaskAssignmentStatus.Unchanged, status);
            Assert.AreEqual(versionBefore, store.GetInstance(instanceId).Version,
                "nothing changed - then nothing must be written either.");
        }

        [TestMethod]
        public void Reassign_CompletedTask_ReportsNotATask()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            Token task = ParkedTask(store, engine, out string instanceId);
            engine.CompleteUserTask(instanceId, task.Id);

            UserTaskAssignmentStatus status = engine.ReassignUserTask(instanceId, task.Id, "berta");

            Assert.AreEqual(UserTaskAssignmentStatus.NotATask, status,
                "the race against a colleague who was faster must be distinguishable from a real error.");
        }

        [TestMethod]
        public void Reassign_UnknownToken_ReportsNotFound()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            ParkedTask(store, engine, out string instanceId);

            Assert.AreEqual(UserTaskAssignmentStatus.NotFound,
                engine.ReassignUserTask(instanceId, "does-not-exist", "berta"));
        }

        [TestMethod]
        public void Reassign_RecordsWhoHandedItOn()
        {
            // Der Verlauf ist der einzige Nachweis: das Token traegt nur seinen AKTUELLEN Zustaendigen.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            Token task = ParkedTask(store, engine, out string instanceId);

            engine.ReassignUserTask(instanceId, task.Id, "berta", "chef", "Urlaubsvertretung");

            HistoryEntry entry = store.GetInstance(instanceId).History
                .Last(h => h.Event == "UserTaskReassigned");
            StringAssert.Contains(entry.Detail, "anna");
            StringAssert.Contains(entry.Detail, "berta");
            StringAssert.Contains(entry.Detail, "chef");
            StringAssert.Contains(entry.Detail, "Urlaubsvertretung");
        }

        [TestMethod]
        public void Reassign_LeavesTheDeadlineAlone()
        {
            // Ein Wechsel des Bearbeiters ist kein Grund, die Uhr neu zu stellen - sonst liesse sich eine
            // Frist durch Herumreichen beliebig verlaengern.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            Token task = ParkedTask(store, engine, out string instanceId);

            engine.ReassignUserTask(instanceId, task.Id, "berta");

            Token after = Reload(store, instanceId, task.Id);
            Assert.AreEqual(task.TaskDueUtc, after.TaskDueUtc);
            Assert.AreEqual(task.TaskCreatedUtc, after.TaskCreatedUtc);
        }

        private EfWorkflowStore NewStore() => new EfWorkflowStore(() => new WorkflowContext(options));

        private static WorkflowEngine EngineOver(EfWorkflowStore store)
            => new WorkflowEngine(store, new ActivityRegistry());

        /// <summary>Startet den Vorgang und liefert die bei "anna" geparkte Aufgabe.</summary>
        private static Token ParkedTask(EfWorkflowStore store, WorkflowEngine engine, out string instanceId)
        {
            store.SaveDefinition(UserTaskDefinition());
            WorkflowInstance instance = engine.StartWorkflow("ut",
                new Dictionary<string, object> { { "owner", "anna" } });
            instanceId = instance.Id;

            Token task = store.GetInstance(instance.Id).Tokens
                .Single(t => t.Status == TokenStatus.Waiting && t.TaskKey != null);
            Assert.AreEqual("anna", task.AssignedTo, "precondition: the task starts out with anna.");
            return task;
        }

        private static Token Reload(EfWorkflowStore store, string instanceId, string tokenId)
            => store.GetInstance(instanceId).Tokens.Single(t => t.Id == tokenId);

        private static WorkflowDefinition UserTaskDefinition()
            => new WorkflowDefinition
            {
                TechnicalName = "ut",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new UserActivityNode
                    {
                        Id = "u",
                        TaskKey = "ApproveInvoice",
                        RequiredPermission = "Invoice.Approve",
                        Assignment = "owner",
                        Title = "Rechnung freigeben",
                        DueInHours = 24
                    },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->u", SourceId = "s", TargetId = "u" },
                    new SequenceFlow { Id = "u->e", SourceId = "u", TargetId = "e" }
                }
            };
    }
}
