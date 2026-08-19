using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft die <b>Ausloeser</b>: dass eine eintreffende Nachricht eine Instanz entstehen laesst
    /// (Message-Start) und dass ein faelliger Zeitplan sie startet - samt der drei Modi, dem
    /// Ueberlappungs-Riegel und dem Aufbau des Verzeichnisses beim Speichern der Definition.
    /// </summary>
    /// <remarks>
    /// Ueber den EF-Store, weil das Verzeichnis genau dort lebt: die Materialisierung beim Speichern und
    /// der Aufgriff mit Anspruch sind Datenbank-Verhalten, das ein Speicher-Store nicht nachbildet.
    /// </remarks>
    [TestClass]
    public class WorkflowStartTriggerTest
    {
        /// <summary>Taeglich um 08:00, ab 01.01.2020 - ein Muster, das gestern schon galt.</summary>
        private const string DailyAtEight = "d20200101080001";

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

        // --- Das Verzeichnis ------------------------------------------------------------------------

        [TestMethod]
        public void SaveDefinition_MaterializesTriggers()
        {
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(WithMessageStart("m", "OrderReceived", MessageStartMode.AlwaysStart));

            IReadOnlyList<WorkflowStartTrigger> found = store.FindMessageTriggers("OrderReceived");
            Assert.AreEqual(1, found.Count, "the message trigger must be findable by its name.");
            Assert.AreEqual("m", found[0].DefinitionId);
            Assert.AreEqual("s", found[0].NodeId);
        }

        [TestMethod]
        public void SaveDefinition_PublicDefinition_GetsNoTrigger()
        {
            // Eine oeffentliche Definition gehoert allen - "fuer alle einmal starten" waere eine voellig
            // andere Zusage als die, die ein Ausloeser macht.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithMessageStart("pub", "OrderReceived", MessageStartMode.AlwaysStart);
            definition.IsPublic = true;
            store.SaveDefinition(definition);

            Assert.AreEqual(0, store.FindMessageTriggers("OrderReceived").Count);
        }

        [TestMethod]
        public void SaveDefinition_NewVersion_ReplacesTheTriggerOfTheOldOne()
        {
            // Sonst horchte jede jemals gespeicherte Fassung weiter mit, und EINE Nachricht startete so
            // viele Instanzen, wie es Versionen gibt.
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(WithMessageStart("m", "OrderReceived", MessageStartMode.AlwaysStart));

            WorkflowDefinition v2 = WithMessageStart("m", "OrderReceived", MessageStartMode.AlwaysStart);
            v2.Version = 2;
            store.SaveDefinition(v2);

            IReadOnlyList<WorkflowStartTrigger> found = store.FindMessageTriggers("OrderReceived");
            Assert.AreEqual(1, found.Count, "only one trigger may survive - the newest.");
            Assert.AreEqual(2, found[0].DefinitionVersion);
        }

        [TestMethod]
        public void SaveDefinition_UnchangedSchedule_KeepsItsState()
        {
            // Ohne das finge jedes Speichern der Definition - auch eine Aenderung an ganz anderer Stelle -
            // den Zeitplan von vorn an.
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(WithSchedule("s1", DailyAtEight));
            DateTime marker = DateTime.UtcNow.AddDays(-1);
            SetTriggerState(marker, marker.AddDays(-1), "old-instance");

            WorkflowDefinition again = WithSchedule("s1", DailyAtEight);
            again.Version = 2;
            store.SaveDefinition(again);

            WorkflowStartTrigger trigger = SingleScheduleTrigger(store);
            Assert.AreEqual(marker.ToString("O"), trigger.NextDueUtc?.ToString("O"),
                "the due date of an unchanged schedule must survive a save.");
            Assert.AreEqual("old-instance", trigger.LastInstanceId);
        }

        // --- Message-Start --------------------------------------------------------------------------

        [TestMethod]
        public void Message_AlwaysStart_CreatesAnInstance()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(WithMessageStart("m", "OrderReceived", MessageStartMode.AlwaysStart));

            int reached = engine.DeliverSignal("OrderReceived", "order-4711",
                new Dictionary<string, object> { { "amount", 100 } });

            Assert.AreEqual(1, reached);
            WorkflowInstance started = SingleInstance(store, "m");
            Assert.AreEqual("order-4711", started.CorrelationKey,
                "the key of the message becomes the correlation key of the new instance.");
            Assert.AreEqual(100, started.Variables["amount"], "the payload is the start data.");
        }

        [TestMethod]
        public void Message_AlwaysStart_StartsEvenWhileAnInstanceWaits()
        {
            // Der Kern der Vorgabe: was geschieht, steht im Modell - es haengt NICHT davon ab, ob
            // zufaellig gerade jemand auf denselben Namen wartet.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(WithMessageStart("m", "OrderReceived", MessageStartMode.AlwaysStart,
                waitOnSameName: true));
            engine.DeliverSignal("OrderReceived", "order-4711");

            engine.DeliverSignal("OrderReceived", "order-4711");

            Assert.AreEqual(2, InstancesOf(store, "m").Count);
        }

        [TestMethod]
        public void Message_CorrelateOrStart_PrefersTheWaitingInstance()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(WithMessageStart("m", "OrderReceived", MessageStartMode.CorrelateOrStart,
                waitOnSameName: true));

            engine.DeliverSignal("OrderReceived", "order-4711");   // legt an, parkt am Wartepunkt
            engine.DeliverSignal("OrderReceived", "order-4711");   // findet den Wartepunkt

            Assert.AreEqual(1, InstancesOf(store, "m").Count,
                "the second message continued the waiting instance instead of opening a second case.");
        }

        [TestMethod]
        public void Message_StartIfNoneRunning_BlocksTheSecondOne()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(WithMessageStart("m", "OrderReceived", MessageStartMode.StartIfNoneRunning));

            engine.DeliverSignal("OrderReceived", "order-4711");
            engine.DeliverSignal("OrderReceived", "order-4711");

            Assert.AreEqual(1, InstancesOf(store, "m").Count, "the bolt against double submission held.");
        }

        [TestMethod]
        public void Message_StartIfNoneRunning_AllowsADifferentKey()
        {
            // Der Riegel gilt der SACHE, nicht der Definition - zwei verschiedene Bestellungen sind zwei
            // Vorgaenge.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(WithMessageStart("m", "OrderReceived", MessageStartMode.StartIfNoneRunning));

            engine.DeliverSignal("OrderReceived", "order-4711");
            engine.DeliverSignal("OrderReceived", "order-4712");

            Assert.AreEqual(2, InstancesOf(store, "m").Count);
        }

        // --- Zeitplan -------------------------------------------------------------------------------

        [TestMethod]
        public void Schedule_WhenDue_StartsAndMovesOn()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(WithSchedule("s1", DailyAtEight));
            DateTime nowUtc = MakeDue();

            int started = engine.TriggerDueStarts(nowUtc, "runner-a", TimeSpan.FromMinutes(5));

            Assert.AreEqual(1, started);
            WorkflowInstance instance = SingleInstance(store, "s1");
            WorkflowStartTrigger after = SingleScheduleTrigger(store);
            Assert.AreEqual(instance.Id, after.LastInstanceId);
            Assert.IsNotNull(after.LastRunUtc, "the run must be recorded - the pattern's 'immediately' hangs on it.");
            Assert.IsTrue(after.NextDueUtc > nowUtc,
                "the next date must move forward, otherwise the trigger fires again on every poll.");
        }

        [TestMethod]
        public void Schedule_NotDue_StartsNothing()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            store.SaveDefinition(WithSchedule("s1", DailyAtEight));

            Assert.AreEqual(0, engine.TriggerDueStarts(DateTime.UtcNow, "runner-a", TimeSpan.FromMinutes(5)));
            Assert.AreEqual(0, InstancesOf(store, "s1").Count);
        }

        [TestMethod]
        public void Schedule_SecondRunnerGetsNothing_WhileTheClaimHolds()
        {
            // Der Anspruch ist hier keine blosse Optimierung: es gibt keine Instanz, deren Version einen
            // zweiten Runner ausbremsen koennte. Ohne ihn liefe derselbe Auftrag je Knoten einmal an.
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(WithSchedule("s1", DailyAtEight));
            DateTime nowUtc = MakeDue();

            IReadOnlyList<WorkflowStartTrigger> first =
                store.ClaimDueScheduleTriggers(nowUtc, "runner-a", TimeSpan.FromMinutes(5), 10);
            IReadOnlyList<WorkflowStartTrigger> second =
                store.ClaimDueScheduleTriggers(nowUtc, "runner-b", TimeSpan.FromMinutes(5), 10);

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(0, second.Count, "the second runner must not get the same schedule.");
        }

        [TestMethod]
        public void Schedule_SkipWhilePreviousRuns_SkipsButKeepsTicking()
        {
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            WorkflowDefinition definition = WithSchedule("s1", DailyAtEight);
            ((StartNode)definition.Nodes[0]).ScheduleStart.SkipWhilePreviousRuns = true;
            store.SaveDefinition(definition);

            DateTime firstRun = MakeDue();
            engine.TriggerDueStarts(firstRun, "runner-a", TimeSpan.FromMinutes(5));
            Assert.AreEqual(1, InstancesOf(store, "s1").Count, "precondition: the first run is waiting.");

            DateTime secondRun = MakeDue();
            int started = engine.TriggerDueStarts(secondRun, "runner-a", TimeSpan.FromMinutes(5));

            Assert.AreEqual(0, started, "the previous run is still going.");
            Assert.AreEqual(1, InstancesOf(store, "s1").Count);
            Assert.IsTrue(SingleScheduleTrigger(store).NextDueUtc > secondRun,
                "a skipped date must still move on - otherwise the trigger runs hot on every poll.");
        }

        [TestMethod]
        public void Schedule_BrokenDefinition_DoesNotLoopForever()
        {
            // Ein scheiternder Start darf nicht faellig BLEIBEN: sonst wird aus einem einzelnen Fehler
            // eine Last, die im Takt des Runners wiederkehrt.
            EfWorkflowStore store = NewStore();
            WorkflowEngine engine = EngineOver(store);
            WorkflowDefinition definition = WithSchedule("s1", DailyAtEight);
            store.SaveDefinition(definition);
            using (var ctx = new WorkflowContext(options))
            {
                // Die Definition unter dem Ausloeser unbrauchbar machen (kein Start-Knoten mehr).
                // Eine Definition ohne Knoten: der Start scheitert an "hat keinen Start-Knoten". Bewusst so
                // und nicht ueber den Diskriminator - der Test soll nicht an dessen Schreibweise haengen.
                WorkflowDefinitionRow row = ctx.WorkflowDefinitions.Single();
                row.DefinitionJson = "{}";
                ctx.SaveChanges();
            }

            DateTime nowUtc = MakeDue();
            int started = engine.TriggerDueStarts(nowUtc, "runner-a", TimeSpan.FromMinutes(5));

            Assert.AreEqual(0, started);
            Assert.IsTrue(SingleScheduleTrigger(store).NextDueUtc > nowUtc,
                "even a failed start must move the schedule forward.");
        }

        // --- Aufbau ---------------------------------------------------------------------------------

        private EfWorkflowStore NewStore() => new EfWorkflowStore(() => new WorkflowContext(options));

        private static WorkflowEngine EngineOver(EfWorkflowStore store)
            => new WorkflowEngine(store, new ActivityRegistry());

        /// <summary>Macht den (einzigen) Zeitplan faellig und liefert den passenden "Jetzt".</summary>
        private DateTime MakeDue()
        {
            DateTime nowUtc = DateTime.UtcNow;
            using var ctx = new WorkflowContext(options);
            foreach (WorkflowStartTriggerRow row in ctx.WorkflowStartTriggers.ToList())
            {
                row.NextDueUtc = nowUtc.AddMinutes(-1);
                row.LeaseOwner = null;
                row.LeaseUntilUtc = null;
            }

            ctx.SaveChanges();
            return nowUtc;
        }

        private void SetTriggerState(DateTime nextDue, DateTime lastRun, string lastInstanceId)
        {
            using var ctx = new WorkflowContext(options);
            WorkflowStartTriggerRow row = ctx.WorkflowStartTriggers.Single();
            row.NextDueUtc = nextDue;
            row.LastRunUtc = lastRun;
            row.LastInstanceId = lastInstanceId;
            ctx.SaveChanges();
        }

        private WorkflowStartTrigger SingleScheduleTrigger(EfWorkflowStore store)
            => store.ClaimDueScheduleTriggers(DateTime.UtcNow.AddYears(100), "probe", TimeSpan.Zero, 10)
                .Single(t => t.Kind == WorkflowStartTriggerKind.Schedule);

        private List<WorkflowInstance> InstancesOf(EfWorkflowStore store, string definitionId)
        {
            using var ctx = new WorkflowContext(options);
            return ctx.WorkflowInstances.Where(i => i.DefinitionId == definitionId)
                .Select(i => i.Id)
                .ToList()
                .Select(store.GetInstance)
                .ToList();
        }

        private WorkflowInstance SingleInstance(EfWorkflowStore store, string definitionId)
            => InstancesOf(store, definitionId).Single();

        /// <summary>
        /// Start -&gt; Wartepunkt -&gt; Ende, mit einem Nachrichten-Ausloeser am Start.
        /// </summary>
        /// <param name="waitOnSameName">
        /// Wartet der Prozess auf DENSELBEN Namen, der ihn startet? Das entscheidet, was eine zweite
        /// Nachricht antrifft - und die beiden Faelle pruefen verschiedene Dinge: mit demselben Namen laesst
        /// sich zeigen, ob fortgesetzt oder neu eroeffnet wird; mit einem anderen bleibt die erste Instanz
        /// offen, und nur so ist der Riegel gegen Doppelanlagen ueberhaupt pruefbar (sonst beendete die
        /// zweite Nachricht die erste Instanz und der Riegel faende nichts mehr vor, das laeuft).
        /// </param>
        private static WorkflowDefinition WithMessageStart(string id, string signalName, MessageStartMode mode,
            bool waitOnSameName = false)
            => new WorkflowDefinition
            {
                Id = id,
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode
                    {
                        Id = "s",
                        MessageStart = new MessageStartTrigger { SignalName = signalName, Mode = mode }
                    },
                    new WaitNode { Id = "w", SignalName = waitOnSameName ? signalName : "never" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->e", SourceId = "w", TargetId = "e" }
                }
            };

        /// <summary>Start -&gt; Wartepunkt -&gt; Ende, mit einem Zeitplan am Start.</summary>
        private static WorkflowDefinition WithSchedule(string id, string pattern)
            => new WorkflowDefinition
            {
                Id = id,
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode
                    {
                        Id = "s",
                        ScheduleStart = new ScheduleStartTrigger { Pattern = pattern }
                    },
                    // Der Wartepunkt haelt die Instanz offen - Voraussetzung dafuer, den
                    // Ueberlappungs-Riegel ueberhaupt pruefen zu koennen.
                    new WaitNode { Id = "w", SignalName = "never" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->e", SourceId = "w", TargetId = "e" }
                }
            };
    }
}
