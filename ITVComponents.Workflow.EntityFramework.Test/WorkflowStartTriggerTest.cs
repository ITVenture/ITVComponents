using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Runtime;
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

            IReadOnlyList<WorkflowStartTriggerMatch> found = MessageMatches(store, "OrderReceived");
            Assert.AreEqual(1, found.Count, "the message trigger must be findable by its name.");
            Assert.AreEqual("m", found[0].Trigger.DefinitionId);
            Assert.AreEqual("s", found[0].Trigger.NodeId);
            Assert.IsNotNull(found[0].Activation,
                "a tenant-owned definition gets its one activation by itself - nobody ticks a box for it.");
        }

        [TestMethod]
        public void SaveDefinition_PublicDefinition_GetsATriggerButNoActivation()
        {
            // Eine oeffentliche Definition DEKLARIERT ihren Einstieg wie jede andere - sie feuert nur
            // fuer niemanden, solange ihn kein Mandant uebernommen hat. Der Ausloeser ist die
            // Deklaration, die Aktivierung ist die Zusage.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithMessageStart("pub", "OrderReceived", MessageStartMode.AlwaysStart);
            definition.IsPublic = true;
            store.SaveDefinition(definition);

            using (var ctx = new WorkflowContext(options))
            {
                Assert.AreEqual(1, ctx.WorkflowStartTriggers.Count(),
                    "the declaration exists - it is what a tenant later ticks.");
                Assert.AreEqual(0, ctx.WorkflowStartTriggerActivations.Count(),
                    "nobody has taken it over yet.");
            }

            Assert.AreEqual(0, MessageMatches(store, "OrderReceived").Count,
                "without an activation there is no tenant to start for.");
        }

        [TestMethod]
        public void PublicDefinition_AfterActivation_StartsForThatTenant()
        {
            // Der Kern von LocalActivation: derselbe zentrale Einstieg, aber er laeuft im Mandanten, der
            // ihn sich geholt hat - nicht im (nicht vorhandenen) Mandanten der Definition.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithMessageStart("pub", "OrderReceived", MessageStartMode.AlwaysStart);
            definition.IsPublic = true;
            ((StartNode)definition.Nodes.First(n => n is StartNode)).MessageStart.AllowLocalActivation = true;
            store.SaveDefinition(definition);

            store.SaveActivation(new WorkflowStartTriggerActivation
            {
                OwnerTenantId = null,
                DefinitionId = "pub",
                NodeId = "s",
                Kind = WorkflowStartTriggerKind.Message,
                TenantId = "acme",
                Enabled = true,
                ActivatedBy = "tester"
            });

            IReadOnlyList<WorkflowStartTriggerMatch> forAcme =
                store.FindMessageTriggers("OrderReceived", "acme").Matches;
            Assert.AreEqual(1, forAcme.Count, "the tenant that took it over gets it.");
            Assert.AreEqual("acme", forAcme[0].TenantId, "and it runs in HIS tenant, not the definition's.");

            WorkflowMessageTriggerLookup forBeta = store.FindMessageTriggers("OrderReceived", "beta");
            Assert.AreEqual(0, forBeta.Matches.Count, "a tenant who did not take it over gets nothing.");
            Assert.AreEqual(1, forBeta.SuppressedByTenant.Count,
                "and that must be visible - otherwise it is indistinguishable from 'nobody listens'.");
        }

        [TestMethod]
        public void Message_WithoutOrigin_DoesNotStartForeignTenants()
        {
            // Die Flanke, die es auch vor LocalActivation schon gab: EINE mandantenlose Nachricht liess
            // bei hundert Mandanten hundert Vorgaenge entstehen.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithMessageStart("m", "OrderReceived", MessageStartMode.AlwaysStart);
            definition.TenantId = "acme";
            store.SaveDefinition(definition);

            Assert.AreEqual(0, store.FindMessageTriggers("OrderReceived", null).Matches.Count,
                "no origin means no start - unless the node says otherwise.");
            Assert.AreEqual(1, store.FindMessageTriggers("OrderReceived", "acme").Matches.Count,
                "the owning tenant's own message still starts it.");
        }

        [TestMethod]
        public void Message_WithoutOrigin_StartsWhenTheNodeAllowsIt()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithMessageStart("m", "OrderReceived", MessageStartMode.AlwaysStart);
            definition.TenantId = "acme";
            ((StartNode)definition.Nodes.First(n => n is StartNode)).MessageStart.AllowTenantlessStart = true;
            store.SaveDefinition(definition);

            Assert.AreEqual(1, store.FindMessageTriggers("OrderReceived", null).Matches.Count,
                "'AllowTenantlessStart' is exactly the deliberate exception.");
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

            IReadOnlyList<WorkflowStartTriggerMatch> found = MessageMatches(store, "OrderReceived");
            Assert.AreEqual(1, found.Count, "only one trigger may survive - the newest.");
            Assert.AreEqual(2, found[0].Trigger.DefinitionVersion);
        }

        [TestMethod]
        public void SaveDefinition_NewVersion_KeepsTheActivation()
        {
            // DIE Falle des Umbaus: die Ausloeser-Zeilen werden bei jedem Speichern weggeraeumt und neu
            // eingefuegt, ihr TriggerKey ist danach ein anderer. Haenge die Aktivierung daran, verloere
            // jeder Mandant seine Uebernahme bei der ersten Korrektur an der zentralen Definition - und
            // zwar lautlos: der Zeitplan liefe einfach nicht mehr.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition v1 = WithSchedule("s1", DailyAtEight);
            store.SaveDefinition(v1);

            int keyBefore;
            using (var ctx = new WorkflowContext(options))
            {
                keyBefore = ctx.WorkflowStartTriggers.Single().TriggerKey;
            }

            WorkflowDefinition v2 = WithSchedule("s1", DailyAtEight);
            v2.Version = 2;
            store.SaveDefinition(v2);

            using (var ctx = new WorkflowContext(options))
            {
                Assert.AreEqual(1, ctx.WorkflowStartTriggerActivations.Count(),
                    "the activation survives - it hangs on the definition's identity, not on a row number.");
                Assert.AreEqual(2, ctx.WorkflowStartTriggers.Single().DefinitionVersion,
                    "precondition: the trigger really was rebuilt for the new version.");
            }

            Assert.IsNotNull(SingleScheduleTrigger(store),
                $"the schedule must still be claimable (trigger key before the save was {keyBefore}).");
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

            WorkflowStartTriggerActivation trigger = SingleScheduleTrigger(store);
            Assert.AreEqual(marker.ToString("O"), trigger.NextDueUtc?.ToString("O"),
                "the due date of an unchanged schedule must survive a save.");
            Assert.AreEqual("old-instance", trigger.LastInstanceId);
        }

        [TestMethod]
        public void SaveDefinition_ChangedSchedule_StartsOver()
        {
            // Die Kehrseite: ein umgeschriebener Zeitplan ist ein ANDERER Plan, und sein erster Lauf
            // gehoert ihm. Sonst greift ein "sofort"-Kennzeichen nie, weil "schon mal gelaufen" aus der
            // Zeit davor stammt.
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(WithSchedule("s1", DailyAtEight));
            DateTime marker = DateTime.UtcNow.AddDays(-1);
            SetTriggerState(marker, marker.AddDays(-1), "old-instance");

            WorkflowDefinition changed = WithSchedule("s1", "d20200101090001");
            changed.Version = 2;
            store.SaveDefinition(changed);

            using var ctx = new WorkflowContext(options);
            WorkflowStartTriggerActivationRow after = ctx.WorkflowStartTriggerActivations.Single();
            Assert.IsNull(after.LastRunUtc, "a rewritten schedule gets a fresh run.");
            Assert.IsNull(after.LastInstanceId);
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
            WorkflowStartTriggerActivation after = SingleScheduleTrigger(store);
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

            IReadOnlyList<WorkflowStartTriggerMatch> first =
                store.ClaimDueScheduleTriggers(nowUtc, "runner-a", TimeSpan.FromMinutes(5), 10);
            IReadOnlyList<WorkflowStartTriggerMatch> second =
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

        // --- Feature-Gate ---------------------------------------------------------------------------

        [TestMethod]
        public void Schedule_WithoutTheFeature_SkipsButKeepsTicking()
        {
            // Der Fall, um den es bei der ganzen Konstruktion geht: ein zentral gepflegter Zahlungslauf
            // darf nicht weiterlaufen, nachdem das Abonnement des Mandanten ausgelaufen ist. Das Feature
            // haengt am Mandanten und ist deshalb die EINZIGE Bedingung, die auch ohne Benutzer gilt.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithSchedule("s1", DailyAtEight);
            definition.RequiredFeature = "ITVPayrun";
            store.SaveDefinition(definition);

            var engine = new WorkflowEngine(store, new ActivityRegistry(),
                featureGate: new DenyingFeatureGate());
            DateTime nowUtc = MakeDue();

            int started = engine.TriggerDueStarts(nowUtc, "runner-a", TimeSpan.FromMinutes(5));

            Assert.AreEqual(0, started, "without the feature nothing may start.");
            Assert.AreEqual(0, InstancesOf(store, "s1").Count);
            Assert.IsTrue(SingleScheduleTrigger(store).NextDueUtc > nowUtc,
                "the schedule keeps ticking - it must resume by itself once the feature is back.");

            using var ctx = new WorkflowContext(options);
            Assert.IsTrue(ctx.WorkflowStartTriggerActivations.Single().Enabled,
                "skipping must NOT untick the box - after a short lapse nobody would notice they have to "
                + "tick it again.");
        }

        [TestMethod]
        public void Schedule_WithTheFeature_Starts()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithSchedule("s1", DailyAtEight);
            definition.RequiredFeature = "ITVPayrun";
            store.SaveDefinition(definition);

            WorkflowEngine engine = EngineOver(store);   // Vorgabe-Gate: erlaubt alles
            DateTime nowUtc = MakeDue();

            Assert.AreEqual(1, engine.TriggerDueStarts(nowUtc, "runner-a", TimeSpan.FromMinutes(5)),
                "an unwired host must behave exactly as before the feature condition existed.");
        }

        /// <summary>Ein Gate, das alles verweigert - der abgelaufene Mandant.</summary>
        private sealed class DenyingFeatureGate : IWorkflowTenantFeatureGate
        {
            public bool IsEnabled(string tenantId, string featureName) => false;
        }

        // --- Der Wechsel der Sichtbarkeit -----------------------------------------------------------

        [TestMethod]
        public void TenantOwnedBecomesPublic_MovesTriggerAndActivationAlong()
        {
            // Der einzige Weg, auf dem eine Definition den Besitzer wechselt. Frueher wurden dabei die
            // Zeilen des alten Besitzers ueber den NEUEN gesucht - also nicht gefunden: es entstand eine
            // zweite Ausloeser-Zeile, die alte blieb liegen und wurde nie wieder angefasst.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithSchedule("wf", DailyAtEight);
            definition.TenantId = "acme";
            ((StartNode)definition.Nodes.First(n => n is StartNode)).ScheduleStart.AllowLocalActivation = true;
            store.SaveDefinition(definition);

            definition.IsPublic = true;
            definition.TenantId = null;
            store.SaveDefinition(definition);

            using (var ctx = new WorkflowContext(options))
            {
                WorkflowStartTriggerRow trigger = ctx.WorkflowStartTriggers.IgnoreQueryFilters().Single();
                Assert.IsNull(trigger.TenantId, "the trigger belongs to the definition - it has no owner now.");
                Assert.IsTrue(trigger.IsPublic);

                WorkflowStartTriggerActivationRow activation =
                    ctx.WorkflowStartTriggerActivations.IgnoreQueryFilters().Single();
                Assert.IsNull(activation.OwnerTenantId,
                    "the takeover hangs on the public definition now - the runner and the overview both "
                    + "look it up through OwnerTenantId.");
                Assert.AreEqual("acme", activation.TenantId, "and acme still runs it.");
                Assert.IsTrue(activation.Enabled, "nobody unticked anything.");
            }

            Assert.AreEqual(1, store.FindActivatableTriggers("beta").Count,
                "and it is on offer centrally - that is what public plus 'may be taken over' means.");
        }

        [TestMethod]
        public void PublicBecomesTenantOwned_KeepsTheNewOwnerAndDropsTheOthers()
        {
            // Der Rueckweg. Die Uebernahme des kuenftigen Besitzers wird mitgezogen (samt Lauf-Zustand),
            // die der anderen zeigt auf einen Prozess, den sie ab jetzt nicht mehr sehen duerfen.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = WithSchedule("wf", DailyAtEight);
            definition.IsPublic = true;
            ((StartNode)definition.Nodes.First(n => n is StartNode)).ScheduleStart.AllowLocalActivation = true;
            store.SaveDefinition(definition);

            foreach (string tenant in new[] { "acme", "beta" })
            {
                store.SaveActivation(new WorkflowStartTriggerActivation
                {
                    OwnerTenantId = null,
                    DefinitionId = "wf",
                    NodeId = "s",
                    Kind = WorkflowStartTriggerKind.Schedule,
                    TenantId = tenant,
                    Enabled = true,
                    ActivatedBy = "tester"
                });
            }

            definition.IsPublic = false;
            definition.TenantId = "acme";
            store.SaveDefinition(definition);

            using var ctx = new WorkflowContext(options);
            Assert.AreEqual("acme", ctx.WorkflowStartTriggers.IgnoreQueryFilters().Single().TenantId,
                "one trigger, and it belongs to acme - the public one must not linger beside it.");

            WorkflowStartTriggerActivationRow kept =
                ctx.WorkflowStartTriggerActivations.IgnoreQueryFilters().Single();
            Assert.AreEqual("acme", kept.OwnerTenantId, "acme keeps running it, now as the owner.");
            Assert.AreEqual("acme", kept.TenantId,
                "and beta's takeover is gone - it pointed at a workflow beta may no longer see.");
        }

        // --- Aufbau ---------------------------------------------------------------------------------

        private EfWorkflowStore NewStore() => new EfWorkflowStore(() => new WorkflowContext(options));

        private static WorkflowEngine EngineOver(EfWorkflowStore store)
            => new WorkflowEngine(store, new ActivityRegistry());

        /// <summary>
        /// Macht den (einzigen) Zeitplan faellig und liefert den passenden "Jetzt". Die Faelligkeit steht
        /// jetzt an der AKTIVIERUNG - der Ausloeser traegt keinen Lauf-Zustand mehr.
        /// </summary>
        private DateTime MakeDue()
        {
            DateTime nowUtc = DateTime.UtcNow;
            using var ctx = new WorkflowContext(options);
            foreach (WorkflowStartTriggerActivationRow row in
                     ctx.WorkflowStartTriggerActivations.ToList())
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
            WorkflowStartTriggerActivationRow row = ctx.WorkflowStartTriggerActivations.Single();
            row.NextDueUtc = nextDue;
            row.LastRunUtc = lastRun;
            row.LastInstanceId = lastInstanceId;
            ctx.SaveChanges();
        }

        /// <summary>Der Lauf-Zustand des einzigen Zeitplans - er haengt an seiner Aktivierung.</summary>
        private WorkflowStartTriggerActivation SingleScheduleTrigger(EfWorkflowStore store)
            => store.ClaimDueScheduleTriggers(DateTime.UtcNow.AddYears(100), "probe", TimeSpan.Zero, 10)
                .Single(m => m.Trigger.Kind == WorkflowStartTriggerKind.Schedule)
                .Activation;

        /// <summary>Die Nachrichten-Ausloeser, die auf diesen Namen anspringen (ohne Ursprungs-Mandant).</summary>
        private static IReadOnlyList<WorkflowStartTriggerMatch> MessageMatches(EfWorkflowStore store,
            string signalName)
            => store.FindMessageTriggers(signalName, null).Matches;

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
                TechnicalName = id,
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
                TechnicalName = id,
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
