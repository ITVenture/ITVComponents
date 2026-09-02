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
                TechnicalName = "wf",
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
                TechnicalName = "wf",
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
                TechnicalName = "wf",
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

            store.SaveDefinition(new WorkflowDefinition { TechnicalName = "wf", Nodes = nodes, Flows = flows });
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

        /// <summary>
        /// Split -> zwei einstufige Zweige, die BEIDE scheitern koennen (jeder an seiner eigenen
        /// Variable) -> AND-Join -> Ende. Nur ueber den nebenlaeufigen Weg erreichbar: sequenziell
        /// bricht der Vortrieb beim ersten Fault ab, der zweite Zweig laeuft dann gar nicht erst.
        /// </summary>
        private void SaveTwoFailingBranches()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a1", ActivityRef = "needsA" },
                    new AutomatedActivityNode { Id = "b1", ActivityRef = "needsB" },
                    new ParallelGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "a1"), F("split", "b1"),
                    F("a1", "join"), F("b1", "join"), F("join", "e")
                }
            });
        }

        /// <remarks>
        /// Gezaehlt wird die AUSFUEHRUNG, nicht das Protokoll: <see cref="InMemoryWorkflowStore"/> gibt bei
        /// <c>GetInstance</c> dieselbe Objektreferenz zurueck (der EF-Store deserialisiert dagegen je
        /// Aufruf). Beim direkten Treiben ueber <see cref="WorkflowEngine.RunBranch"/> sind das
        /// in-memory-Objekt und der "frische" Stand im Commit deshalb dasselbe, und die History-Eintraege
        /// des Zweig-Deltas landen ein zweites Mal darin. Token, Variablen und Status sind davon nicht
        /// betroffen (das Anwenden ist dort idempotent) - Zaehler sind hier also die verlaessliche Sonde.
        /// </remarks>
        private static ActivityRegistry NeedsOwnVariable(IDictionary<string, int> runs)
            => new ActivityRegistry()
                .Register("needsA", ctx => Require(ctx, "okA", runs))
                .Register("needsB", ctx => Require(ctx, "okB", runs));

        private static void Require(WorkflowActivityContext ctx, string name, IDictionary<string, int> runs)
        {
            Count(runs, name);
            if (!ctx.Variables.TryGetValue(name, out object v) || !Equals(v, true))
            {
                throw new InvalidOperationException($"{name} is not set");
            }
        }

        [TestMethod]
        public void TwoFaultedBranches_BothTokensStayActive_AndBothRestartOnRetry()
        {
            SaveTwoFailingBranches();
            var runs = new Dictionary<string, int>(StringComparer.Ordinal);
            var engine = new WorkflowEngine(store, NeedsOwnVariable(runs));

            // Nebenlaeufiger Weg von Hand nachgestellt: je Zweig ein RunBranch, wie es der Runner tut.
            WorkflowInstance inst = engine.CreateInstance("wf");
            string startToken = inst.ActiveTokens.Single().Id;
            IReadOnlyList<string> branches = engine.RunBranch(inst.Id, startToken);
            Assert.AreEqual(2, branches.Count, "der Split erzeugt zwei Zweige.");

            foreach (string branch in branches)
            {
                engine.RunBranch(inst.Id, branch);
            }

            WorkflowInstance faulted = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, faulted.Status);
            Assert.AreEqual(1, runs["okA"], "Zweig A lief einmal und scheiterte.");
            Assert.AreEqual(1, runs["okB"], "Zweig B ebenfalls - ein Fault blockiert RunBranch nicht.");
            CollectionAssert.AreEquivalent(new[] { "a1", "b1" },
                faulted.ActiveTokens.Select(t => t.NodeId).ToList(),
                "beide Tokens bleiben aktiv auf ihrem Knoten stehen.");

            // Der Wiederaufsatzpunkt ist EINER - der zuletzt gemeldete Fehler.
            Token point = WorkflowEngine.FindRetryPoint(faulted);
            Assert.IsNotNull(point);
            CollectionAssert.Contains(new[] { "a1", "b1" }, point.NodeId);

            // Ein Retry gibt die Instanz frei; angefasst wird kein einziges Token.
            engine.RetryFaulted(inst.Id);
            WorkflowInstance resumed = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Running, resumed.Status);
            Assert.AreEqual(2, resumed.ActiveTokens.Count(), "beide Zweige sind weiterhin aktiv...");

            // ...und werden beide erneut ausgefuehrt (der Runner reiht alle aktiven Tokens ein).
            foreach (Token t in resumed.ActiveTokens.ToList())
            {
                engine.RunBranch(inst.Id, t.Id);
            }

            WorkflowInstance again = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, again.Status, "ohne Korrektur scheitern wieder beide.");
            Assert.AreEqual(2, runs["okA"], "Zweig A wurde tatsaechlich erneut ausgefuehrt...");
            Assert.AreEqual(2, runs["okB"], "...und Zweig B ebenso. EIN Retry stoesst BEIDE wieder an.");
            Assert.AreEqual(2, again.ActiveTokens.Count(), "und beide stehen wieder auf ihrer Fehlerstelle.");
        }

        [TestMethod]
        public void TwoFaultedBranches_CorrectionReachesOnlyTheBranchOfTheRetryPoint()
        {
            // WICHTIG: die Korrektur geht in den Scope EINES Tokens (des Wiederaufsatzpunkts). Nach einem
            // Split hat jeder Zweig seinen eigenen Scope - der andere sieht sie also nicht. Zwei kaputte
            // Zweige brauchen deshalb zwei Durchgaenge.
            SaveTwoFailingBranches();
            var runs = new Dictionary<string, int>(StringComparer.Ordinal);
            var engine = new WorkflowEngine(store, NeedsOwnVariable(runs));

            WorkflowInstance inst = engine.CreateInstance("wf");
            foreach (string branch in engine.RunBranch(inst.Id, inst.ActiveTokens.Single().Id))
            {
                engine.RunBranch(inst.Id, branch);
            }

            // Beide Korrekturen mitgeben - ankommen kann nur die des Wiederaufsatz-Zweigs.
            engine.RetryFaulted(inst.Id, new Dictionary<string, object> { ["okA"] = true, ["okB"] = true });

            WorkflowInstance resumed = store.GetInstance(inst.Id);
            Token a = resumed.Tokens.Single(t => t.NodeId == "a1");
            Token b = resumed.Tokens.Single(t => t.NodeId == "b1");
            int corrected = new[] { a, b }.Count(t => t.Variables != null && t.Variables.ContainsKey("okA"));
            Assert.AreEqual(1, corrected,
                "nur der Zweig des Wiederaufsatzpunkts bekommt die Korrektur - der andere hat seinen eigenen Scope.");

            foreach (Token t in resumed.ActiveTokens.ToList())
            {
                engine.RunBranch(inst.Id, t.Id);
            }

            WorkflowInstance after = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, after.Status,
                "ein Zweig laeuft durch, der andere scheitert erneut - der Fall braucht einen zweiten Retry.");
            Assert.AreEqual(1, after.ActiveTokens.Count(),
                "genau der noch nicht korrigierte Zweig steht noch da.");
        }

        [TestMethod]
        public void TwoFaultedBranches_PerBranchCorrections_FixBothInOneGo()
        {
            // Der Gegenentwurf: Korrekturen JE ZWEIG - dann reicht ein Durchgang.
            SaveTwoFailingBranches();
            var runs = new Dictionary<string, int>(StringComparer.Ordinal);
            var engine = new WorkflowEngine(store, NeedsOwnVariable(runs));

            WorkflowInstance inst = engine.CreateInstance("wf");
            foreach (string branch in engine.RunBranch(inst.Id, inst.ActiveTokens.Single().Id))
            {
                engine.RunBranch(inst.Id, branch);
            }

            IReadOnlyList<Token> stalled = WorkflowEngine.FindStalledBranches(store.GetInstance(inst.Id));
            Assert.AreEqual(2, stalled.Count, "beide gescheiterten Zweige werden angeboten.");
            CollectionAssert.AreEquivalent(new[] { "a1", "b1" }, stalled.Select(t => t.NodeId).ToList());

            var updates = stalled.ToDictionary(
                t => t.Id,
                t => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    [t.NodeId == "a1" ? "okA" : "okB"] = true
                });

            Assert.IsTrue(engine.RetryFaultedBranches(inst.Id, updates));

            DriveToCompletion(engine, inst.Id);

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status,
                "beide Zweige laufen durch und der Join feuert - EIN Durchgang genuegt.");
            Assert.AreEqual(2, runs["okA"], "a1: einmal gescheitert, einmal erfolgreich.");
            Assert.AreEqual(2, runs["okB"], "b1 ebenso.");
        }

        [TestMethod]
        public void RetryWithUnknownTokenId_IsIgnored_AndDoesNotBlockTheResume()
        {
            // Ein Zweig kann zwischen Anzeige und Absenden weitergelaufen sein - das darf den Wiederaufsatz
            // nicht scheitern lassen (die Korrektur faellt dann eben weg und steht im Log).
            SaveLinear();
            var engine = new WorkflowEngine(store, NeedsAmount());
            WorkflowInstance inst = engine.StartWorkflow("wf");

            var updates = new Dictionary<string, IDictionary<string, object>>
            {
                ["does-not-exist"] = new Dictionary<string, object> { ["amount"] = 5m }
            };

            Assert.IsTrue(engine.RetryFaultedBranches(inst.Id, updates));
            Assert.AreEqual(WorkflowStatus.Running, store.GetInstance(inst.Id).Status);
        }

        /// <summary>Treibt alle aktiven Zweige, bis keiner mehr aktiv ist (nebenlaeufiger Weg von Hand).</summary>
        private void DriveToCompletion(WorkflowEngine engine, string instanceId)
        {
            for (int round = 0; round < 10; round++)
            {
                List<Token> active = store.GetInstance(instanceId).ActiveTokens.ToList();
                if (active.Count == 0)
                {
                    return;
                }

                foreach (Token t in active)
                {
                    engine.RunBranch(instanceId, t.Id);
                }
            }

            Assert.Fail("der Vortrieb kam in 10 Runden nicht zum Ende.");
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

            InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
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

            InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
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
