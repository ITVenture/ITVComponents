using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Der Lebenszyklus der Start-Ausloeser und ihrer Uebernahmen - gefahren gegen <b>beide</b>
    /// Speicher-Fassungen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Diese Regeln stehen in beiden Stores ausformuliert (rund 180 Zeilen in-memory, rund 300 in EF,
    /// bis in die Log-Texte hinein). Sie zusammenzulegen scheitert nicht an der Logik, sondern am
    /// Speicher-Modell: die eine Fassung arbeitet auf Domaenenobjekten in Dictionaries, die andere auf
    /// EF-Zeilen mit <c>IgnoreQueryFilters</c> und <c>SaveChanges</c>. Sie traegt dabei
    /// SQL-Uebersetzbarkeit im Gepaeck - <c>EfWorkflowStore</c> vermerkt ausdruecklich, dass
    /// <c>DefaultIfEmpty(wert)</c> sich nicht uebersetzen laesst, und genau das benutzt die
    /// In-Memory-Fassung. Eine gemeinsame Implementierung muesste also in der EF-Form geschrieben werden
    /// und dem Speicher-ohne-Datenbank die Zwaenge einer Datenbank aufdruecken.
    /// </para>
    /// <para>
    /// <b>Deshalb dieser Test statt einer Zusammenlegung.</b> Was doppelt gepflegt bleibt, muss
    /// wenigstens nachweislich dasselbe tun. Der Bericht hatte die Divergenz vermerkt
    /// („ein gruener In-Memory-Test beweist fuer diesen Weg nichts mehr") - die rund dreissig
    /// vorhandenen Tests in <see cref="WorkflowStartTriggerTest"/> laufen ausschliesslich gegen EF.
    /// </para>
    /// <para>
    /// <b>Eine Abweichung ist gewollt und wird hier NICHT geprueft:</b> der EF-Store sammelt zusaetzlich
    /// ueber <c>DefinitionKey</c>, um Ausloeser-Zeilen aus der Zeit vor einer Korrektur einzufangen. Ein
    /// Speicher ohne Persistenz kann keinen Altbestand haben.
    /// </para></remarks>
    [TestClass]
    public class WorkflowTriggerLifecycleContractTest
    {
        private const string Memory = "memory";
        private const string Ef = "ef";
        private const string DailyAtEight = "d20200101080001";
        private const string DailyAtNine = "d20200101090001";

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

        private IWorkflowStore NewStore(string kind)
            => kind == Ef
                ? new EfWorkflowStore(() => new WorkflowContext(options))
                : new InMemoryWorkflowStore();

        // --- Entstehen ---------------------------------------------------------------------------------

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void ATenantOwnedSchedule_GetsItsOwnActivationRightAway(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveDefinition(WithSchedule("s1", DailyAtEight, tenantId: "t1"));

            WorkflowStartTriggerActivation own = store.GetActivations("t1").Single();

            Assert.AreEqual("t1", own.TenantId, $"[{kind}] the owner runs its own schedule.");
            Assert.IsTrue(own.Enabled, $"[{kind}] and it runs from the start.");
            Assert.IsNotNull(own.NextDueUtc, $"[{kind}] a schedule without a next date would never fire.");
            Assert.AreEqual(WorkflowStartTriggerKind.Schedule, own.Kind);
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void APublicSchedule_GetsNoActivation(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowDefinition definition = WithSchedule("s1", DailyAtEight, tenantId: null);
            definition.IsPublic = true;
            // Ohne diese Erlaubnis ist die Definition zwar oeffentlich, aber NICHT uebernehmbar - eine
            // oeffentliche Definition allein macht ihren Zeitplan noch nicht zum Angebot.
            ((StartNode)definition.Nodes[0]).ScheduleStart.AllowLocalActivation = true;
            store.SaveDefinition(definition);

            Assert.AreEqual(0, store.GetActivations("t1").Count,
                $"[{kind}] with a public definition the tenant decides for itself whether to run it - "
                + "creating the activation would run it behind their back.");
            Assert.AreEqual(1, store.FindActivatableTriggers("t1").Count,
                $"[{kind}] but it must be offered for adoption.");
        }

        // --- Ueberdauern -------------------------------------------------------------------------------

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void AnUnchangedSchedule_KeepsItsRunState(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveDefinition(WithSchedule("s1", DailyAtEight, tenantId: "t1"));
            var due = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
            Stamp(store, due, due.AddDays(-1), "old-instance");

            WorkflowDefinition again = WithSchedule("s1", DailyAtEight, tenantId: "t1");
            again.Version = 2;
            store.SaveDefinition(again);

            WorkflowStartTriggerActivation after = store.GetActivations("t1").Single();
            Assert.AreEqual(due, after.NextDueUtc,
                $"[{kind}] otherwise EVERY save of the definition - even a change somewhere else "
                + "entirely - would start the schedule over.");
            Assert.AreEqual("old-instance", after.LastInstanceId, $"[{kind}] the last run belongs to it.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void ANewVersion_KeepsTheActivation(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveDefinition(WithSchedule("s1", DailyAtEight, tenantId: "t1"));
            int keyBefore = store.GetActivations("t1").Single().ActivationKey;

            WorkflowDefinition v2 = WithSchedule("s1", DailyAtEight, tenantId: "t1");
            v2.Version = 2;
            store.SaveDefinition(v2);

            WorkflowStartTriggerActivation after = store.GetActivations("t1").Single();
            Assert.AreEqual(keyBefore, after.ActivationKey,
                $"[{kind}] the activation hangs on the trigger's SUBJECT (definition, node, kind), not on "
                + "its row - the trigger key is handed out anew on every rebuild. Whoever ties the "
                + "adoption to the key loses it on the next save.");
        }

        // --- Neu anfangen ------------------------------------------------------------------------------

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void ARewrittenSchedule_StartsOver(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveDefinition(WithSchedule("s1", DailyAtEight, tenantId: "t1"));
            var due = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
            Stamp(store, due, due.AddDays(-1), "old-instance");

            WorkflowDefinition changed = WithSchedule("s1", DailyAtNine, tenantId: "t1");
            changed.Version = 2;
            store.SaveDefinition(changed);

            WorkflowStartTriggerActivation after = store.GetActivations("t1").Single();
            Assert.IsNull(after.LastRunUtc,
                $"[{kind}] a rewritten schedule is a DIFFERENT plan and its first run belongs to it - "
                + "otherwise an \"immediately\" marker never takes hold, because \"already ran\" comes "
                + "from the time before.");
            Assert.IsNull(after.LastInstanceId, $"[{kind}] the old instance is not this plan's.");
            Assert.AreNotEqual(due, after.NextDueUtc, $"[{kind}] and the next date is recomputed.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void ARewrittenSchedule_LeavesAnOwnPatternAlone(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowDefinition definition = WithSchedule("s1", DailyAtEight, tenantId: "t1");
            ((StartNode)definition.Nodes[0]).ScheduleStart.AllowReschedule = true;
            store.SaveDefinition(definition);

            WorkflowStartTriggerActivation own = store.GetActivations("t1").Single();
            var mine = new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc);
            own.PatternOverride = "d20200101060001";
            store.SaveActivation(own);
            // Der Lauf-Zustand geht ueber UpdateScheduleActivation und NICHT ueber SaveActivation: das
            // schreibt bewusst nur Zustimmung, Muster-Uebersteuerung und Variablen und laesst
            // LastRunUtc/LastInstanceId einer bestehenden Zeile in Ruhe.
            //
            // Beim ersten Anlauf stand das hier als schlichte Zuweisung am Objekt - und der Test war
            // in-memory GRUEN und in EF rot. Der Speicher ohne Datenbank liefert dieselbe Referenz
            // zurueck, die der Aufrufer bearbeitet hat; die Zuweisung "wirkt" dort ohne jedes Speichern.
            // Genau dafuer laeuft dieser Test gegen beide Fassungen.
            // Der letzte Lauf MUSS dabei mitkommen: beide Fassungen schreiben die Instanz nur zusammen
            // mit dem Zeitpunkt ihres Laufs - "welche Instanz" ohne "wann" waere eine halbe Auskunft.
            store.UpdateScheduleActivation(own.ActivationKey, mine, mine.AddDays(-1), "mine");

            WorkflowDefinition changed = WithSchedule("s1", DailyAtNine, tenantId: "t1");
            ((StartNode)changed.Nodes[0]).ScheduleStart.AllowReschedule = true;
            changed.Version = 2;
            store.SaveDefinition(changed);

            WorkflowStartTriggerActivation after = store.GetActivations("t1").Single();
            Assert.AreEqual(mine, after.NextDueUtc,
                $"[{kind}] whoever runs their OWN pattern is not affected by a rewrite of the central "
                + "one - resetting them would silently pull them back onto someone else's plan.");
            Assert.AreEqual("mine", after.LastInstanceId, $"[{kind}] and their run state stays theirs.");
        }

        // --- Verschwinden ------------------------------------------------------------------------------

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void ARemovedStartNode_DropsTheTriggerButKeepsTheAdoption(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            store.SaveDefinition(WithSchedule("s1", DailyAtEight, tenantId: "t1"));
            Assert.AreEqual(1, store.GetActivations("t1").Count, $"[{kind}] precondition.");

            WorkflowDefinition without = WithSchedule("s1", DailyAtEight, tenantId: "t1");
            ((StartNode)without.Nodes[0]).ScheduleStart = null;
            without.Version = 2;
            store.SaveDefinition(without);

            Assert.AreEqual(0, store.FindActivatableTriggers("t1").Count,
                $"[{kind}] the trigger is gone with its node.");
            Assert.AreEqual(1, store.GetActivations("t1").Count,
                $"[{kind}] the adoption stays - it is the tenant's decision, and it must still be there "
                + "if the node comes back. Deleting it would silently discard their consent.");
        }

        // --- Hilfen ------------------------------------------------------------------------------------

        /// <summary>Schreibt einen Lauf-Zustand auf die (einzige) Aktivierung des Mandanten.</summary>
        private static void Stamp(IWorkflowStore store, DateTime nextDueUtc, DateTime lastRunUtc,
            string lastInstanceId)
        {
            WorkflowStartTriggerActivation activation = store.GetActivations("t1").Single();
            store.UpdateScheduleActivation(activation.ActivationKey, nextDueUtc, lastRunUtc, lastInstanceId);
        }

        private static WorkflowDefinition WithSchedule(string id, string pattern, string tenantId)
            => new WorkflowDefinition
            {
                Id = id,
                Version = 1,
                TenantId = tenantId,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode
                    {
                        Id = "s",
                        ScheduleStart = new ScheduleStartTrigger { Pattern = pattern }
                    },
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
