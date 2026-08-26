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
                TechnicalName = "wf",
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

        // --- Fristen als Ausdruck -------------------------------------------------------------------
        // Die Frist ist ein CScript-Ausdruck ueber dem Scope des Schritts. Erlaubt sind TimeSpan (Dauer),
        // DateTime (Zeitpunkt) und eine Zahl (Stunden) - dieselbe Konvention wie beim gewoehnlichen Timer,
        // um die Kurzform der frueheren Stunden-Liste erweitert.

        /// <summary>Setzt die Fristen als Ausdruecke (statt der alten Stundenliste).</summary>
        private static void Deadlines(BoundaryTimerNode timer, params string[] expressions)
        {
            timer.IntervalsInHours = new List<double>();
            timer.Deadlines = expressions
                .Select(e => new BoundaryDeadline { Expression = e })
                .ToList();
        }

        [TestMethod]
        public void Deadline_MayBeANumber_CountingAsHours()
        {
            SaveTaskWithTimer(t => Deadlines(t, "20 + 4"));
            WorkflowInstance inst = engine.StartWorkflow("wf");

            Assert.AreEqual(0, Fire(inst.Id, 23).Tokens.Count(t => t.NodeId == "remind"),
                "vor Ablauf der 24h passiert nichts.");
            Fire(inst.Id, 25);
            CollectionAssert.AreEqual(new[] { "remind#1" }, escalations);
        }

        [TestMethod]
        public void Deadline_MayReturnATimeSpan()
        {
            // CScript ruft statische Methoden ueber den Typnamen in Anfuehrungszeichen auf - dieselbe
            // Schreibweise wie beim gewoehnlichen Timer (siehe WorkflowEngineTest).
            SaveTaskWithTimer(t => Deadlines(t, "'System.TimeSpan'.FromHours(24)"));
            WorkflowInstance inst = engine.StartWorkflow("wf");

            Fire(inst.Id, 25);
            CollectionAssert.AreEqual(new[] { "remind#1" }, escalations);
        }

        [TestMethod]
        public void Deadline_MayReturnAnAbsoluteDateTime()
        {
            // Absolute Frist aus einer Instanz-Variablen - der Fall "bis zum vereinbarten Termin".
            SaveTaskWithTimer(t => Deadlines(t, "faelligAm"));
            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "faelligAm", DateTime.UtcNow.AddHours(24) } });

            Token armed = store.GetInstance(inst.Id).Tokens.Single(t => t.NodeId == "esc");
            Assert.IsTrue(armed.DueUtc > DateTime.UtcNow.AddHours(23)
                          && armed.DueUtc < DateTime.UtcNow.AddHours(25),
                "die absolute Frist steht unveraendert am Token.");

            Fire(inst.Id, 25);
            CollectionAssert.AreEqual(new[] { "remind#1" }, escalations);
        }

        [TestMethod]
        public void Deadline_EvaluatesAgainstTheScopeOfTheStep()
        {
            SaveTaskWithTimer(t => Deadlines(t, "stunden"));
            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "stunden", 10 } });

            Assert.AreEqual(0, escalations.Count);
            Fire(inst.Id, 11);
            CollectionAssert.AreEqual(new[] { "remind#1" }, escalations,
                "die Frist kommt aus den Variablen der Instanz.");
        }

        [TestMethod]
        public void RepeatLast_WithAnAbsoluteDeadline_StopsInsteadOfFiringForever()
        {
            // Der gefaehrliche Fall: ein absoluter Zeitpunkt bleibt beim Wiederholen derselbe und waere ab
            // der zweiten Runde vergangen - ohne Schutz feuerte der Timer in einer Schleife.
            SaveTaskWithTimer(t =>
            {
                Deadlines(t, "faelligAm");
                t.RepeatLast = true;
            });
            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "faelligAm", DateTime.UtcNow.AddHours(24) } });

            WorkflowInstance after = Fire(inst.Id, 25);
            Assert.AreEqual(1, escalations.Count, "einmal ausgeloest...");
            Assert.AreEqual(TokenStatus.Consumed, after.Tokens.Single(t => t.NodeId == "esc").Status,
                "...danach verstummt der Timer, statt endlos zu feuern.");

            Fire(inst.Id, 100);
            Assert.AreEqual(1, escalations.Count);
        }

        [TestMethod]
        public void FailingDeadline_OfAReminder_LeavesTheTaskAlone_ButIsRecorded()
        {
            SaveTaskWithTimer(t => Deadlines(t, "gibtsNicht.quatsch()"));
            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance parked = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Waiting, parked.Status,
                "eine tadellose Aufgabe darf nicht an einer kaputten Erinnerung sterben.");
            Assert.AreEqual("Approve", parked.Tokens.Single(t => t.NodeId == "task").TaskKey);
            Assert.IsFalse(parked.Tokens.Any(t => t.NodeId == "esc" && t.Status == TokenStatus.Waiting),
                "der Timer wurde nicht scharf.");
            Assert.IsTrue(parked.History.Any(h => h.Event == "BoundaryTimerFailed"
                                                  && h.Severity == HistorySeverity.Error),
                "...aber der Grund steht in der Historie - still verschwinden darf das nicht.");
        }

        [TestMethod]
        public void FailingDeadline_OfAnInterruptingTimer_FaultsTheInstance()
        {
            // Beim unterbrechenden Timer ist die Frist die einzige Ausstiegstuer des Schritts. Faellt sie
            // aus, stuende die Aufgabe fuer immer - das ist ein Fehler, keine fehlende Erinnerung.
            SaveTaskWithTimer(t =>
            {
                t.Interrupting = true;
                Deadlines(t, "gibtsNicht.quatsch()");
            });
            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance after = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, after.Status);
            StringAssert.Contains(after.FaultMessage, "esc");
        }

        [TestMethod]
        public void LegacyHourList_KeepsWorking_AndMigratesOnDemand()
        {
            // Bereits gespeicherte Definitionen tragen die Stundenliste. Sie muessen unveraendert laufen -
            // ein stiller Ausfall waere im Graphen nicht zu sehen.
            var timer = new BoundaryTimerNode { Id = "esc", IntervalsInHours = new List<double> { 24, 12 } };

            IReadOnlyList<BoundaryDeadline> effective = timer.EffectiveDeadlines();
            Assert.AreEqual(2, effective.Count);
            Assert.AreEqual("24", effective[0].Expression);
            Assert.AreEqual("12", effective[1].Expression);

            Assert.IsTrue(timer.MigrateLegacyDeadlines(), "die Migration hat etwas zu tun...");
            Assert.AreEqual(2, timer.Deadlines.Count);
            Assert.AreEqual(0, timer.IntervalsInHours.Count, "...und raeumt das alte Feld ab.");
            Assert.IsFalse(timer.MigrateLegacyDeadlines(), "ein zweiter Lauf aendert nichts mehr.");
        }

        [TestMethod]
        public void Deadlines_WinOverTheLegacyHourList()
        {
            var timer = new BoundaryTimerNode
            {
                Id = "esc",
                IntervalsInHours = new List<double> { 99 },
                Deadlines = new List<BoundaryDeadline> { new BoundaryDeadline { Expression = "1" } }
            };

            Assert.AreEqual("1", timer.EffectiveDeadlines().Single().Expression,
                "die neue Liste ist massgeblich, sobald sie etwas enthaelt.");
            Assert.IsFalse(timer.MigrateLegacyDeadlines(), "und wird von der Migration nicht ueberschrieben.");
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
                TechnicalName = "wf",
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
                TechnicalName = "wf",
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
                TechnicalName = "wf",
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
