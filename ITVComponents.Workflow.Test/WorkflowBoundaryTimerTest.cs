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
    /// Prueft den <b>Fristen-Timer am Schritt</b> (<see cref="BoundaryTimerNode"/>): er haengt an einem
    /// Schritt, an dem geparkt wird, und loest nach Ablauf einen NEBENPFAD aus, ohne den Hauptfluss
    /// anzuhalten. Genau darin unterscheidet er sich von einem AND-Split (dort muesste wieder gejoint
    /// werden, und der Hauptfluss haenge bis zum Ablauf der Frist).
    /// </summary>
    [TestClass]
    public class WorkflowBoundaryTimerTest
    {
        private InMemoryWorkflowStore store;
        private ActivityRegistry activities;
        private WorkflowEngine engine;
        private List<string> escalations;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            escalations = new List<string>();
            activities = new ActivityRegistry();
            activities.Register("remind", ctx =>
                escalations.Add(ctx.Variables.TryGetValue("round", out object r) ? $"remind#{r}" : "remind"));
            engine = new WorkflowEngine(store, activities);
        }

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Start → Benutzer-Aufgabe → Ende, mit einem Fristen-Timer an der Aufgabe, dessen Nebenpfad eine
        /// Erinnerung verschickt und in einem Nebenpfad-Ende auslaeuft.
        /// </summary>
        private void SaveTaskWithTimer(Action<BoundaryTimerNode> configure)
        {
            var timer = new BoundaryTimerNode
            {
                Id = "esc", AttachedToNodeId = "task", CountVariable = "round",
                IntervalsInHours = new List<double> { 24 }
            };
            configure(timer);

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new UserActivityNode { Id = "task", TaskKey = "Approve" },
                    timer,
                    new AutomatedActivityNode { Id = "remind", ActivityRef = "remind" },
                    new SidePathEndNode { Id = "side-end" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "task"), F("task", "e"),
                    F("esc", "remind"), F("remind", "side-end")
                }
            });
        }

        private WorkflowInstance Fire(string instanceId, double hoursFromNow)
        {
            WorkflowInstance instance = store.GetInstance(instanceId);
            engine.TriggerTimers(instance, DateTime.UtcNow.AddHours(hoursFromNow));
            return store.GetInstance(instanceId);
        }

        [TestMethod]
        public void Timer_IsArmedWhenTheTaskParks()
        {
            SaveTaskWithTimer(_ => { });
            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance parked = store.GetInstance(inst.Id);
            Token armed = parked.Tokens.Single(t => t.NodeId == "esc");
            Assert.AreEqual(TokenStatus.Waiting, armed.Status);
            Assert.IsNotNull(armed.DueUtc, "die Frist steht als Timer-Faelligkeit am Token.");
            Assert.AreEqual(parked.Tokens.Single(t => t.NodeId == "task").Id, armed.BoundaryOwnerTokenId,
                "der Timer kennt sein Haupt-Token - darueber laeuft seine Lebensdauer.");
            Assert.AreEqual(0, armed.BoundaryIteration);
        }

        [TestMethod]
        public void Timer_FiresSidePath_AndTheTaskKeepsWaiting()
        {
            SaveTaskWithTimer(_ => { });
            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance after = Fire(inst.Id, 25);

            CollectionAssert.AreEqual(new[] { "remind#1" }, escalations,
                "der Nebenpfad ist gelaufen und kennt die Nummer der Ausloesung.");
            Token task = after.Tokens.Single(t => t.NodeId == "task");
            Assert.AreEqual(TokenStatus.Waiting, task.Status, "die Aufgabe wartet unveraendert weiter...");
            Assert.AreEqual("Approve", task.TaskKey, "...und steht weiterhin in der Arbeitsliste.");
            Assert.AreEqual(WorkflowStatus.Waiting, after.Status);
        }

        [TestMethod]
        public void SidePath_WorksOnACopy_AndDoesNotWriteBackIntoTheMainFlow()
        {
            // Die Eskalation schreibt in ihren Scope - im Hauptfluss darf davon nichts ankommen.
            activities.Register("remind", ctx => ctx.Variables["touchedBySidePath"] = true);
            SaveTaskWithTimer(_ => { });
            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance after = Fire(inst.Id, 25);

            Assert.IsFalse(after.Variables.ContainsKey("touchedBySidePath"),
                "was der Nebenpfad rechnet, bleibt im Nebenpfad.");
        }

        [TestMethod]
        public void Timer_Repeats_WithTheDeclaredSchedule()
        {
            // 24h, dann 12h - danach Ruhe (RepeatLast ist aus).
            SaveTaskWithTimer(t => t.IntervalsInHours = new List<double> { 24, 12 });
            WorkflowInstance inst = engine.StartWorkflow("wf");

            Fire(inst.Id, 25);
            Assert.AreEqual(1, escalations.Count);

            WorkflowInstance second = Fire(inst.Id, 25 + 13);
            Assert.AreEqual(2, escalations.Count, "die zweite Frist laeuft ab der ersten Ausloesung.");

            Token armed = second.Tokens.Single(t => t.NodeId == "esc");
            Assert.AreEqual(TokenStatus.Consumed, armed.Status,
                "der Plan ist abgearbeitet - ohne 'letztes wiederholen' schweigt der Timer.");

            Fire(inst.Id, 500);
            Assert.AreEqual(2, escalations.Count, "und feuert auch spaeter nicht mehr.");
        }

        [TestMethod]
        public void Timer_RepeatsTheLastIntervalForever_WhenAsked()
        {
            SaveTaskWithTimer(t =>
            {
                t.IntervalsInHours = new List<double> { 24, 2 };
                t.RepeatLast = true;
            });
            WorkflowInstance inst = engine.StartWorkflow("wf");

            Fire(inst.Id, 25);
            Fire(inst.Id, 28);
            Fire(inst.Id, 31);

            Assert.AreEqual(3, escalations.Count, "nach dem Plan alle 2h weiter.");
            CollectionAssert.AreEqual(new[] { "remind#1", "remind#2", "remind#3" }, escalations,
                "die Nummer zaehlt ueber alle Ausloesungen hoch.");
            Assert.AreEqual(TokenStatus.Waiting,
                store.GetInstance(inst.Id).Tokens.Single(t => t.NodeId == "esc").Status);
        }

        [TestMethod]
        public void CompletingTheTask_KillsTheTimer_AndTheWorkflowCompletes()
        {
            SaveTaskWithTimer(t => t.RepeatLast = true);
            WorkflowInstance inst = engine.StartWorkflow("wf");
            Fire(inst.Id, 25);
            Assert.AreEqual(1, escalations.Count);

            Token task = store.GetInstance(inst.Id).Tokens.Single(t => t.NodeId == "task");
            engine.CompleteUserTask(inst.Id, task.Id, null);
            engine.Advance(store.GetInstance(inst.Id));

            WorkflowInstance done = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, done.Status,
                "der wartende Timer darf den Abschluss nicht blockieren.");
            Assert.IsTrue(done.Tokens.Where(t => t.NodeId == "esc").All(t => t.Status == TokenStatus.Consumed),
                "der Timer ist mit dem Haupt-Token gestorben.");

            Fire(inst.Id, 500);
            Assert.AreEqual(1, escalations.Count, "und eskaliert nach dem Abschluss nicht mehr.");
        }

        [TestMethod]
        public void MainTokenMovingOn_KillsARunningSidePath()
        {
            // Nebenpfad mit eigenem Wartepunkt: er laeuft noch, wenn die Aufgabe erledigt wird.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new UserActivityNode { Id = "task", TaskKey = "Approve" },
                    new BoundaryTimerNode
                    {
                        Id = "esc", AttachedToNodeId = "task",
                        IntervalsInHours = new List<double> { 24 }
                    },
                    new WaitNode { Id = "ack", SignalName = "acknowledged" },
                    new SidePathEndNode { Id = "side-end" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "task"), F("task", "e"), F("esc", "ack"), F("ack", "side-end")
                }
            });

            WorkflowInstance inst = engine.StartWorkflow("wf");
            Fire(inst.Id, 25);

            WorkflowInstance escalated = store.GetInstance(inst.Id);
            Assert.IsTrue(escalated.Tokens.Any(t => t.NodeId == "ack" && t.Status == TokenStatus.Waiting),
                "der Nebenpfad haengt an seinem eigenen Wartepunkt.");

            Token task = escalated.Tokens.Single(t => t.NodeId == "task");
            engine.CompleteUserTask(inst.Id, task.Id, null);
            engine.Advance(store.GetInstance(inst.Id));

            WorkflowInstance done = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, done.Status);
            Assert.IsTrue(done.Tokens.Where(t => t.NodeId == "ack").All(t => t.Status == TokenStatus.Consumed),
                "ein laufender Nebenpfad wird mit abgeraeumt, nicht nur der Timer.");
        }

        [TestMethod]
        public void InterruptingTimer_TakesTheMainTokenAndClosesTheTask()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new UserActivityNode { Id = "task", TaskKey = "Approve" },
                    new BoundaryTimerNode
                    {
                        Id = "esc", AttachedToNodeId = "task", Interrupting = true,
                        CountVariable = "round", IntervalsInHours = new List<double> { 24 }
                    },
                    new AutomatedActivityNode { Id = "reject", ActivityRef = "remind" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "task"), F("task", "e"), F("esc", "reject"), F("reject", "e")
                }
            });

            WorkflowInstance inst = engine.StartWorkflow("wf");
            WorkflowInstance after = Fire(inst.Id, 25);

            Assert.AreEqual(WorkflowStatus.Completed, after.Status,
                "das HAUPT-Token hat den Eskalationspfad genommen und den Workflow beendet.");
            Token task = after.Tokens.Single(t => t.Id == inst.Tokens.First().Id
                                                  || t.BoundaryOwnerTokenId == null && t.NodeId == "e");
            Assert.IsNull(task.TaskKey, "die Aufgabe ist aus der Arbeitsliste verschwunden.");
            Assert.AreEqual(1, after.Variables["round"], "die Nummer landet im Hauptfluss.");
            CollectionAssert.AreEqual(new[] { "remind#1" }, escalations);
        }

        [TestMethod]
        public void Timer_OnAStepWhereNothingParks_NeverArms()
        {
            // Eine gewoehnliche Aktivitaet laeuft synchron durch - dort gibt es nichts scharf zu stellen.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "fast", ActivityRef = "remind" },
                    new BoundaryTimerNode
                    {
                        Id = "esc", AttachedToNodeId = "fast", IntervalsInHours = new List<double> { 24 }
                    },
                    new SidePathEndNode { Id = "side-end" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "fast"), F("fast", "e"), F("esc", "side-end") }
            });

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance done = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, done.Status);
            Assert.IsFalse(done.Tokens.Any(t => t.NodeId == "esc"),
                "kein Parken, kein Timer - der Validator meldet das beim Speichern als Warnung.");
        }
    }
}
