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
    /// Prueft das <b>Anhalten und Fortsetzen</b> einer Instanz: sie wird von keinem Runner mehr
    /// vorangetrieben, nimmt aber weiterhin Ereignisse entgegen.
    /// </summary>
    /// <remarks>
    /// Der zweite Teil ist der wichtigere und der leicht zu uebersehende: wuerde die Zustellung mit
    /// abgeschaltet, gingen genau die Nachrichten verloren, die waehrend der Pause eintreffen - und das
    /// ist der Zeitraum, in dem man sie am wenigsten verlieren will.
    /// </remarks>
    [TestClass]
    public class WorkflowSuspendTest
    {
        private InMemoryWorkflowStore store;
        private WorkflowEngine engine;
        private List<string> ran;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            ran = new List<string>();
            engine = new WorkflowEngine(store,
                new ActivityRegistry().Register("work", _ => ran.Add("work")));
        }

        /// <summary>Start -&gt; Wartepunkt -&gt; Aktivitaet -&gt; Ende.</summary>
        private void SaveDefinition()
            => store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "work" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->a", SourceId = "w", TargetId = "a" },
                    new SequenceFlow { Id = "a->e", SourceId = "a", TargetId = "e" }
                }
            });

        [TestMethod]
        public void Suspended_InstanceIsNotPickedUpAnyMore()
        {
            SaveDefinition();
            WorkflowInstance instance = engine.StartWorkflow("wf");

            Assert.IsTrue(engine.SuspendWorkflow(instance.Id, "waiting for the customer"));

            Assert.IsFalse(store.FindRunnable().Any(i => i.Id == instance.Id),
                "a suspended instance must not be handed to a runner any more.");
            Assert.IsFalse(store.FindDueTimers(System.DateTime.UtcNow.AddYears(1))
                    .Any(i => i.Id == instance.Id),
                "nor may its due timers be picked up.");
        }

        [TestMethod]
        public void Suspended_MessageStillArrives_ButNothingRuns()
        {
            // Der Kern der Sache: das Token wird aktiv, die Aktivitaet laeuft NICHT.
            SaveDefinition();
            WorkflowInstance instance = engine.StartWorkflow("wf");
            engine.SuspendWorkflow(instance.Id, "waiting for the customer");

            engine.SignalWorkflow(instance.Id, "go");

            WorkflowInstance after = store.GetInstance(instance.Id);
            Assert.AreEqual(0, ran.Count, "a suspended instance must not execute anything.");
            Assert.IsTrue(after.Tokens.Any(t => t.Status == TokenStatus.Active),
                "but the message must have arrived - the branch is ready to go.");
        }

        [TestMethod]
        public void Resumed_TheWaitingWorkRunsAfterwards()
        {
            SaveDefinition();
            WorkflowInstance instance = engine.StartWorkflow("wf");
            engine.SuspendWorkflow(instance.Id, "waiting for the customer");
            engine.SignalWorkflow(instance.Id, "go");

            Assert.IsTrue(engine.ResumeWorkflow(instance.Id));
            engine.Advance(store.GetInstance(instance.Id));

            CollectionAssert.AreEqual(new[] { "work" }, ran,
                "what arrived during the pause must run once the instance is resumed.");
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(instance.Id).Status);
        }

        [TestMethod]
        public void Suspend_DoesNotChangeTheStatus()
        {
            // Angehalten ist eine Aussage UEBER den Lebenszyklus, nicht ein Teil davon - sonst waere sie
            // beim naechsten Vortrieb still ueberschrieben (der setzt staendig auf Running zurueck).
            SaveDefinition();
            WorkflowInstance instance = engine.StartWorkflow("wf");
            WorkflowStatus before = store.GetInstance(instance.Id).Status;

            engine.SuspendWorkflow(instance.Id);

            WorkflowInstance after = store.GetInstance(instance.Id);
            Assert.AreEqual(before, after.Status);
            Assert.IsTrue(after.Suspended);
        }

        [TestMethod]
        public void Suspend_RecordsTheReason()
        {
            // Ein angehaltener Vorgang sieht sonst aus wie ein haengender.
            SaveDefinition();
            WorkflowInstance instance = engine.StartWorkflow("wf");

            engine.SuspendWorkflow(instance.Id, "customer on holiday", "chef");

            WorkflowInstance after = store.GetInstance(instance.Id);
            Assert.AreEqual("customer on holiday", after.SuspendedReason);
            HistoryEntry entry = after.History.Last(h => h.Event == "Suspended");
            StringAssert.Contains(entry.Detail, "chef");
            StringAssert.Contains(entry.Detail, "customer on holiday");
        }

        [TestMethod]
        public void Resume_ClearsTheReason()
        {
            SaveDefinition();
            WorkflowInstance instance = engine.StartWorkflow("wf");
            engine.SuspendWorkflow(instance.Id, "customer on holiday");

            engine.ResumeWorkflow(instance.Id);

            WorkflowInstance after = store.GetInstance(instance.Id);
            Assert.IsFalse(after.Suspended);
            Assert.IsNull(after.SuspendedReason, "the reason belongs to the pause, not to the instance.");
        }

        [TestMethod]
        public void Suspend_FinishedInstance_IsRefused()
        {
            // Es gaebe nichts anzuhalten - und ein "angehaltener" abgeschlossener Vorgang waere eine
            // Aussage, die niemand einloesen kann.
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "quick",
                Nodes = new List<WorkflowNode> { new StartNode { Id = "s" }, new EndNode { Id = "e" } },
                Flows = new List<SequenceFlow>
                    { new SequenceFlow { Id = "s->e", SourceId = "s", TargetId = "e" } }
            });
            WorkflowInstance done = engine.StartWorkflow("quick");
            Assert.AreEqual(WorkflowStatus.Completed, done.Status, "precondition: it is finished.");

            Assert.IsFalse(engine.SuspendWorkflow(done.Id));
            Assert.IsFalse(store.GetInstance(done.Id).Suspended);
        }

        [TestMethod]
        public void Suspend_Twice_IsHarmless()
        {
            SaveDefinition();
            WorkflowInstance instance = engine.StartWorkflow("wf");

            Assert.IsTrue(engine.SuspendWorkflow(instance.Id, "first"));
            Assert.IsTrue(engine.SuspendWorkflow(instance.Id, "second"));

            Assert.AreEqual("first", store.GetInstance(instance.Id).SuspendedReason,
                "the second call is a no-op - it must not overwrite the reason of the actual pause.");
        }

        [TestMethod]
        public void Suspend_UnknownInstance_IsRefused()
            => Assert.IsFalse(engine.SuspendWorkflow("does-not-exist"));
    }
}
