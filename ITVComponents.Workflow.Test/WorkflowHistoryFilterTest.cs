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
    /// Prueft den <see cref="WorkflowHistoryFilter"/>: welche Eintraege ueberhaupt ins Ablauf-Protokoll
    /// einer Instanz kommen. Der Filter greift auf der SCHREIB-Seite - ein herausgefilterter Eintrag
    /// entsteht gar nicht erst und wird damit auch nicht persistiert.
    /// </summary>
    [TestClass]
    public class WorkflowHistoryFilterTest
    {
        private InMemoryWorkflowStore store;
        private IWorkflowHistoryFilter previousDefault;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            // Der Standard ist prozessweit - wer ihn in einem Test aendert, muss ihn zurueckstellen,
            // sonst faerbt er auf jeden folgenden Test ab.
            previousDefault = WorkflowHistoryFilter.Default;
        }

        [TestCleanup]
        public void Teardown() => WorkflowHistoryFilter.Default = previousDefault;

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>Start -> Aktivitaet -> Ende; die Aktivitaet tut nichts oder wirft.</summary>
        private void SaveDefinition(bool failing = false, HistorySeverity? minSeverity = null)
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                MinHistorySeverity = minSeverity,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = failing ? "boom" : "step" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "a"), F("a", "e") }
            });
        }

        private WorkflowEngine Engine(IWorkflowHistoryFilter filter = null, bool failing = false)
        {
            ActivityRegistry activities = failing
                ? new ActivityRegistry().Register("boom",
                    _ => throw new System.InvalidOperationException("kaboom"))
                : new ActivityRegistry().Register("step", _ => { });
            return new WorkflowEngine(store, activities, historyFilter: filter);
        }

        [TestMethod]
        public void WithoutConfiguration_EverythingIsLogged()
        {
            SaveDefinition();
            WorkflowInstance inst = Engine().StartWorkflow("wf");

            List<HistoryEntry> history = store.GetInstance(inst.Id).History;
            Assert.IsTrue(history.Any(h => h.Severity == HistorySeverity.Verbose),
                "the default filter must not change the previous behaviour.");
        }

        [TestMethod]
        public void MinSeverity_DropsTheStepChatter_ButKeepsTheMilestones()
        {
            SaveDefinition();
            var filter = new WorkflowHistoryFilter { MinSeverity = HistorySeverity.Info };

            WorkflowInstance inst = Engine(filter).StartWorkflow("wf");

            List<HistoryEntry> history = store.GetInstance(inst.Id).History;
            Assert.IsFalse(history.Any(h => h.Severity == HistorySeverity.Verbose),
                "Verbose entries (Entered/Completed per node) are the noise this setting is for.");
            Assert.IsTrue(history.Any(h => h.Event == "Started"), "milestones stay.");
            Assert.IsTrue(history.Any(h => h.Event == "Completed" && h.NodeId == null),
                "the completion of the instance is a milestone, not step chatter.");
        }

        [TestMethod]
        public void SuppressedEvents_DropByName_WithWildcards()
        {
            SaveDefinition();
            var filter = new WorkflowHistoryFilter { SuppressedEvents = { "Enter*" } };

            WorkflowInstance inst = Engine(filter).StartWorkflow("wf");

            List<HistoryEntry> history = store.GetInstance(inst.Id).History;
            Assert.IsFalse(history.Any(h => h.Event == "Entered"));
            Assert.IsTrue(history.Any(h => h.Event == "Started"), "only the named events are dropped.");
        }

        [TestMethod]
        public void AllowedEvents_KeepOnlyWhatIsListed()
        {
            SaveDefinition();
            var filter = new WorkflowHistoryFilter { AllowedEvents = { "Started" } };

            WorkflowInstance inst = Engine(filter).StartWorkflow("wf");

            List<HistoryEntry> history = store.GetInstance(inst.Id).History;
            Assert.IsTrue(history.All(h => h.Event == "Started"));
            Assert.AreEqual(1, history.Count);
        }

        [TestMethod]
        public void Errors_AlwaysGetThrough_EvenWithEverythingElseSilenced()
        {
            SaveDefinition(failing: true);
            // So streng wie es geht: nichts steht auf der Positivliste, die Mindest-Stufe ist die hoechste.
            var filter = new WorkflowHistoryFilter
            {
                MinSeverity = HistorySeverity.Error,
                AllowedEvents = { "NothingMatchesThis" },
                SuppressedEvents = { "*" }
            };

            WorkflowInstance inst = Engine(filter, failing: true).StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status);
            Assert.IsTrue(final.History.Any(h => h.Event == "Faulted"),
                "a silenced log must never swallow the one entry that explains why the instance died.");
        }

        [TestMethod]
        public void DefinitionSetting_OverridesTheEngineFilter()
        {
            // Die Engine laesst alles durch, die Definition will nur Meilensteine.
            SaveDefinition(minSeverity: HistorySeverity.Info);

            WorkflowInstance inst = Engine(new WorkflowHistoryFilter()).StartWorkflow("wf");

            Assert.IsFalse(store.GetInstance(inst.Id).History.Any(h => h.Severity == HistorySeverity.Verbose),
                "a definition may be less chatty than the host default.");
        }

        [TestMethod]
        public void ProcessWideDefault_AppliesWithoutAnEngineFilter()
        {
            SaveDefinition();
            WorkflowHistoryFilter.Default = new WorkflowHistoryFilter { MinSeverity = HistorySeverity.Info };

            WorkflowInstance inst = Engine().StartWorkflow("wf");

            Assert.IsFalse(store.GetInstance(inst.Id).History.Any(h => h.Severity == HistorySeverity.Verbose),
                "the process-wide default is the one-line setting an application makes at startup.");
        }
    }
}
