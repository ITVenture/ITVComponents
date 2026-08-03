using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die <see cref="ActivityIteration"/>: eine Aktivitaet laeuft je Element einer Sammlung,
    /// wahlweise mehrere Elemente gleichzeitig, und die Engine fasst die Ergebnisse zu Listen zusammen.
    /// Der Zweig bleibt dabei EIN Zweig - es entstehen keine zusaetzlichen Tokens.
    /// </summary>
    [TestClass]
    public class WorkflowIterationTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Die Elemente einer Iterations-Ausgabe. Die Engine liefert ein Array (typisiert, wenn die
        /// Elemente einheitlich sind) - der Test soll sich nicht auf den genauen Elementtyp festlegen.
        /// </summary>
        private static List<object> Items(object value) => ((IEnumerable)value).Cast<object>().ToList();

        /// <summary>
        /// Baut eine Definition mit genau einem Iterations-Knoten: Start -> a -> Ende (plus optionalem
        /// Fehler-Ausgang). Die Sammlung kommt aus der Variablen "items".
        /// </summary>
        private AutomatedActivityNode BuildDefinition(ActivityIteration iteration, bool withErrorFlow = false,
            params ActivityOutputBinding[] outputs)
        {
            var node = new AutomatedActivityNode
            {
                Id = "a",
                ActivityRef = "step",
                Iteration = iteration,
                ErrorFlowId = withErrorFlow ? "a->handled" : null
            };
            node.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "files", Kind = ParameterBindingKind.Variable, Source = "items"
            });
            node.Outputs.AddRange(outputs);

            var nodes = new List<WorkflowNode>
            {
                new StartNode { Id = "s" }, node, new EndNode { Id = "ok" }
            };
            var flows = new List<SequenceFlow> { F("s", "a"), F("a", "ok") };
            if (withErrorFlow)
            {
                nodes.Add(new EndNode { Id = "handled" });
                flows.Add(F("a", "handled"));
            }

            store.SaveDefinition(new WorkflowDefinition { Id = "wf", Nodes = nodes, Flows = flows });
            return node;
        }

        [TestMethod]
        public void Serial_RunsOncePerItem_AndCollectsOutputsInInputOrder()
        {
            BuildDefinition(new ActivityIteration { ItemsInput = "files", MaxParallel = 1 },
                outputs: new ActivityOutputBinding { Parameter = "signed", Variable = "results" });
            var seen = new List<object>();
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                lock (seen)
                {
                    seen.Add(ctx.Inputs["files"]);
                }

                ctx.Outputs["signed"] = $"signed:{ctx.Inputs["files"]}";
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            CollectionAssert.AreEqual(new object[] { "a", "b", "c" }, seen,
                "serial iteration runs the items in input order.");
            var results = Items(final.Variables["results"]);
            CollectionAssert.AreEqual(new object[] { "signed:a", "signed:b", "signed:c" }, results,
                "the per-item outputs are collected as a list, in INPUT order.");
        }

        [TestMethod]
        public void Iteration_LeavesTheBranchIntact()
        {
            BuildDefinition(new ActivityIteration { ItemsInput = "files", MaxParallel = 4 });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", _ => { }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", Enumerable.Range(0, 50).Cast<object>().ToList() } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(1, final.Tokens.Count,
                "an iteration is not a fan-out: 50 items must not produce 50 tokens.");
        }

        [TestMethod]
        public void Parallel_RunsItemsConcurrently_AndStillOrdersResults()
        {
            const int count = 4;
            BuildDefinition(new ActivityIteration { ItemsInput = "files", MaxParallel = count },
                outputs: new ActivityOutputBinding { Parameter = "n", Variable = "results" });

            // Der Nachweis der Nebenlaeufigkeit ohne Zeitmessung: jeder Lauf meldet sich an und wartet, bis
            // ALLE angekommen sind. Liefe die Iteration seriell, kaeme keiner ueber das Warten hinaus.
            // Das Ergebnis wird gemerkt statt hier behauptet - eine Assertion im Element-Lauf faenge die
            // Engine als Element-Fehler ab, und der Test scheiterte an der falschen Stelle.
            using var allArrived = new CountdownEvent(count);
            bool overlapped = true;
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                allArrived.Signal();
                if (!allArrived.Wait(TimeSpan.FromSeconds(5)))
                {
                    overlapped = false;
                }

                ctx.Outputs["n"] = ctx.Inputs["files"];
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", Enumerable.Range(0, count).Cast<object>().ToList() } });

            Assert.IsTrue(overlapped, "the items did not run at the same time - the iteration did not parallelise.");
            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            var results = Items(final.Variables["results"]);
            CollectionAssert.AreEqual(Enumerable.Range(0, count).Cast<object>().ToList(), results,
                "despite running concurrently the results keep the input order.");
        }

        [TestMethod]
        public void ItemParameter_AndIndexParameter_AreHandedIn()
        {
            BuildDefinition(new ActivityIteration
            {
                ItemsInput = "files", ItemParameter = "file", IndexParameter = "i", MaxParallel = 1
            }, outputs: new ActivityOutputBinding { Parameter = "line", Variable = "results" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                // Unter einem EIGENEN Namen bleibt die vollstaendige Sammlung zusaetzlich sichtbar.
                Assert.IsInstanceOfType(ctx.Inputs["files"], typeof(string[]));
                ctx.Outputs["line"] = $"{ctx.Inputs["i"]}:{ctx.Inputs["file"]}";
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "x", "y" } } });

            var results = Items(store.GetInstance(inst.Id).Variables["results"]);
            CollectionAssert.AreEqual(new object[] { "0:x", "1:y" }, results);
        }

        [TestMethod]
        public void NullCollection_IsAnEmptyRun_NotAFailure()
        {
            BuildDefinition(new ActivityIteration { ItemsInput = "files" });
            bool ran = false;
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", _ => ran = true));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status,
                "nothing to do is a normal case, not an error.");
            Assert.IsFalse(ran);
        }

        [TestMethod]
        public void StringCollection_IsRejected_InsteadOfIteratingCharacters()
        {
            BuildDefinition(new ActivityIteration { ItemsInput = "files" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", _ => { }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", "abc" } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status,
                "a string is enumerable, but iterating it character by character is never the intent.");
            StringAssert.Contains(final.FaultMessage, "not a collection");
        }

        [TestMethod]
        public void UnboundCollectionParameter_Faults_WithAClearMessage()
        {
            BuildDefinition(new ActivityIteration { ItemsInput = "notBound" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", _ => { }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status);
            StringAssert.Contains(final.FaultMessage, "does not bind that parameter");
        }

        [TestMethod]
        public void FailingItem_WithoutContinue_AbortsAndTakesTheErrorFlow()
        {
            BuildDefinition(new ActivityIteration { ItemsInput = "files", MaxParallel = 1 },
                withErrorFlow: true);
            int runs = 0;
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                Interlocked.Increment(ref runs);
                if ((string)ctx.Inputs["files"] == "b")
                {
                    throw new InvalidOperationException("bad file");
                }
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c", "d" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status, "the error flow handled it - no fault.");
            Assert.AreEqual(2, runs, "the run stops after the failing item instead of grinding through the rest.");
        }

        [TestMethod]
        public void ContinueOnError_AttemptsEverything_AndReportsTheFailures()
        {
            BuildDefinition(new ActivityIteration
                {
                    ItemsInput = "files", MaxParallel = 1, ContinueOnError = true,
                    FailedItemsOutput = "failed", SucceededCountOutput = "okCount"
                },
                withErrorFlow: true,
                new ActivityOutputBinding { Parameter = "failed", Variable = "failures" },
                new ActivityOutputBinding { Parameter = "okCount", Variable = "signedCount" });
            int runs = 0;
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                Interlocked.Increment(ref runs);
                if ((string)ctx.Inputs["files"] == "b")
                {
                    ctx.Fail("locked");
                }
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(3, runs, "every item is attempted.");
            var failures = (Items(final.Variables["failures"])).Cast<IterationFailure>().ToList();
            Assert.AreEqual(1, failures.Count);
            Assert.AreEqual("b", failures[0].Item);
            Assert.AreEqual(1, failures[0].Index, "the position in the input collection is part of the report.");
            Assert.AreEqual("locked", failures[0].Message);
            Assert.AreEqual(2, final.Variables["signedCount"]);
        }

        [TestMethod]
        public void Failure_TellsDeclinedApartFromCrashed()
        {
            BuildDefinition(new ActivityIteration
                {
                    ItemsInput = "files", MaxParallel = 1, ContinueOnError = true, FailedItemsOutput = "failed"
                },
                withErrorFlow: true,
                new ActivityOutputBinding { Parameter = "failed", Variable = "failures" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                switch ((string)ctx.Inputs["files"])
                {
                    case "declined":
                        ctx.Fail("not allowed");
                        break;
                    case "crashed":
                        throw new InvalidOperationException("boom");
                }
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "ok", "declined", "crashed" } } });

            var failures = Items(store.GetInstance(inst.Id).Variables["failures"]).Cast<IterationFailure>().ToList();
            Assert.AreEqual(2, failures.Count);

            Assert.IsFalse(failures[0].WasThrown, "ctx.Fail is a controlled refusal, not a crash.");
            Assert.IsNull(failures[0].ExceptionType);
            Assert.IsNull(failures[0].ExceptionDetail);
            Assert.AreEqual("not allowed", failures[0].Message);

            Assert.IsTrue(failures[1].WasThrown);
            Assert.AreEqual(typeof(InvalidOperationException).FullName, failures[1].ExceptionType);
            Assert.AreEqual("boom", failures[1].Message);
            StringAssert.Contains(failures[1].ExceptionDetail, "InvalidOperationException",
                "the written-out exception (with stack trace) is what makes the entry worth having.");
        }

        [TestMethod]
        public void FailureList_SurvivesTheVariableSerialization()
        {
            // Der Grund, warum die Ausnahme als DATEN und nicht als Objekt drinsteht: die Fehlerliste
            // landet ueber die Ausgabe-Bindung in einer Variable, und die Variablen gehen beim Commit
            // durch WorkflowJson. Ein Exception-Objekt liesse genau das scheitern - nachdem die Arbeit
            // getan ist.
            BuildDefinition(new ActivityIteration
                {
                    ItemsInput = "files", MaxParallel = 1, ContinueOnError = true, FailedItemsOutput = "failed"
                },
                withErrorFlow: true,
                new ActivityOutputBinding { Parameter = "failed", Variable = "failures" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step",
                _ => throw new InvalidOperationException("boom")));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            string json = Serialization.WorkflowJson.Serialize(final.Variables);

            StringAssert.Contains(json, "InvalidOperationException");
            StringAssert.Contains(json, "boom");
        }

        [TestMethod]
        public void FailingItem_WithoutErrorFlow_FaultsTheInstance()
        {
            BuildDefinition(new ActivityIteration { ItemsInput = "files", MaxParallel = 1 });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                if ((string)ctx.Inputs["files"] == "b")
                {
                    throw new InvalidOperationException("bad file");
                }
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status,
                "without an error flow a failed item faults the instance - like any other activity failure.");
            StringAssert.Contains(final.FaultMessage, "1 of 2");
        }

        [TestMethod]
        public void TheThreeListsHaveTheirOwnShapes()
        {
            // Die eine Zusage, an der die Wiederholungs-Schleife haengt: Erledigtes als ERGEBNIS,
            // Offenes als ORIGINAL (es muss wieder in die Sammlung passen), Fehler als Diagnose-Eintrag.
            BuildDefinition(new ActivityIteration
                {
                    ItemsInput = "files", MaxParallel = 1, ContinueOnError = true,
                    ItemResultOutput = "processed",
                    SucceededItemsOutput = "done", PendingItemsOutput = "open", FailedItemsOutput = "problems"
                },
                withErrorFlow: true,
                new ActivityOutputBinding { Parameter = "done", Variable = "doneFiles" },
                new ActivityOutputBinding { Parameter = "open", Variable = "openFiles" },
                new ActivityOutputBinding { Parameter = "problems", Variable = "problemList" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                var file = (string)ctx.Inputs["files"];
                if (file == "b")
                {
                    ctx.Fail("locked");
                    return;
                }

                ctx.Outputs["processed"] = file + "_signed";
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            CollectionAssert.AreEqual(new object[] { "a_signed", "c_signed" },
                Items(final.Variables["doneFiles"]),
                "the succeeded list carries the RESULTS - that is what gets carried forward.");
            CollectionAssert.AreEqual(new object[] { "b" }, Items(final.Variables["openFiles"]),
                "the pending list carries the ORIGINALS - that is what goes back into the next attempt.");

            var problems = (Items(final.Variables["problemList"])).Cast<IterationFailure>().ToList();
            Assert.AreEqual(1, problems.Count);
            Assert.AreEqual("b", problems[0].Item, "the failure keeps the original next to its cause.");
            Assert.AreEqual("locked", problems[0].Message);
        }

        [TestMethod]
        public void RetryLoop_CarriesOverEarlierResults_AndEndsUpComplete()
        {
            // Der Kern des Wiederholungs-Musters: Durchlauf 2 bearbeitet nur den Rest, das Ergebnis ist
            // trotzdem vollstaendig. Beide Durchlaeufe laufen ueber DENSELBEN Knoten - eine
            // Fehlerkante, die auf ihn zurueckfuehrt, ist genau das, was ein Modellierer baut.
            var node = new AutomatedActivityNode
            {
                Id = "a",
                ActivityRef = "step",
                ErrorFlowId = "a->a",
                Iteration = new ActivityIteration
                {
                    ItemsInput = "todo", MaxParallel = 1, ContinueOnError = true,
                    ItemResultOutput = "processed",
                    SucceededItemsOutput = "done", PendingItemsOutput = "stillOpen",
                    CarryOverInput = "alreadyDone"
                }
            };
            // Die Sammlung ist die RESTLISTE, die die Fehlerkante zurueckschreibt; die Uebernahme ist das
            // Ergebnis des vorigen Durchlaufs.
            node.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "todo", Kind = ParameterBindingKind.Variable, Source = "items"
            });
            node.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "alreadyDone", Kind = ParameterBindingKind.Variable, Source = "signed"
            });
            node.Outputs.Add(new ActivityOutputBinding { Parameter = "done", Variable = "signed" });
            node.Outputs.Add(new ActivityOutputBinding { Parameter = "stillOpen", Variable = "items" });
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" }, node, new EndNode { Id = "ok" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "a"), F("a", "ok"),
                    new SequenceFlow { Id = "a->a", SourceId = "a", TargetId = "a" }
                }
            });

            // Beim ERSTEN Anlauf scheitert "b", beim zweiten geht es durch (der klassische transiente Fehler).
            var attempts = new Dictionary<string, int>();
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                var file = (string)ctx.Inputs["todo"];
                attempts.TryGetValue(file, out int seen);
                attempts[file] = seen + 1;
                if (file == "b" && seen == 0)
                {
                    ctx.Fail("locked");
                    return;
                }

                ctx.Outputs["processed"] = file + "_signed";
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            CollectionAssert.AreEqual(new object[] { "a_signed", "c_signed", "b_signed" },
                Items(final.Variables["signed"]),
                "the second pass must ADD to the first one's result, not replace it - carried over first.");
            Assert.AreEqual(0, (Items(final.Variables["items"])).Count, "nothing is left open.");
            Assert.AreEqual(1, attempts["a"], "an item that succeeded must not be processed a second time.");
            Assert.AreEqual(2, attempts["b"]);
        }

        [TestMethod]
        public void PendingItems_AlsoContainWhatWasNeverAttempted()
        {
            // Ohne ContinueOnError bleibt nach dem Abbruch Arbeit liegen. Genau die verlöre ein Retry,
            // der sich auf die FEHLGESCHLAGENEN verlaesst.
            BuildDefinition(new ActivityIteration
                {
                    ItemsInput = "files", MaxParallel = 1,
                    FailedItemsOutput = "failed", PendingItemsOutput = "pending"
                },
                withErrorFlow: true,
                new ActivityOutputBinding { Parameter = "failed", Variable = "failedFiles" },
                new ActivityOutputBinding { Parameter = "pending", Variable = "pendingFiles" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                if ((string)ctx.Inputs["files"] == "b")
                {
                    throw new InvalidOperationException("bad file");
                }
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c", "d" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            var failures = (Items(final.Variables["failedFiles"])).Cast<IterationFailure>().ToList();
            CollectionAssert.AreEqual(new object[] { "b" }, failures.Select(f => f.Item).ToArray(),
                "only 'b' actually failed.");
            CollectionAssert.AreEqual(new object[] { "b", "c", "d" }, Items(final.Variables["pendingFiles"]),
                "'c' and 'd' were never attempted - a retry on the failed list alone would drop them.");
        }

        [TestMethod]
        public void CarryOver_OfAnUnboundParameter_Faults()
        {
            BuildDefinition(new ActivityIteration
            {
                ItemsInput = "files", CarryOverInput = "notBound", SucceededItemsOutput = "done"
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", _ => { }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Faulted, final.Status,
                "a mis-bound carry-over must fail loudly - silently it would lose every earlier result.");
            StringAssert.Contains(final.FaultMessage, "does not bind that parameter");
        }

        [TestMethod]
        public void MissingItemResult_IsReported_InsteadOfSilentNulls()
        {
            BuildDefinition(new ActivityIteration
                {
                    ItemsInput = "files", MaxParallel = 1,
                    ItemResultOutput = "processed", SucceededItemsOutput = "done"
                },
                outputs: new ActivityOutputBinding { Parameter = "done", Variable = "doneFiles" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                // Schreibt den deklarierten Ergebnis-Parameter NICHT.
                ctx.Outputs["somethingElse"] = 1;
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            HistoryEntry warning = final.History.FirstOrDefault(h => h.Event == "IterationResultMissing");
            Assert.IsNotNull(warning, "gaps in the succeeded list must not appear out of nowhere.");
            Assert.AreEqual(HistorySeverity.Warning, warning.Severity);
            StringAssert.Contains(warning.Detail, "processed");
        }

        [TestMethod]
        public void VariableWritesOfAnItem_AreDiscarded_ButLogged()
        {
            BuildDefinition(new ActivityIteration { ItemsInput = "files", MaxParallel = 2 });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("step",
                ctx => ctx.Variables["sideEffect"] = "written"));

            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b" } } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.IsFalse(final.Variables.ContainsKey("sideEffect"),
                "every item works on its own copy of the scope - the write cannot be merged.");
            HistoryEntry warning = final.History
                .FirstOrDefault(h => h.Event == "IterationVariablesDiscarded");
            Assert.IsNotNull(warning, "discarding must not happen silently.");
            StringAssert.Contains(warning.Detail, "sideEffect");
            Assert.AreEqual(HistorySeverity.Warning, warning.Severity);
        }
    }
}
