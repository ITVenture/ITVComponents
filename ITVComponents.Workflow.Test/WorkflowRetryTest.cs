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
    /// Prueft den Wiederaufsatz einer fehlgeschlagenen Instanz (<see cref="WorkflowEngine.RetryFaulted"/>):
    /// Daten korrigieren und den Schritt, an dem es scheiterte, erneut ausfuehren. Es wird bewusst nichts
    /// zurueckgespult und nichts uebersprungen - der Token steht beim Fault noch aktiv auf seinem Knoten.
    /// </summary>
    [TestClass]
    public class WorkflowRetryTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>Start -> Aktivitaet (ohne Fehler-Ausgang) -> Ende.</summary>
        private void SaveLinear(string activityRef = "step")
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = activityRef },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "a"), F("a", "e") }
            });
        }

        /// <summary>
        /// Eine Aktivitaet, die scheitert, solange 'amount' fehlt oder <= 0 ist - der typische Fall, den
        /// der Retry-Knopf loesen soll: der Fehler steckt in den DATEN, nicht im Prozess.
        /// </summary>
        private static ActivityRegistry NeedsAmount(Action onRun = null)
            => new ActivityRegistry().Register("step", ctx =>
            {
                onRun?.Invoke();
                if (!ctx.Variables.TryGetValue("amount", out object v) || Convert.ToDecimal(v) <= 0m)
                {
                    throw new InvalidOperationException("amount is missing or not positive");
                }

                ctx.Outputs["ok"] = true;
            });

        [TestMethod]
        public void Retry_AfterCorrectingTheData_CompletesTheWorkflow()
        {
            SaveLinear();
            var engine = new WorkflowEngine(store, NeedsAmount());

            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.AreEqual(WorkflowStatus.Faulted, store.GetInstance(inst.Id).Status,
                "without a valid amount the activity faults the instance.");

            bool resumed = engine.RetryFaulted(inst.Id,
                new Dictionary<string, object> { ["amount"] = 100m });
            Assert.IsTrue(resumed);

            // Der Wiederaufsatz setzt nur zurueck auf Running - vorangetrieben wird wie ueberall sonst.
            engine.Advance(store.GetInstance(inst.Id));

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.IsNull(final.FaultMessage, "the fault message is cleared on resume.");
            Assert.AreEqual(100m, final.Variables["amount"], "the correction is part of the instance state.");
        }

        [TestMethod]
        public void Retry_ResumesAtTheFailedNode_AndDoesNotRerunEarlierSteps()
        {
            // Zwei Aktivitaeten hintereinander; die erste zaehlt ihre Laeufe, die zweite scheitert.
            int firstRuns = 0;
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "first", ActivityRef = "first" },
                    new AutomatedActivityNode { Id = "second", ActivityRef = "step" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "first"), F("first", "second"), F("second", "e") }
            });
            ActivityRegistry activities = NeedsAmount();
            activities.Register("first", _ => firstRuns++);
            var engine = new WorkflowEngine(store, activities);

            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.AreEqual(1, firstRuns);

            WorkflowInstance faulted = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, faulted.Status);
            Assert.AreEqual("second", faulted.ActiveTokens.Single().NodeId,
                "the token stays on the node that failed - that IS the resume point.");

            engine.RetryFaulted(inst.Id, new Dictionary<string, object> { ["amount"] = 5m });
            engine.Advance(store.GetInstance(inst.Id));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            Assert.AreEqual(1, firstRuns, "the already completed step must NOT run again.");
        }

        [TestMethod]
        public void Retry_WithoutCorrection_FaultsAgain_AndStaysRetryable()
        {
            SaveLinear();
            var engine = new WorkflowEngine(store, NeedsAmount());

            WorkflowInstance inst = engine.StartWorkflow("wf");
            engine.RetryFaulted(inst.Id);
            engine.Advance(store.GetInstance(inst.Id));

            WorkflowInstance again = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, again.Status, "nothing was corrected - it fails again.");
            Assert.IsNotNull(again.FaultMessage);
            Assert.AreEqual(1, again.ActiveTokens.Count(), "and it can be retried once more.");
        }

        [TestMethod]
        public void Retry_LogsTheResumeWithTheCorrectedNames()
        {
            SaveLinear();
            var engine = new WorkflowEngine(store, NeedsAmount());

            WorkflowInstance inst = engine.StartWorkflow("wf");
            engine.RetryFaulted(inst.Id, new Dictionary<string, object> { ["amount"] = 1m }, "corrected by tester");

            HistoryEntry entry = store.GetInstance(inst.Id).History.Last(h => h.Event == "Retry");
            Assert.AreEqual("a", entry.NodeId);
            Assert.AreEqual(HistorySeverity.Warning, entry.Severity, "an intervention is not a routine event.");
            StringAssert.Contains(entry.Detail, "amount", "the audit trail names what was changed.");
            StringAssert.Contains(entry.Detail, "corrected by tester");
        }

        [TestMethod]
        public void Retry_WritesIntoTheScopeOfTheFailedBranch()
        {
            // AND-Split: jeder Strang bekommt einen eigenen Zweig-Scope. Eine Korrektur muss dort landen,
            // wo die fehlgeschlagene Aktivitaet liest - sonst sieht sie den korrigierten Wert nie.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "left", ActivityRef = "step" },
                    new AutomatedActivityNode { Id = "right", ActivityRef = "noop" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "left"), F("split", "right"), F("left", "e"), F("right", "e")
                }
            });
            ActivityRegistry activities = NeedsAmount();
            activities.Register("noop", _ => { });
            var engine = new WorkflowEngine(store, activities);

            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.AreEqual(WorkflowStatus.Faulted, store.GetInstance(inst.Id).Status);

            engine.RetryFaulted(inst.Id, new Dictionary<string, object> { ["amount"] = 42m });

            WorkflowInstance resumed = store.GetInstance(inst.Id);
            Token failed = resumed.Tokens.Single(t => t.NodeId == "left");
            Assert.IsNotNull(failed.Variables, "the branch has its own scope after the split.");
            Assert.AreEqual(42m, failed.Variables["amount"],
                "the correction goes into the scope the failing activity actually reads.");
        }

        // --- Parallele Zweige --------------------------------------------------------------------

        /// <summary>
        /// Split -> Zweig A (a1..a3) und Zweig B (b1..b5, scheitert an b2) -> AND-Join -> Ende.
        /// Die Reihenfolge der Split-Kanten bestimmt, welcher Zweig sequenziell zuerst laeuft.
        /// </summary>
        private void SaveTwoBranches(bool branchBFirst)
        {
            var flows = new List<SequenceFlow> { F("s", "split") };
            flows.Add(branchBFirst ? F("split", "b1") : F("split", "a1"));
            flows.Add(branchBFirst ? F("split", "a1") : F("split", "b1"));
            flows.AddRange(new[]
            {
                F("a1", "a2"), F("a2", "a3"), F("a3", "join"),
                F("b1", "b2"), F("b2", "b3"), F("b3", "b4"), F("b4", "b5"), F("b5", "join"),
                F("join", "e")
            });

            var nodes = new List<WorkflowNode>
            {
                new StartNode { Id = "s" },
                new ParallelGatewayNode { Id = "split" },
                new ParallelGatewayNode { Id = "join" },
                new EndNode { Id = "e" }
            };
            nodes.AddRange(new[] { "a1", "a2", "a3" }
                .Select(id => new AutomatedActivityNode { Id = id, ActivityRef = id }));
            nodes.Add(new AutomatedActivityNode { Id = "b1", ActivityRef = "b1" });
            nodes.Add(new AutomatedActivityNode { Id = "b2", ActivityRef = "step" });   // der Wackelkandidat
            nodes.AddRange(new[] { "b3", "b4", "b5" }
                .Select(id => new AutomatedActivityNode { Id = id, ActivityRef = id }));

            store.SaveDefinition(new WorkflowDefinition { Id = "wf", Nodes = nodes, Flows = flows });
        }

        private static ActivityRegistry CountingActivities(IDictionary<string, int> runs)
        {
            ActivityRegistry activities = NeedsAmount(() => Count(runs, "b2"));
            foreach (string id in new[] { "a1", "a2", "a3", "b1", "b3", "b4", "b5" })
            {
                string captured = id;
                activities.Register(captured, _ => Count(runs, captured));
            }

            return activities;
        }

        private static void Count(IDictionary<string, int> runs, string id)
            => runs[id] = runs.TryGetValue(id, out int n) ? n + 1 : 1;

        [TestMethod]
        public void ParallelFault_StopsTheOtherBranchWhereItStands_AndRetryResumesBoth()
        {
            // Zweig B laeuft zuerst und faultet an b2 - Zweig A ist zu diesem Zeitpunkt noch gar nicht
            // gelaufen. Genau die Frage: bleibt A stehen, und nimmt der Retry ihn wieder mit?
            SaveTwoBranches(branchBFirst: true);
            var runs = new Dictionary<string, int>(StringComparer.Ordinal);
            var engine = new WorkflowEngine(store, CountingActivities(runs));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance faulted = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, faulted.Status);
            Assert.AreEqual(1, runs["b1"]);
            Assert.AreEqual(1, runs["b2"], "b2 lief einmal und scheiterte.");
            Assert.IsFalse(runs.ContainsKey("a1"),
                "Zweig A ist gar nicht gelaufen - der Vortrieb bricht ab, sobald EIN Zweig faultet.");

            // Beide Tokens sind aktiv: B steht auf b2 (Fehlerstelle), A noch am Anfang seines Zweigs.
            Assert.AreEqual(2, faulted.ActiveTokens.Count());
            Assert.AreEqual("b2", WorkflowEngine.FindRetryPoint(faulted).NodeId,
                "der Wiederaufsatzpunkt ist die Fehlerstelle, nicht irgendein aktives Token.");
            CollectionAssert.AreEquivalent(new[] { "a1", "b2" },
                faulted.ActiveTokens.Select(t => t.NodeId).ToList());

            engine.RetryFaulted(inst.Id, new Dictionary<string, object> { ["amount"] = 7m });
            engine.Advance(store.GetInstance(inst.Id));

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status,
                "beide Zweige laufen weiter und der Join feuert.");
            Assert.AreEqual(2, runs["b2"], "b2 lief genau zweimal: einmal fehlgeschlagen, einmal nach der Korrektur.");
            foreach (string id in new[] { "a1", "a2", "a3", "b1", "b3", "b4", "b5" })
            {
                Assert.AreEqual(1, runs[id], $"'{id}' darf genau einmal gelaufen sein.");
            }
        }

        [TestMethod]
        public void ParallelFault_AfterTheOtherBranchParkedAtTheJoin_RetryStillCompletes()
        {
            // Andere Kanten-Reihenfolge: A laeuft komplett durch und parkt am Join, DANN faultet B. A ist
            // dann kein aktives Token mehr, sondern wartet (Joining) - der Retry darf ihn nicht anfassen.
            SaveTwoBranches(branchBFirst: false);
            var runs = new Dictionary<string, int>(StringComparer.Ordinal);
            var engine = new WorkflowEngine(store, CountingActivities(runs));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance faulted = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, faulted.Status);
            Assert.AreEqual(1, runs["a3"], "Zweig A lief bis zum Join durch, bevor B ueberhaupt startete.");
            Assert.AreEqual("b2", WorkflowEngine.FindRetryPoint(faulted).NodeId);
            Assert.AreEqual(1, faulted.ActiveTokens.Count(), "nur der fehlgeschlagene Zweig ist noch aktiv.");
            Assert.IsTrue(faulted.Tokens.Any(t => t.Status == TokenStatus.Joining),
                "Zweig A wartet am Join.");

            engine.RetryFaulted(inst.Id, new Dictionary<string, object> { ["amount"] = 7m });
            engine.Advance(store.GetInstance(inst.Id));

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(1, runs["a1"], "der wartende Zweig laeuft NICHT noch einmal.");
            Assert.AreEqual(2, runs["b2"]);
        }

        // --- Abbruch einer fehlgeschlagenen Instanz ----------------------------------------------

        [TestMethod]
        public void FaultedInstance_CanBeCancelled()
        {
            // Wer den Wiederaufsatz aufgibt, muss den Fall schliessen koennen - sonst bliebe er fuer
            // immer in der Uebersicht liegen.
            SaveLinear();
            var engine = new WorkflowEngine(store, NeedsAmount());
            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.AreEqual(WorkflowStatus.Faulted, store.GetInstance(inst.Id).Status);

            Assert.IsTrue(engine.CancelWorkflow(inst.Id));

            WorkflowInstance cancelled = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Cancelled, cancelled.Status);
            Assert.IsTrue(cancelled.Tokens.All(t => t.Status == TokenStatus.Consumed));
            StringAssert.Contains(cancelled.History.Last(h => h.Event == "Cancelled").Detail, "given up after",
                "der Grund des Fehlschlags bleibt im Protokoll sichtbar.");
        }

        [TestMethod]
        public void CancelledInstance_CannotBeRetried()
        {
            SaveLinear();
            var engine = new WorkflowEngine(store, NeedsAmount());
            WorkflowInstance inst = engine.StartWorkflow("wf");
            engine.CancelWorkflow(inst.Id);

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                () => engine.RetryFaulted(inst.Id));
            StringAssert.Contains(ex.Message, "not faulted");
        }

        [TestMethod]
        public void Retry_OfARunningInstance_IsRejected()
        {
            SaveLinear();
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", _ => { }));
            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                () => engine.RetryFaulted(inst.Id));
            StringAssert.Contains(ex.Message, "not faulted");
        }

        [TestMethod]
        public void Retry_OfAnUnknownInstance_IsFalse()
        {
            SaveLinear();
            var engine = new WorkflowEngine(store, NeedsAmount());
            Assert.IsFalse(engine.RetryFaulted("does-not-exist"));
        }
    }
}
