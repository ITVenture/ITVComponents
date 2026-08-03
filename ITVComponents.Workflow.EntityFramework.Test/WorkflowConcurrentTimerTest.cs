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
    /// Prueft, was passiert, wenn ZWEI Runner denselben faelligen Timer aufgreifen - auf beiden Ebenen:
    /// dem Anspruch (<c>ClaimDueTimers</c>, spart die doppelte Arbeit) und dem Commit
    /// (<c>ReactivateTimers</c>, verhindert das doppelte Feuern).
    /// </summary>
    /// <remarks>
    /// Faellige Timer stehen im gemeinsamen Store, nicht in einem host-eigenen Puffer: in einer
    /// Mehr-Instanzen-Umgebung sieht sie grundsaetzlich jeder Runner. Der Anspruch macht daraus
    /// disjunkte Scheiben - er ist aber nur eine <b>Optimierung</b> und darf ablaufen, waehrend sein
    /// Halter noch arbeitet. Die Zusicherung "feuert genau einmal" traegt deshalb weiterhin allein die
    /// optimistische Nebenlaeufigkeit von <c>ReactivateAndCommit</c>. Beides ist hier gepruefft, denn
    /// ein Regress zeigte sich sonst als doppelte Eskalation - nur unter Last, nur verteilt, also
    /// genau dort, wo niemand hinschaut.
    /// <para>
    /// Bewusst ueber den <b>EF-Store</b>: der In-Memory-Store kennt keine Ansprueche und gibt dieselbe
    /// Objekt-Referenz zurueck - beide "Runner" arbeiteten am selben Objekt, und die zu pruefende Lage
    /// (zwei Kopien, eine davon veraltet) entstuende gar nicht erst.
    /// </para>
    /// </remarks>
    [TestClass]
    public class WorkflowConcurrentTimerTest
    {
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

        private EfWorkflowStore NewStore() => new EfWorkflowStore(() => new WorkflowContext(options));

        private static WorkflowEngine EngineOver(IWorkflowStore store)
            => new WorkflowEngine(store, new ActivityRegistry().Register("after", _ => { }));

        [TestMethod]
        public void ReactivateTimers_TwoRunnersRaceForTheSameTimer_ItElapsesExactlyOnce()
        {
            EfWorkflowStore inner = NewStore();
            inner.SaveDefinition(TimerDefinition());
            WorkflowInstance instance = WaitingAtDueTimer(inner);

            // Der Rivale reaktiviert und committet, WAEHREND der Verlierer seine Kopie schon in der Hand
            // hat - dessen Commit muss danach am Versions-Check scheitern.
            var store = new RivalReactivationStore(inner, instance.Id,
                (s, id) => EngineOver(s).ReactivateTimers(id, DateTime.UtcNow));

            IReadOnlyList<string> mine = EngineOver(store).ReactivateTimers(instance.Id, DateTime.UtcNow);

            Assert.AreEqual(1, store.RivalResult.Count, "the rival won the race and reactivated the token.");
            Assert.AreEqual(0, mine.Count,
                "the loser must reactivate nothing: on the retry the timer is no longer due.");

            WorkflowInstance final = inner.GetInstance(instance.Id);
            Assert.AreEqual(1, final.History.Count(h => h.Event == "TimerElapsed"),
                "the timer must have fired exactly once.");
            Assert.AreEqual(1, final.Tokens.Count, "no second token was spawned.");
            Assert.AreEqual("af", final.Tokens.Single().NodeId,
                "the one token moved past the timer exactly once.");
        }

        [TestMethod]
        public void ReactivateTimers_TwoRunnersRaceForTheSameBoundaryTimer_TheSidePathStartsOnce()
        {
            // Der teurere Fall: hier ist doppeltes Feuern nicht nur ein Buchungsfehler, sondern eine
            // zweite Eskalation - dieselbe Erinnerung ginge zweimal raus.
            EfWorkflowStore inner = NewStore();
            inner.SaveDefinition(UserTaskWithDeadlineDefinition());
            WorkflowInstance instance = ParkedWithDueDeadline(inner);

            var store = new RivalReactivationStore(inner, instance.Id,
                (s, id) => EngineOver(s).ReactivateTimers(id, DateTime.UtcNow));

            IReadOnlyList<string> mine = EngineOver(store).ReactivateTimers(instance.Id, DateTime.UtcNow);

            Assert.AreEqual(1, store.RivalResult.Count, "the rival started the side path.");
            Assert.AreEqual(0, mine.Count, "the loser must not start a second escalation.");

            WorkflowInstance final = inner.GetInstance(instance.Id);
            Assert.AreEqual(1, final.History.Count(h => h.Event == "BoundaryTimerElapsed"),
                "the deadline must have elapsed exactly once.");
            Assert.AreEqual(1, final.Tokens.Count(t => t.NodeId == "se"),
                "exactly one token walks the side path.");
            Assert.AreEqual(TokenStatus.Waiting, final.Tokens.Single(t => t.NodeId == "u").Status,
                "the user task is untouched - a non-interrupting deadline does not move the main flow.");
        }

        [TestMethod]
        public void ClaimDueTimers_WhileTheLeaseHolds_ASecondRunnerSeesNothing()
        {
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(TimerDefinition());
            WaitingAtDueTimer(store);
            DateTime now = DateTime.UtcNow;

            Assert.AreEqual(1, store.ClaimDueTimers(now, "runner-a", Lease, 10).Count(),
                "the first runner gets the due timer.");
            Assert.AreEqual(0, store.ClaimDueTimers(now, "runner-b", Lease, 10).Count(),
                "the second must not even load it - that is the whole point of the claim.");
        }

        [TestMethod]
        public void ClaimDueTimers_AnExpiredLease_IsTakenOverByAnotherRunner()
        {
            // Die Selbstheilung: haelt ein abgestuerzter Runner den Anspruch, darf ihn nach Ablauf ein
            // anderer uebernehmen. Ohne das bliebe der Timer bis zum Neustart des Toten liegen.
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(TimerDefinition());
            WaitingAtDueTimer(store);
            DateTime now = DateTime.UtcNow;

            Assert.AreEqual(1, store.ClaimDueTimers(now, "runner-a", TimeSpan.FromSeconds(30), 10).Count());
            Assert.AreEqual(0, store.ClaimDueTimers(now, "runner-b", TimeSpan.FromSeconds(30), 10).Count());
            Assert.AreEqual(1, store.ClaimDueTimers(now.AddMinutes(1), "runner-b", Lease, 10).Count(),
                "once the lease has run out the timer is up for grabs again.");
        }

        [TestMethod]
        public void ClaimDueTimers_HonoursTheBatchLimit_AndLeavesTheRestForOthers()
        {
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(TimerDefinition());
            WaitingAtDueTimer(store);
            WaitingAtDueTimer(store);
            WaitingAtDueTimer(store);
            DateTime now = DateTime.UtcNow;

            Assert.AreEqual(2, store.ClaimDueTimers(now, "runner-a", Lease, 2).Count(),
                "the batch limit caps what one runner takes at once.");
            Assert.AreEqual(1, store.ClaimDueTimers(now, "runner-b", Lease, 10).Count(),
                "the remainder stays available - a backlog is shared, not hoarded.");
        }

        [TestMethod]
        public void ClaimDueTimers_ACommitEndsTheClaim_SoARearmedTimerIsVisibleAgain()
        {
            // Der Stempel gehoert dem Aufgriff, nicht dem Token: schreibt die Engine die Zeile, ist der
            // Aufgriff vorbei. Bliebe er stehen, waere ein neu gestellter Fristen-Timer bis zum Ablauf
            // des ALTEN Anspruchs fuer jeden Runner unsichtbar - die Eskalation kaeme zu spaet.
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(TimerDefinition());
            WorkflowInstance instance = WaitingAtDueTimer(store);
            DateTime now = DateTime.UtcNow;

            Assert.AreEqual(1, store.ClaimDueTimers(now, "runner-a", TimeSpan.FromHours(1), 10).Count());
            Assert.AreEqual(0, store.ClaimDueTimers(now, "runner-b", Lease, 10).Count());

            WorkflowInstance loaded = store.GetInstance(instance.Id);
            Assert.IsTrue(store.TryCommitInstance(loaded, loaded.Version));

            Assert.AreEqual(1, store.ClaimDueTimers(now, "runner-b", Lease, 10).Count(),
                "a commit ends the claim, even though the lease would still have an hour to run.");
        }

        [TestMethod]
        public void ReleaseLocksOfOwner_AlsoFreesTheOwnTimerClaims()
        {
            // Ein neu gestarteter Runner soll die liegengebliebene Arbeit sofort aufholen koennen und
            // nicht erst vor seinen eigenen, verwaisten Anspruechen warten.
            EfWorkflowStore store = NewStore();
            store.SaveDefinition(TimerDefinition());
            WaitingAtDueTimer(store);
            DateTime now = DateTime.UtcNow;

            Assert.AreEqual(1, store.ClaimDueTimers(now, "runner-a", TimeSpan.FromHours(1), 10).Count());
            store.ReleaseLocksOfOwner("runner-a");

            Assert.AreEqual(1, store.ClaimDueTimers(now, "runner-b", Lease, 10).Count(),
                "releasing an owner's locks releases its timer claims too.");
        }

        /// <summary>Eine Anspruchsdauer, die im Test nie ablaeuft (die Zeit wird als Parameter gestellt).</summary>
        private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

        /// <summary>Eine Instanz, deren Token an einem laengst faelligen Timer wartet.</summary>
        private static WorkflowInstance WaitingAtDueTimer(IWorkflowStore store)
        {
            var instance = new WorkflowInstance
            {
                DefinitionId = "tmr",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Running,
                Tokens = new List<Token>
                {
                    new Token
                    {
                        Id = "t",
                        NodeId = "tm",
                        Status = TokenStatus.Waiting,
                        DueUtc = DateTime.UtcNow.AddMinutes(-5)
                    }
                }
            };
            store.SaveInstance(instance);
            return instance;
        }

        /// <summary>
        /// Eine Instanz, die an ihrer Benutzer-Aufgabe parkt und deren Fristen-Timer abgelaufen ist. Das
        /// Timer-Token entsteht erst im Zweig-Vortrieb (samt Verknuepfung zum Haupt-Token), deshalb hier
        /// erst laufen lassen und dann die Frist zurueckdatieren - nicht von Hand zusammenbauen.
        /// </summary>
        private WorkflowInstance ParkedWithDueDeadline(EfWorkflowStore store)
        {
            WorkflowEngine engine = EngineOver(store);
            WorkflowInstance created = engine.CreateInstance("ut",
                new Dictionary<string, object> { { "owner", "anna" } });
            engine.RunBranch(created.Id, created.Tokens.Single().Id);

            WorkflowInstance parked = store.GetInstance(created.Id);
            parked.Tokens.Single(t => t.NodeId == "bt").DueUtc = DateTime.UtcNow.AddMinutes(-5);
            Assert.IsTrue(store.TryCommitInstance(parked, parked.Version),
                "backdating the deadline must commit - nobody else touched the instance yet.");
            return parked;
        }

        private static WorkflowDefinition TimerDefinition()
        {
            return new WorkflowDefinition
            {
                Id = "tmr",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new TimerNode { Id = "tm" },
                    new AutomatedActivityNode { Id = "af", ActivityRef = "after" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "tm"), Flow("tm", "af"), Flow("af", "e") }
            };
        }

        private static WorkflowDefinition UserTaskWithDeadlineDefinition()
        {
            return new WorkflowDefinition
            {
                Id = "ut",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new UserActivityNode { Id = "u", TaskKey = "ApproveInvoice", Assignment = "owner" },
                    new BoundaryTimerNode
                    {
                        Id = "bt",
                        AttachedToNodeId = "u",
                        Deadlines = new List<BoundaryDeadline> { new BoundaryDeadline { Expression = "4" } }
                    },
                    new SidePathEndNode { Id = "se" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "u"), Flow("u", "e"), Flow("bt", "se") }
            };
        }

        private static SequenceFlow Flow(string from, string to)
            => new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Ein Store-Decorator, der das Rennen zweier Runner nachstellt: beim ERSTEN
        /// <see cref="GetInstance"/> der beobachteten Instanz laesst er einen Rivalen die Reaktivierung
        /// vollstaendig durchfuehren und committen - der Aufrufer haelt danach eine bereits VERALTETE
        /// Kopie in der Hand. Genau die Lage, die entsteht, wenn zwei Prozesse denselben faelligen Timer
        /// im selben Moment aufgreifen; ohne diesen Kunstgriff waere sie nicht verlaesslich zu treffen.
        /// </summary>
        private sealed class RivalReactivationStore : IWorkflowStore
        {
            private readonly IWorkflowStore inner;
            private readonly string instanceId;
            private readonly Func<IWorkflowStore, string, IReadOnlyList<string>> rival;
            private bool rivalRan;

            public RivalReactivationStore(IWorkflowStore inner, string instanceId,
                Func<IWorkflowStore, string, IReadOnlyList<string>> rival)
            {
                this.inner = inner;
                this.instanceId = instanceId;
                this.rival = rival;
            }

            /// <summary>Was der Rivale reaktiviert hat.</summary>
            public IReadOnlyList<string> RivalResult { get; private set; } = Array.Empty<string>();

            public WorkflowInstance GetInstance(string id)
            {
                // Erst die Kopie des Aufrufers holen, DANN den Rivalen laufen lassen: nur so ist die
                // Kopie hinterher veraltet. Umgekehrt bekaeme er den schon fertigen Stand und es gaebe
                // nichts zu gewinnen.
                WorkflowInstance forCaller = inner.GetInstance(id);
                if (!rivalRan && id == instanceId)
                {
                    rivalRan = true;
                    RivalResult = rival(inner, id);
                }

                return forCaller;
            }

            public int? GetInstancePriority(string id) => inner.GetInstancePriority(id);
            public void SaveDefinition(WorkflowDefinition definition) => inner.SaveDefinition(definition);
            public WorkflowDefinition GetDefinition(string id, int? version = null) => inner.GetDefinition(id, version);
            public void SaveInstance(WorkflowInstance instance) => inner.SaveInstance(instance);
            public bool TryCommitInstance(WorkflowInstance instance, int baseVersion) => inner.TryCommitInstance(instance, baseVersion);
            public IEnumerable<WorkflowInstance> FindWaitingForSignal(string s, string c = null) => inner.FindWaitingForSignal(s, c);
            public IEnumerable<WorkflowInstance> FindWaitingForBroadcast(string s) => inner.FindWaitingForBroadcast(s);
            public IEnumerable<WorkflowInstance> FindDueTimers(DateTime now) => inner.FindDueTimers(now);
            public IEnumerable<WorkflowInstance> ClaimDueTimers(DateTime now, string o, TimeSpan l, int m) => inner.ClaimDueTimers(now, o, l, m);
            public DateTime? PeekNextTimerDueUtc(DateTime now) => inner.PeekNextTimerDueUtc(now);
            public IEnumerable<WorkflowInstance> FindBranchesWaitingForTarget(IEnumerable<string> targets) => inner.FindBranchesWaitingForTarget(targets);
            public IEnumerable<WorkflowInstance> FindRunnable() => inner.FindRunnable();
            public IEnumerable<WorkflowInstance> FindChildInstances(string parentInstanceId) => inner.FindChildInstances(parentInstanceId);
            public IEnumerable<WorkflowInstance> FindFinishedChildrenWithWaitingParent() => inner.FindFinishedChildrenWithWaitingParent();
            public IWorkflowBranchLock TryAcquireBranchLock(string i, string t, string o) => inner.TryAcquireBranchLock(i, t, o);
            public void ReleaseLocksOfOwner(string owner) => inner.ReleaseLocksOfOwner(owner);
        }
    }
}
