using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die Dringlichkeit einer Instanz (<see cref="WorkflowInstance.Priority"/>): woher sie kommt
    /// (Aufrufer, Definition, Standard), dass ein Subworkflow sie erbt und dass sie sich nachtraeglich
    /// aendern laesst. Was die Ausfuehrungsschicht daraus macht, prueft der Runner-Test.
    /// </summary>
    [TestClass]
    public class WorkflowPriorityTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        private void SaveWaiting(string id, int? defaultPriority = null)
        {
            // Ein Wartepunkt, damit die Instanz nach dem Start noch existiert und laeuft.
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = id,
                DefaultPriority = defaultPriority,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "w"), F("w", "e") }
            });
        }

        [TestMethod]
        public void WithoutAnySetting_AnInstanceIsNormal()
        {
            SaveWaiting("wf");
            var engine = new WorkflowEngine(store, new ActivityRegistry());

            WorkflowInstance inst = engine.StartWorkflow("wf");

            Assert.AreEqual(WorkflowPriority.Normal, store.GetInstance(inst.Id).Priority,
                "Normal - not 0. 0 would be the HIGHEST level, and 'unset' must not mean 'most urgent'.");
        }

        [TestMethod]
        public void DefinitionDefault_AppliesToNewInstances()
        {
            SaveWaiting("wf", WorkflowPriority.Lowest);
            var engine = new WorkflowEngine(store, new ActivityRegistry());

            WorkflowInstance inst = engine.StartWorkflow("wf");

            Assert.AreEqual(WorkflowPriority.Lowest, store.GetInstance(inst.Id).Priority);
        }

        [TestMethod]
        public void CallerWins_OverTheDefinitionDefault()
        {
            SaveWaiting("wf", WorkflowPriority.Lowest);
            var engine = new WorkflowEngine(store, new ActivityRegistry());

            WorkflowInstance inst = engine.StartWorkflow("wf", priority: WorkflowPriority.High);

            Assert.AreEqual(WorkflowPriority.High, store.GetInstance(inst.Id).Priority,
                "whoever starts an instance knows more about this one case than the definition does.");
        }

        [TestMethod]
        public void Subworkflow_InheritsTheCallersPriority_NotItsOwnDefault()
        {
            SaveWaiting("child", WorkflowPriority.Lowest);
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "parent",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new CallWorkflowNode { Id = "c", SubDefinitionId = "child" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "c"), F("c", "e") }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry());

            WorkflowInstance parent = engine.StartWorkflow("parent", priority: WorkflowPriority.High);

            WorkflowInstance child = store.FindChildInstances(parent.Id).Single();
            Assert.AreEqual(WorkflowPriority.High, child.Priority,
                "the caller waits for the child - letting the child be slower would stall the caller.");
        }

        [TestMethod]
        public void SetPriority_ChangesARunningInstance_AndLogsIt()
        {
            SaveWaiting("wf");
            var engine = new WorkflowEngine(store, new ActivityRegistry());
            WorkflowInstance inst = engine.StartWorkflow("wf");

            Assert.IsTrue(engine.SetPriority(inst.Id, WorkflowPriority.Highest));

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowPriority.Highest, final.Priority);
            HistoryEntry entry = final.History.FirstOrDefault(h => h.Event == "PriorityChanged");
            Assert.IsNotNull(entry, "a change from outside belongs in the instance log.");
            StringAssert.Contains(entry.Detail, "Highest");
        }

        [TestMethod]
        public void SetPriority_OfAnUnknownInstance_IsRefusedInsteadOfThrowing()
        {
            var engine = new WorkflowEngine(store, new ActivityRegistry());

            Assert.IsFalse(engine.SetPriority("does-not-exist", WorkflowPriority.High));
        }

        [TestMethod]
        public void FindRunnable_YieldsTheMostUrgentFirst()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow> { F("s", "e") }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry());
            // CreateInstance legt an, ohne voranzutreiben - die Instanzen bleiben also lauffaehig.
            engine.CreateInstance("wf", priority: WorkflowPriority.Lowest);
            WorkflowInstance urgent = engine.CreateInstance("wf", priority: WorkflowPriority.Highest);
            engine.CreateInstance("wf", priority: WorkflowPriority.Normal);

            List<WorkflowInstance> runnable = store.FindRunnable().ToList();

            Assert.AreEqual(urgent.Id, runnable[0].Id,
                "a caller that only gets through part of the list must at least get the right part.");
            CollectionAssert.AreEqual(
                new[] { WorkflowPriority.Highest, WorkflowPriority.Normal, WorkflowPriority.Lowest },
                runnable.Select(i => i.Priority).ToArray());
        }

        [TestMethod]
        public void Clamp_KeepsAnOutOfBandValueUsable()
        {
            // Eine Instanz aus einer Umgebung mit anderem Band soll laufen, nicht scheitern.
            Assert.AreEqual(1, WorkflowPriority.Clamp(-5, 1, 3));
            Assert.AreEqual(3, WorkflowPriority.Clamp(99, 1, 3));
            Assert.AreEqual(2, WorkflowPriority.Clamp(2, 1, 3));
        }
    }
}
