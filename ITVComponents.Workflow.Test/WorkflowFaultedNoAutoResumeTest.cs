using System;
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
    /// Haelt fest, dass eine <b>gefaultete</b> Instanz von keinem eintreffenden Ereignis wieder in Gang
    /// gesetzt wird - weder von einem faellig werdenden Timer noch von einem Signal. Wieder aufgenommen
    /// wird sie ausschliesslich durch einen ausdruecklichen Retry.
    /// </summary>
    /// <remarks>
    /// <see cref="WorkflowEngine.RetryFaulted"/> braucht die Tokens der gescheiterten Instanz - deshalb
    /// laesst <c>Fault</c> sie bewusst stehen. Ein Zweig, der neben dem gescheiterten auf einen Timer
    /// wartet, bleibt damit scharf. Ohne Riegel setzte die Reaktivierung die Instanz beim Faelligwerden
    /// wieder auf <see cref="WorkflowStatus.Running"/>: sie liefe still weiter, die Fehlermeldung waere
    /// weg, und den Wiederaufsatz haette niemand entschieden.
    /// <para>
    /// Der Riegel sitzt in der Engine (<c>MayResumeOnEvent</c>), nicht im Store - der Store muss den
    /// Zweig weiter finden, sonst kaeme man nach einem Retry nie wieder an ihn heran. Die gepollten
    /// Suchlaeufe lassen gefaultete Instanzen zusaetzlich aus, damit der Runner sie nicht bei jedem Takt
    /// aufgreift und abweist; geprueft wird das im Store-Vertragstest.
    /// </para></remarks>
    [TestClass]
    public class WorkflowFaultedNoAutoResumeTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Eine parallele Region: der eine Zweig wartet (Timer oder Signal), der andere scheitert. Genau
        /// so entsteht der Zustand, um den es geht - eine gefaultete Instanz MIT einem noch wartenden
        /// Token. Der wartende Zweig steht zuerst, damit er seinen Wartepunkt erreicht, bevor der andere
        /// die Instanz faulten laesst.
        /// </summary>
        private void SaveForkedDefinition(WorkflowNode waitNode)
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "fork" },
                    waitNode,
                    new EndNode { Id = "e1" },
                    new AutomatedActivityNode { Id = "boom", ActivityRef = "boom" },
                    new EndNode { Id = "e2" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "fork"), F("fork", "w"), F("fork", "boom"), F("w", "e1"), F("boom", "e2")
                }
            });
        }

        private WorkflowEngine NewEngine()
            => new WorkflowEngine(store, new ActivityRegistry()
                .Register("boom", ctx => throw new InvalidOperationException("this branch fails")));

        [TestMethod]
        public void ADueTimerDoesNotResumeAFaultedInstance()
        {
            SaveForkedDefinition(new TimerNode { Id = "w", DueExpression = "due" });
            WorkflowEngine engine = NewEngine();
            DateTime due = DateTime.UtcNow.AddMinutes(30);

            WorkflowInstance started = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "due", due } });

            // Vorbedingung - erst wenn dieser Zustand wirklich entsteht, prueft der Test etwas.
            WorkflowInstance faulted = store.GetInstance(started.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, faulted.Status,
                "the failing branch must have faulted the instance.");
            Assert.IsTrue(faulted.Tokens.Any(t => t.Status == TokenStatus.Waiting && t.DueUtc != null),
                "the timer of the other branch must still be armed - that is what Fault leaves standing.");

            DateTime later = due.AddMinutes(1);

            // Der gepollte Suchlauf greift sie gar nicht erst auf.
            Assert.AreEqual(0, store.FindDueTimers(later).Count(),
                "the timer poll must not pick up a faulted instance - otherwise it refuses it on every "
                + "single cycle, and a due timer stays due forever.");

            // Und der direkte Weg weist sie ab.
            Assert.IsFalse(engine.TriggerTimers(faulted, later),
                "no timer must fire on a faulted instance.");

            WorkflowInstance after = store.GetInstance(started.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, after.Status,
                "the fault must survive - a due timer is not a decision to pick the instance up again.");
            Assert.IsNotNull(after.FaultMessage, "the fault message must not be cleared.");
            Assert.IsTrue(after.Tokens.Any(t => t.Status == TokenStatus.Waiting && t.DueUtc != null),
                "the timer token must stay waiting - the retry still needs it.");
        }

        [TestMethod]
        public void ATriggerRunOverAllDueTimersLeavesAFaultedInstanceAlone()
        {
            SaveForkedDefinition(new TimerNode { Id = "w", DueExpression = "due" });
            WorkflowEngine engine = NewEngine();
            DateTime due = DateTime.UtcNow.AddMinutes(30);
            WorkflowInstance started = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "due", due } });

            engine.TriggerDueTimers(due.AddMinutes(1));

            WorkflowInstance after = store.GetInstance(started.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, after.Status,
                "the runner's timer sweep must not silently un-fault the instance.");

            // Diese Zusicherung ist die eigentliche: der Status ALLEIN beweist hier nichts. Das Token des
            // gescheiterten Zweigs bleibt aktiv (es ist der Wiederaufsatzpunkt), und ein Vortrieb fuehrt
            // die Aktivitaet deshalb erneut aus - sie scheitert wieder, und die Instanz stuende auch ohne
            // Riegel am Ende wieder auf Faulted. Ob der Timer gefeuert hat, sieht man nur an SEINEM Token.
            Assert.IsTrue(after.Tokens.Any(t => t.Status == TokenStatus.Waiting && t.DueUtc != null),
                "the timer must still be armed - if the sweep had fired it, the token would have moved on.");
        }

        [TestMethod]
        public void AnArrivingSignalDoesNotResumeAFaultedInstance()
        {
            SaveForkedDefinition(new WaitNode { Id = "w", SignalName = "go" });
            WorkflowEngine engine = NewEngine();
            WorkflowInstance started = engine.StartWorkflow("wf");

            WorkflowInstance faulted = store.GetInstance(started.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, faulted.Status,
                "the failing branch must have faulted the instance.");
            Assert.IsTrue(faulted.Tokens.Any(t => t.Status == TokenStatus.Waiting && t.WaitingSignal == "go"),
                "the other branch must still be waiting for the signal.");

            Assert.IsFalse(engine.SignalWorkflow(started.Id, "go"),
                "a signal must not push a faulted instance onward.");

            WorkflowInstance after = store.GetInstance(started.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, after.Status, "the fault must survive the signal.");
            Assert.IsTrue(after.Tokens.Any(t => t.Status == TokenStatus.Waiting && t.WaitingSignal == "go"),
                "the wait point must stay - after a retry the branch has to be reachable again.");
        }

        /// <summary>
        /// Auch der Klick eines Menschen schiebt einen Wartepunkt weiter - und darf einen stehenden
        /// Vorgang deshalb nicht wieder in Gang setzen. Anders als bei Timer und Signal bekommt die
        /// Oberflaeche hier aber eine Antwort statt nur einen Log-Eintrag.
        /// </summary>
        [TestMethod]
        public void CompletingAUserTaskOfAFaultedInstanceIsRefusedWithItsOwnOutcome()
        {
            SaveForkedDefinition(new UserActivityNode { Id = "w", TaskKey = "Check" });
            WorkflowEngine engine = NewEngine();
            WorkflowInstance started = engine.StartWorkflow("wf");

            WorkflowInstance faulted = store.GetInstance(started.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, faulted.Status,
                "the failing branch must have faulted the instance.");
            Token task = faulted.Tokens.Single(t => t.Status == TokenStatus.Waiting && t.TaskKey != null);

            UserTaskCompletionResult result = engine.CompleteUserTask(started.Id, task.Id,
                new Dictionary<string, object> { { "decision", true } }, "anna");

            Assert.AreEqual(UserTaskCompletionStatus.InstanceNotResumable, result.Status,
                "the outcome must say what is really the matter - NOT NotFound: the task still exists, "
                + "and after a retry of the process the very same click is right again.");
            Assert.IsFalse(result.Success, "nothing was completed.");

            WorkflowInstance after = store.GetInstance(started.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, after.Status, "the fault must survive the click.");
            Assert.IsTrue(after.Tokens.Any(t => t.Id == task.Id && t.Status == TokenStatus.Waiting),
                "the task token must still be waiting - it is the same token the user sees again.");
        }

        /// <summary>
        /// Die Gegenprobe: der Riegel darf nur den Fault betreffen. Dieselbe Definition ohne den
        /// scheiternden Zweig laeuft auf den Timer zu und wird von ihm ganz normal aufgenommen.
        /// </summary>
        [TestMethod]
        public void ADueTimerStillFiresOnAHealthyInstance()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new TimerNode { Id = "w", DueExpression = "due" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "w"), F("w", "e") }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry());
            DateTime due = DateTime.UtcNow.AddMinutes(30);
            WorkflowInstance started = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "due", due } });
            Assert.AreEqual(WorkflowStatus.Waiting, started.Status);

            DateTime later = due.AddMinutes(1);
            Assert.AreEqual(1, store.FindDueTimers(later).Count(), "the healthy instance is due.");
            engine.TriggerDueTimers(later);

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(started.Id).Status,
                "without a fault the timer works exactly as before.");
        }
    }
}
