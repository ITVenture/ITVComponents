using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using ITVComponents.Workflow.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft das <see cref="EventGatewayNode"/>: mehrere Ereignisse warten gleichzeitig, das erste
    /// gewinnt, die uebrigen werden verworfen.
    /// </summary>
    [TestClass]
    public class WorkflowEventGatewayTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>Start -&gt; Gateway -&gt; (Antwort | Frist) -&gt; je ein Nebenweg -&gt; Ende.</summary>
        private void SaveDefinition(string dueExpression = "'System.TimeSpan'.FromHours(1)")
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new EventGatewayNode { Id = "g" },
                    new WaitNode { Id = "answer", SignalName = "reply" },
                    new TimerNode { Id = "deadline", DueExpression = dueExpression },
                    new AutomatedActivityNode { Id = "onAnswer", ActivityRef = "mark" },
                    new AutomatedActivityNode { Id = "onTimeout", ActivityRef = "mark" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "g"), F("g", "answer"), F("g", "deadline"),
                    F("answer", "onAnswer"), F("deadline", "onTimeout"),
                    F("onAnswer", "e"), F("onTimeout", "e")
                }
            });
        }

        private WorkflowEngine Engine(List<string> ran = null)
            => new WorkflowEngine(store, new ActivityRegistry().Register("mark",
                ctx => (ran ?? new List<string>()).Add(ctx.Node.Id)));

        [TestMethod]
        public void Gateway_ParksOneTokenPerEvent()
        {
            SaveDefinition();

            WorkflowInstance inst = Engine().StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Waiting, final.Status);
            var waiting = final.Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList();
            Assert.AreEqual(2, waiting.Count, "one token waits per racing event.");
            CollectionAssert.AreEquivalent(new[] { "answer", "deadline" },
                waiting.Select(t => t.NodeId).ToArray());
            Assert.AreEqual(1, waiting.Select(t => t.RaceTokenId).Distinct().Count(),
                "both know they belong to the same race.");
            Assert.IsTrue(waiting.All(t => t.Variables == null),
                "a race needs no branch scopes - only one of them survives, there is nothing to merge.");
        }

        [TestMethod]
        public void FirstEventWins_AndTheOthersAreDiscarded()
        {
            SaveDefinition();
            var ran = new List<string>();
            WorkflowEngine engine = Engine(ran);
            WorkflowInstance inst = engine.StartWorkflow("wf");

            engine.SignalWorkflow(inst.Id, "reply");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            CollectionAssert.AreEqual(new[] { "onAnswer" }, ran,
                "only the winner's path runs - the timeout path must not.");
            Assert.IsFalse(final.Tokens.Any(t => t.Status == TokenStatus.Waiting),
                "the losing timer must be gone, otherwise the instance could never complete.");
        }

        [TestMethod]
        public void TheTimerCanWinToo()
        {
            // Sofort faellig: der Timer gewinnt das Rennen statt des Signals.
            SaveDefinition("'System.DateTime'.UtcNow");
            var ran = new List<string>();
            WorkflowEngine engine = Engine(ran);
            WorkflowInstance inst = engine.StartWorkflow("wf");

            engine.TriggerTimers(store.GetInstance(inst.Id), DateTime.UtcNow.AddSeconds(1));

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            CollectionAssert.AreEqual(new[] { "onTimeout" }, ran);
            Assert.IsFalse(final.Tokens.Any(t => t.Status == TokenStatus.Waiting),
                "the losing wait point must be gone.");
        }

        [TestMethod]
        public void TheWinnerLeavesTheRace_SoALaterRoundIsIndependent()
        {
            SaveDefinition();
            WorkflowEngine engine = Engine();
            WorkflowInstance inst = engine.StartWorkflow("wf");

            engine.SignalWorkflow(inst.Id, "reply");

            WorkflowInstance final = store.GetInstance(inst.Id);

            // Der Gewinner ist der Token, der weitergelaufen ist (er steht am Ende).
            Token winner = final.Tokens.Single(t => t.NodeId == "e");
            Assert.IsNull(winner.RaceTokenId,
                "the winner must drop its race membership - otherwise a later round of the same gateway " +
                "would count it among its siblings.");

            // Die Verlierer behalten sie bewusst: sie sind verbraucht (und damit fuer jedes kuenftige
            // Rennen unsichtbar), und im Monitor ist die Angabe eine Spur - "dieses Token hat Rennen X
            // verloren".
            Assert.IsTrue(final.Tokens.Where(t => t.RaceTokenId != null)
                    .All(t => t.Status == TokenStatus.Consumed),
                "any token still carrying a race membership must be consumed.");
        }

        [TestMethod]
        public void ATargetThatDoesNotWait_IsRejectedByTheValidator()
        {
            // Eine automatische Aktivitaet hinter dem Gateway liefe sofort durch und gewaenne immer -
            // das Gateway waere ein stiller Nicht-Effekt.
            var definition = new WorkflowDefinition
            {
                TechnicalName = "bad",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new EventGatewayNode { Id = "g" },
                    new WaitNode { Id = "answer", SignalName = "reply" },
                    new AutomatedActivityNode { Id = "now", ActivityRef = "mark" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "g"), F("g", "answer"), F("g", "now"), F("answer", "e"), F("now", "e")
                }
            };

            var issues = WorkflowDefinitionValidator.Validate(definition).ToList();

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error
                                          && i.Message.Contains("does not wait for an event")),
                "an activity behind an event gateway must be an error, not a warning.");
        }

        [TestMethod]
        public void ASingleEvent_IsRejected()
        {
            var definition = new WorkflowDefinition
            {
                TechnicalName = "bad",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new EventGatewayNode { Id = "g" },
                    new WaitNode { Id = "answer", SignalName = "reply" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "g"), F("g", "answer"), F("answer", "e") }
            };

            var issues = WorkflowDefinitionValidator.Validate(definition).ToList();

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error
                                          && i.Message.Contains("at least")),
                "a race with one participant is not a race.");
        }
    }
}
