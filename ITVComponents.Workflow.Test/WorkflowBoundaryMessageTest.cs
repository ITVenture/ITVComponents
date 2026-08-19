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
    /// Prueft den <b>Nachrichten-Empfang am Schritt</b> (<see cref="BoundaryMessageNode"/>): er haengt an
    /// einem Schritt, an dem geparkt wird, und feuert, wenn dort eine Nachricht eintrifft, waehrend
    /// gearbeitet wird. Das ist die Luecke, die der Fristen-Timer nicht schliessen kann - der reagiert nur
    /// auf ZEIT, nicht auf ein Ereignis.
    /// </summary>
    [TestClass]
    public class WorkflowBoundaryMessageTest
    {
        private InMemoryWorkflowStore store;
        private WorkflowEngine engine;
        private List<string> reactions;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            reactions = new List<string>();
            var activities = new ActivityRegistry();
            activities.Register("react", ctx =>
                reactions.Add(ctx.Variables.TryGetValue("round", out object r) ? $"react#{r}" : "react"));
            engine = new WorkflowEngine(store, activities);
        }

        private static SequenceFlow F(string from, string to)
            => new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Start → Benutzer-Aufgabe → Ende, mit einem Nachrichten-Empfang an der Aufgabe, dessen Nebenpfad
        /// eine Reaktion ausfuehrt und in einem Nebenpfad-Ende auslaeuft.
        /// </summary>
        private void SaveTaskWithBoundary(Action<BoundaryMessageNode> configure)
        {
            var boundary = new BoundaryMessageNode
            {
                Id = "onmsg",
                AttachedToNodeId = "task",
                SignalName = "Cancelled",
                CountVariable = "round"
            };
            configure(boundary);

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new UserActivityNode { Id = "task", TaskKey = "Approve" },
                    boundary,
                    new AutomatedActivityNode { Id = "react", ActivityRef = "react" },
                    new SidePathEndNode { Id = "side-end" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "task"), F("task", "e"),
                    F("onmsg", "react"), F("react", "side-end")
                }
            });
        }

        private static Token Task(WorkflowInstance instance)
            => instance.Tokens.Single(t => t.NodeId == "task");

        private static Token Listener(WorkflowInstance instance)
            => instance.Tokens.Single(t => t.NodeId == "onmsg" && t.Status == TokenStatus.Waiting);

        [TestMethod]
        public void Listener_IsArmedWhenTheTaskParks()
        {
            SaveTaskWithBoundary(_ => { });

            WorkflowInstance instance = engine.StartWorkflow("wf");

            Token listener = Listener(instance);
            Assert.AreEqual("Cancelled", listener.WaitingSignal,
                "the listener must wait like any other wait point - that is what makes ordinary delivery find it.");
            Assert.AreEqual(Task(instance).Id, listener.BoundaryOwnerTokenId);
        }

        [TestMethod]
        public void Message_SpawnsTheSidePath_AndLeavesTheTaskAlone()
        {
            SaveTaskWithBoundary(_ => { });
            WorkflowInstance instance = engine.StartWorkflow("wf");

            engine.SignalWorkflow(instance.Id, "Cancelled");

            WorkflowInstance after = store.GetInstance(instance.Id);
            CollectionAssert.AreEqual(new[] { "react#1" }, reactions);
            Assert.AreEqual(TokenStatus.Waiting, Task(after).Status,
                "a non-interrupting boundary must not touch the main flow.");
            Assert.AreEqual("Approve", Task(after).TaskKey, "the task stays in the work list.");
        }

        [TestMethod]
        public void Message_FiresAgainAndAgain()
        {
            // DER Unterschied zum Fristen-Timer: eine Nachricht kann wiederkommen ("der Kunde fragt
            // erneut nach"), und der Schritt darf nach der ersten nicht taub werden. Die Fristenliste
            // eines Timers laeuft dagegen einmal durch.
            SaveTaskWithBoundary(_ => { });
            WorkflowInstance instance = engine.StartWorkflow("wf");

            engine.SignalWorkflow(instance.Id, "Cancelled");
            engine.SignalWorkflow(instance.Id, "Cancelled");
            engine.SignalWorkflow(instance.Id, "Cancelled");

            CollectionAssert.AreEqual(new[] { "react#1", "react#2", "react#3" }, reactions,
                "the listener must stay armed - and count.");
        }

        [TestMethod]
        public void Interrupting_MovesTheMainTokenAndEndsTheTask()
        {
            SaveTaskWithBoundary(b => b.Interrupting = true);
            WorkflowInstance instance = engine.StartWorkflow("wf");

            engine.SignalWorkflow(instance.Id, "Cancelled");

            WorkflowInstance after = store.GetInstance(instance.Id);
            CollectionAssert.AreEqual(new[] { "react#1" }, reactions);
            // Das Haupt-Token NIMMT die Kante des Empfangs - es steht danach also gar nicht mehr auf dem
            // Schritt. Geprueft wird deshalb, was fachlich zaehlt: es liegt keine offene Aufgabe mehr da.
            Assert.IsFalse(after.Tokens.Any(t => t.TaskKey != null && t.Status == TokenStatus.Waiting),
                "an interrupted task must disappear from the work list - the process is elsewhere now.");
            Assert.IsFalse(after.Tokens.Any(t => t.NodeId == "onmsg" && t.Status == TokenStatus.Waiting),
                "after interrupting there is nothing left for the listener to hang on.");
        }

        [TestMethod]
        public void Payload_ReachesTheSidePath_ButNotTheMainFlow()
        {
            // Wie beim Fristen-Timer: der Nebenpfad bekommt eine KOPIE des Scopes. Was dort entsteht,
            // darf den Hauptfluss nicht verstellen - er arbeitet ja weiter.
            SaveTaskWithBoundary(_ => { });
            WorkflowInstance instance = engine.StartWorkflow("wf");

            engine.SignalWorkflow(instance.Id, "Cancelled",
                new Dictionary<string, object> { { "reason", "customer changed mind" } });

            WorkflowInstance after = store.GetInstance(instance.Id);
            Assert.IsFalse(after.Variables.ContainsKey("reason"),
                "the payload of a non-interrupting boundary must not leak into the main flow.");
        }

        [TestMethod]
        public void Interrupting_PayloadReachesTheMainFlow()
        {
            // Umgekehrt beim Abbruch: DER Hauptfluss laeuft weiter, und der Grund des Abbruchs wird dort
            // gebraucht (Storno-Nummer, Begruendung).
            SaveTaskWithBoundary(b => b.Interrupting = true);
            WorkflowInstance instance = engine.StartWorkflow("wf");

            engine.SignalWorkflow(instance.Id, "Cancelled",
                new Dictionary<string, object> { { "reason", "customer changed mind" } });

            Assert.AreEqual("customer changed mind", store.GetInstance(instance.Id).Variables["reason"]);
        }

        [TestMethod]
        public void Listener_IsDroppedWhenTheTaskMovesOn()
        {
            // Der Empfang gehoert dem Schritt. Verlaesst das Haupt-Token ihn, ist der Empfang
            // gegenstandslos - sonst feuerte eine spaete Nachricht einen Nebenpfad zu einem Schritt, an
            // dem niemand mehr arbeitet.
            SaveTaskWithBoundary(_ => { });
            WorkflowInstance instance = engine.StartWorkflow("wf");
            engine.CompleteUserTask(instance.Id, Task(instance).Id);

            engine.SignalWorkflow(instance.Id, "Cancelled");

            Assert.AreEqual(0, reactions.Count, "a listener of a finished step must not fire.");
            Assert.IsFalse(store.GetInstance(instance.Id).Tokens
                    .Any(t => t.NodeId == "onmsg" && t.Status == TokenStatus.Waiting),
                "the listener must be cleared away with the step, not just stay silent.");
        }

        [TestMethod]
        public void Correlation_IsEvaluatedAgainstTheStepScope()
        {
            SaveTaskWithBoundary(b => b.CorrelationExpression = "orderNo");
            WorkflowInstance instance = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "orderNo", "4711" } });

            Assert.AreEqual("4711", Listener(instance).WaitingCorrelation);

            // Der falsche Schluessel erreicht ihn nicht ...
            engine.DeliverSignal("Cancelled", "0815");
            Assert.AreEqual(0, reactions.Count);

            // ... der richtige schon.
            engine.DeliverSignal("Cancelled", "4711");
            CollectionAssert.AreEqual(new[] { "react#1" }, reactions);
        }

        [TestMethod]
        public void BrokenCorrelation_FaultsInsteadOfListeningDeaf()
        {
            // Ein Empfang mit unbrauchbarem Schluessel waere taub - die Nachricht traefe ihn nie, und das
            // faende man erst, wenn der Storno ausbleibt.
            SaveTaskWithBoundary(b => b.CorrelationExpression = "this is not valid $$");

            WorkflowInstance instance = engine.StartWorkflow("wf");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
        }
    }
}
