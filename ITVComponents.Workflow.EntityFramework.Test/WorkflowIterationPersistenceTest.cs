using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Serialization;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft die Iterations-Listen ueber eine <b>Park-Grenze</b> hinweg - also ueber den JSON-Round-Trip
    /// der Variablen. Muss zwingend gegen den EF-Store laufen: der In-Memory-Store liefert dieselbe
    /// Objekt-Referenz zurueck und wuerde jeden Serialisierungs-Verlust verdecken.
    /// </summary>
    [TestClass]
    public class WorkflowIterationPersistenceTest
    {
        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;
        private EfWorkflowStore store;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using (var ctx = new WorkflowContext(options))
            {
                ctx.Database.EnsureCreated();
            }

            store = new EfWorkflowStore(() => new WorkflowContext(options));
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Iteration -&gt; Fehlerkante -&gt; Wartepunkt (= Park + Commit). Genau die Form, die ein
        /// Wiederholungs-Flow mit Benutzer-Aufgabe hat.
        /// </summary>
        private void SaveDefinition()
        {
            var node = new AutomatedActivityNode
            {
                Id = "a",
                ActivityRef = "step",
                ErrorFlowId = "a->wait",
                Iteration = new ActivityIteration
                {
                    ItemsInput = "todo", MaxParallel = 1, ContinueOnError = true,
                    PendingItemsOutput = "open", FailedItemsOutput = "problems"
                }
            };
            node.Inputs.Add(new ActivityInputBinding
            {
                Parameter = "todo", Kind = ParameterBindingKind.Variable, Source = "items"
            });
            node.Outputs.Add(new ActivityOutputBinding { Parameter = "open", Variable = "items" });
            node.Outputs.Add(new ActivityOutputBinding { Parameter = "problems", Variable = "problems" });

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    node,
                    new WaitNode { Id = "wait", SignalName = "retry" },
                    new EndNode { Id = "ok" }
                },
                Flows = new List<SequenceFlow> { F("s", "a"), F("a", "ok"), F("a", "wait"), F("wait", "a") }
            });
        }

        /// <summary>
        /// „b" scheitert nur beim ERSTEN Anlauf - der klassische transiente Fehler, den ein Retry
        /// aufloest. <paramref name="seen"/> haelt fest, welche Elemente tatsaechlich verarbeitet wurden.
        /// </summary>
        private WorkflowEngine Engine(List<string> seen)
        {
            // Der Versuchszaehler ist BEWUSST von 'seen' getrennt: 'seen' wird zwischen den Durchlaeufen
            // geleert, um zu pruefen, was der zweite Durchlauf anfasst - haenge die Fehler-Logik daran,
            // scheitert 'b' erneut und der Test misst sich selbst.
            var attempts = new Dictionary<string, int>();
            return new WorkflowEngine(store, new ActivityRegistry().Register("step", ctx =>
            {
                var item = Convert.ToString(ctx.Inputs["todo"]);
                seen.Add(item);
                attempts.TryGetValue(item, out int before);
                attempts[item] = before + 1;
                if (item == "b" && before == 0)
                {
                    ctx.Fail("locked");
                }
            }));
        }

        /// <summary>Ein Datensatz, wie ihn eine Aktivitaet je Element zurueckgibt.</summary>
        public class SignItem
        {
            public string FileName { get; set; }

            public string Error { get; set; }
        }

        [TestMethod]
        public void RegisteredRecordTypes_SurviveAPark_Typed()
        {
            WorkflowJson.RegisterVariableType<SignItem>("test-sign-item");

            var node = new AutomatedActivityNode { Id = "a", ActivityRef = "record" };
            node.Outputs.Add(new ActivityOutputBinding { Parameter = "items", Variable = "items" });
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "rec",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" }, node,
                    new WaitNode { Id = "wait", SignalName = "go" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "a"), F("a", "wait"), F("wait", "e") }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("record",
                ctx => ctx.Outputs["items"] = new List<SignItem>
                {
                    new SignItem { FileName = "a_signed.pdf" },
                    new SignItem { FileName = "b.pdf", Error = "locked" }
                }));

            WorkflowInstance inst = engine.StartWorkflow("rec");

            // Der Zweig steht am Wartepunkt - die Variablen sind also durch die Ablage gegangen.
            WorkflowInstance reloaded = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Waiting, reloaded.Status);

            var items = reloaded.Variables["items"] as IEnumerable<object>;
            Assert.IsNotNull(items, $"came back as {reloaded.Variables["items"]?.GetType().FullName ?? "null"}");
            var list = items.Cast<SignItem>().ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("a_signed.pdf", list[0].FileName);
            Assert.AreEqual("locked", list[1].Error);
        }

        [TestMethod]
        public void CompositeVariables_SurviveAPark_TypedWhenRegistered()
        {
            SaveDefinition();
            WorkflowInstance inst = Engine(new List<string>()).StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c" } } });

            WorkflowInstance reloaded = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Waiting, reloaded.Status, "the branch parked at the wait node.");

            // Nicht angemeldete Elementtypen ueberleben den Park untypisiert - aber sie ueberleben ihn,
            // und zwar als gewoehnliche CLR-Werte (nicht als JsonElement, mit dem niemand arbeiten kann).
            var items = reloaded.Variables["items"] as IEnumerable<object>;
            Assert.IsNotNull(items, $"items came back as {reloaded.Variables["items"]?.GetType().FullName ?? "null"}");
            CollectionAssert.AreEqual(new object[] { "b" }, items.ToList());

            // IterationFailure ist ein Kern-Typ und ab Werk angemeldet - der bleibt typisiert.
            var problems = (reloaded.Variables["problems"] as IEnumerable<object>)?.Cast<IterationFailure>().ToList();
            Assert.IsNotNull(problems);
            Assert.AreEqual("b", problems[0].Item);
            Assert.AreEqual("locked", problems[0].Message);
        }

        [TestMethod]
        public void RetryAfterAPark_ProcessesExactlyTheRemainingItems()
        {
            SaveDefinition();
            var seen = new List<string>();
            WorkflowEngine engine = Engine(seen);
            WorkflowInstance inst = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c" } } });

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, seen, "first pass runs everything.");
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(inst.Id).Status);

            // Das Signal weckt den Zweig; er laeuft zurueck auf den Iterations-Knoten - jetzt mit der
            // Restliste, die den JSON-Round-Trip hinter sich hat. Genau hier faultete die Iteration,
            // solange sie das JsonElement nicht aufloeste.
            seen.Clear();
            engine.SignalWorkflow(inst.Id, "retry");

            WorkflowInstance after = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, after.Status,
                $"the retry pass must run through. Fault: {after.FaultMessage}");
            CollectionAssert.AreEqual(new[] { "b" }, seen,
                "the second pass works on the pending list only - not on all three again.");
        }

        [TestMethod]
        public void FailureList_StaysReadable_AfterAPark()
        {
            SaveDefinition();
            WorkflowInstance inst = Engine(new List<string>()).StartWorkflow("wf",
                new Dictionary<string, object> { { "items", new[] { "a", "b", "c" } } });

            // So kommt eine Benutzer-Aufgabe an die Fehler heran, die VOR ihrem Park entstanden sind:
            // nicht als IterationFailure-Objekte (der Typ ueberlebt die Ablage nicht), aber als
            // benannte Felder - lesbar aus CScript wie aus C#.
            var problems = ((IEnumerable<object>)store.GetInstance(inst.Id).Variables["problems"])
                .Cast<IterationFailure>().ToList();
            Assert.AreEqual(1, problems.Count);
            Assert.AreEqual("b", problems[0].Item);
            Assert.AreEqual(1, problems[0].Index);
            Assert.AreEqual("locked", problems[0].Message);
            Assert.IsNull(problems[0].ExceptionType, "a controlled Fail stays distinguishable from a crash.");
        }
    }
}
