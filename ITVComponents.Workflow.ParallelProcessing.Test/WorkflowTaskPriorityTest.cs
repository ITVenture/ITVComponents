using System.Collections.Generic;
using System.Linq;
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
    /// Prueft, dass die Dringlichkeit einer Instanz bis zum Auftrag durchkommt - denn genau daran haengt
    /// die Priorisierung: <c>ParallelTaskProcessor</c> waehlt nach <see cref="WorkflowTask.Priority"/>,
    /// die Engine kennt davon nichts. Ein Auftrag mit der falschen Stufe landete stumm in der falschen
    /// Warteschlange.
    /// </summary>
    [TestClass]
    public class WorkflowTaskPriorityTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;
        private WorkflowEngine engine;
        private List<WorkflowTask> enqueued;
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
            engine = new WorkflowEngine(store, new ActivityRegistry().Register("noop", _ => { }));
            enqueued = new List<WorkflowTask>();
            worker = new WorkflowTaskWorker(engine, store, "runner-test", t => enqueued.Add(t));
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        [TestMethod]
        public void TaskCarriesItsPriority_ToTheProcessor()
        {
            var task = new WorkflowTask("i1", WorkflowTrigger.Advance, "t1",
                priority: WorkflowPriority.Lowest);

            Assert.AreEqual(WorkflowPriority.Lowest, task.Priority,
                "TaskBase.Priority is what the processor sorts by - it has to arrive there.");
            Assert.AreEqual(WorkflowPriority.Normal, new WorkflowTask("i1", WorkflowTrigger.Timer).Priority,
                "without a level a task is Normal, not 0 (= Highest).");
        }

        [TestMethod]
        public void SpawnedBranches_InheritThePriorityOfTheirTrigger()
        {
            // Ein paralleles Gateway: aus EINEM Zweig entstehen zwei - beide gehoeren zur selben Instanz
            // und muessen deshalb mit deren Stufe eingereiht werden.
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "noop" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "noop" },
                    new EndNode { Id = "e1" },
                    new EndNode { Id = "e2" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "a"), F("split", "b"), F("a", "e1"), F("b", "e2")
                }
            });
            WorkflowInstance instance = engine.CreateInstance("wf", priority: WorkflowPriority.Low);

            worker.Process(new WorkflowTask(instance.Id, WorkflowTrigger.Advance, instance.Tokens[0].Id,
                priority: WorkflowPriority.Low));

            Assert.IsTrue(enqueued.Count > 0, "the split must have produced follow-up branches.");
            Assert.IsTrue(enqueued.All(t => t.Priority == WorkflowPriority.Low),
                "a split child that fell back to Normal would overtake the branch it came from.");
        }

        [TestMethod]
        public void StoreYieldsThePriority_WithoutLoadingTheWholeInstance()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow> { F("s", "e") }
            });
            WorkflowInstance instance = engine.CreateInstance("wf", priority: WorkflowPriority.High);

            Assert.AreEqual(WorkflowPriority.High, store.GetInstancePriority(instance.Id));
            Assert.IsNull(store.GetInstancePriority("does-not-exist"),
                "null distinguishes 'no such instance' from priority 0 (= Highest).");
        }

        [TestMethod]
        public void ClaimDueTimers_TakesTheMostUrgentFirst_WhenTheBatchIsTooSmall()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    // Sofort faellig: der Timer steht damit direkt zum Aufgriff an.
                    new TimerNode { Id = "t", DueExpression = "'System.DateTime'.UtcNow" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "t"), F("t", "e") }
            });

            // Die unwichtige zuerst anlegen - waere die Reihenfolge die der Faelligkeit, gewaenne sie.
            WorkflowInstance low = engine.StartWorkflow("wf", priority: WorkflowPriority.Lowest);
            WorkflowInstance high = engine.StartWorkflow("wf", priority: WorkflowPriority.Highest);

            List<WorkflowInstance> claimed = store
                .ClaimDueTimers(System.DateTime.UtcNow, "owner", System.TimeSpan.FromMinutes(1), 1)
                .ToList();

            Assert.AreEqual(1, claimed.Count, "the batch limit must hold.");
            Assert.AreEqual(high.Id, claimed[0].Id,
                $"the urgent instance must win the single slot (low was {low.Id}).");
        }
    }
}
