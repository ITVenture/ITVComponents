using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Expressions;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die <b>Benutzer-Aufgabe</b>: das Parken samt Aufgaben-Stempel (den die Arbeitsliste braucht),
    /// den Abschluss ueber <see cref="WorkflowEngine.CompleteUserTask"/> und die Abgrenzung gegen die
    /// bestehenden Wartepunkte - eine Aufgabe darf weder von einem Signal noch von einem Timer
    /// weitergeschoben werden.
    /// </summary>
    [TestClass]
    public class WorkflowUserTaskTest
    {
        private InMemoryWorkflowStore store;
        private ActivityRegistry activities;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            activities = new ActivityRegistry();
            engine = new WorkflowEngine(store, activities);
        }

        // --- Parken ---------------------------------------------------------------------------------

        [TestMethod]
        public void UserTask_Parks_AndStampsWhatTheWorkListNeeds()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "ApproveInvoice";
                node.RequiredPermission = "Invoice.Approve";
                node.Title = "Rechnung freigeben";
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");

            Token token = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting);
            Assert.AreEqual("ApproveInvoice", token.TaskKey);
            Assert.AreEqual("Invoice.Approve", token.TaskPermission);
            Assert.AreEqual("Rechnung freigeben", token.TaskTitle);
            Assert.IsNotNull(token.TaskCreatedUtc);
            Assert.IsNull(token.AssignedTo, "without an assignment expression the task belongs to the pool.");
            Assert.IsNull(token.WaitingSignal,
                "a task must not be wakeable by a signal - that would wake every task of the same name.");
        }

        [TestMethod]
        public void UserTask_Assignment_IsEvaluatedOnceAgainstTheBranchScope()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Assignment = "owner";
            }));

            WorkflowInstance instance = engine.StartWorkflow("t",
                new Dictionary<string, object> { { "owner", "anna" } });

            Assert.AreEqual("anna", instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).AssignedTo);
        }

        [TestMethod]
        public void UserTask_FailingAssignment_FaultsTheInstance()
        {
            // Bewusst ein Fault: eine nicht zugewiesene Aufgabe waere fuer JEDEN mit der Permission
            // sichtbar - das ist eine Sichtbarkeits-Ausweitung, kein kosmetisches Problem.
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Assignment = "this is not a valid expression $$";
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
        }

        [TestMethod]
        public void UserTask_TitleIsFormattedFromTheFormatData()
        {
            // Einen eigenen Titel-AUSDRUCK gibt es nicht mehr: der Titel ist selbst ein Format-Prototyp
            // und zieht seine Werte aus demselben FormatData wie die Beschreibung.
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Title = "Invoice [InvoiceNo]";
                // Als Block: ein Objekt-Literal am Anfang lehnt der Ausdrucks-Parser ab - genau dafuer
                // gibt es den Modus-Schalter am Feld.
                node.FormatData = "return {InvoiceNo: number};";
                node.FormatDataMode = ScriptMode.Block;
            }));

            WorkflowInstance instance = engine.StartWorkflow("t",
                new Dictionary<string, object> { { "number", "4711" } });

            Assert.AreEqual("Invoice 4711",
                instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).TaskTitle);
        }

        [TestMethod]
        public void UserTask_CultureJsonTitle_StaysUnresolved()
        {
            // Uebersetzt wird beim ANZEIGEN - sonst bestimmte die Kultur des ausfuehrenden Runners die
            // Sprache des Lesers.
            const string cultureJson = "{\"de\":\"Freigabe\",\"fr\":\"Approbation\"}";
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Title = cultureJson;
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");

            Assert.AreEqual(cultureJson,
                instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).TaskTitle);
        }

        [TestMethod]
        public void UserTask_DueDate_DoesNotBecomeATimer()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.DueInHours = -1; // laengst ueberfaellig, falls die Frist doch als Timer zaehlte
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");
            Token token = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting);

            Assert.IsNull(token.DueUtc, "TaskDueUtc darf nicht die Timer-Faelligkeit sein.");
            Assert.IsFalse(engine.TriggerTimers(instance, DateTime.UtcNow.AddDays(1)),
                "an overdue task must not continue by itself.");
            Assert.AreEqual(TokenStatus.Waiting, token.Status);
        }

        [TestMethod]
        public void UserTask_IsNotWokenByASignal()
        {
            store.SaveDefinition(OneTask("t", node => node.TaskKey = "Check"));
            WorkflowInstance instance = engine.StartWorkflow("t");

            Assert.IsFalse(engine.SignalWorkflow(instance.Id, "Check"),
                "a task is completed through CompleteUserTask, never through a signal.");
        }

        // --- Beschreiben ----------------------------------------------------------------------------

        [TestMethod]
        public void DescribeUserTask_ResolvesThePayloadOnOpening()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Inputs.Add(Input("amount", ParameterBindingKind.Variable, source: "total"));
                node.Inputs.Add(Input("label", ParameterBindingKind.Literal, literal: "please check"));
                node.FormFields.Add(new UserTaskField { Name = "comment" });
            }));

            WorkflowInstance instance = engine.StartWorkflow("t",
                new Dictionary<string, object> { { "total", 99 } });
            Token token = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting);

            UserTaskDescriptor descriptor = engine.DescribeUserTask(instance.Id, token.Id);

            Assert.IsNotNull(descriptor);
            Assert.AreEqual(99, descriptor.Payload["amount"]);
            Assert.AreEqual("please check", descriptor.Payload["label"]);
            Assert.AreEqual("comment", descriptor.FormFields.Single().Name);
        }

        [TestMethod]
        public void DescribeUserTask_OfACompletedTask_IsNull()
        {
            store.SaveDefinition(OneTask("t", node => node.TaskKey = "Check"));
            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            engine.CompleteUserTask(instance.Id, tokenId);

            Assert.IsNull(engine.DescribeUserTask(instance.Id, tokenId));
        }

        // --- Abschluss ------------------------------------------------------------------------------

        [TestMethod]
        public void CompleteUserTask_MapsTheResult_AndContinuesTheBranch()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Outputs.Add(new ActivityOutputBinding { Parameter = "decision", Variable = "approved" });
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            UserTaskCompletionResult result = engine.CompleteUserTask(instance.Id, tokenId,
                new Dictionary<string, object> { { "decision", true } }, "anna");

            Assert.AreEqual(UserTaskCompletionStatus.Completed, result.Status);
            CollectionAssert.AreEqual(new[] { tokenId }, result.ActivatedTokenIds.ToArray());

            WorkflowInstance reloaded = store.GetInstance(instance.Id);
            Assert.AreEqual(true, reloaded.Variables["approved"]);

            engine.Advance(reloaded);
            Assert.AreEqual(WorkflowStatus.Completed, reloaded.Status);
        }

        [TestMethod]
        public void CompleteUserTask_ClearsTheStamp_SoItLeavesEveryWorkList()
        {
            store.SaveDefinition(OneTask("t", node => node.TaskKey = "Check"));
            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            engine.CompleteUserTask(instance.Id, tokenId);

            Token token = store.GetInstance(instance.Id).Tokens.Single(x => x.Id == tokenId);
            Assert.IsNull(token.TaskKey);
            Assert.IsNull(token.TaskTitle);
            Assert.IsNull(token.AssignedTo);
        }

        [TestMethod]
        public void CompleteUserTask_Twice_ReportsAlreadyCompleted()
        {
            // Das Rennen zweier Bearbeiter: der zweite darf keine Erfolgsmeldung sehen.
            store.SaveDefinition(OneTask("t", node => node.TaskKey = "Check"));
            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            Assert.AreEqual(UserTaskCompletionStatus.Completed,
                engine.CompleteUserTask(instance.Id, tokenId).Status);
            Assert.AreEqual(UserTaskCompletionStatus.AlreadyCompleted,
                engine.CompleteUserTask(instance.Id, tokenId).Status);
        }

        [TestMethod]
        public void CompleteUserTask_UnknownToken_ReportsNotFound()
        {
            store.SaveDefinition(OneTask("t", node => node.TaskKey = "Check"));
            WorkflowInstance instance = engine.StartWorkflow("t");

            Assert.AreEqual(UserTaskCompletionStatus.NotFound,
                engine.CompleteUserTask(instance.Id, "does-not-exist").Status);
        }

        [TestMethod]
        public void CompleteUserTask_OnlyCompletesTheGivenTask()
        {
            // Der Grund, warum der Abschluss NICHT ueber ein Signal laeuft: zwei parallele Aufgaben
            // derselben Art duerfen nicht gemeinsam wegfallen.
            var split = new ParallelGatewayNode { Id = "and" };
            var join = new ParallelGatewayNode { Id = "join" };
            var left = new UserActivityNode { Id = "l", TaskKey = "Check" };
            var right = new UserActivityNode { Id = "r", TaskKey = "Check" };
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "par",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" }, split, left, right, join, new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "and"), Flow("and", "l"), Flow("and", "r"),
                    Flow("l", "join"), Flow("r", "join"), Flow("join", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("par");
            List<Token> tasks = instance.Tokens.Where(x => x.TaskKey == "Check").ToList();
            Assert.AreEqual(2, tasks.Count);

            engine.CompleteUserTask(instance.Id, tasks[0].Id);

            List<Token> remaining = store.GetInstance(instance.Id).Tokens
                .Where(x => x.TaskKey == "Check" && x.Status == TokenStatus.Waiting).ToList();
            Assert.AreEqual(1, remaining.Count, "only the completed task disappears - not its sibling.");
            Assert.AreEqual(tasks[1].Id, remaining[0].Id);
        }

        [TestMethod]
        public void CompleteUserTask_WritesIntoTheBranchScope()
        {
            // Innerhalb einer parallelen Region arbeitet der Zweig in seiner Kopie - das Ergebnis der
            // Maske gehoert dorthin, nicht in den (eingefrorenen) Instanz-Scope.
            var split = new ParallelGatewayNode { Id = "and" };
            var join = new ParallelGatewayNode { Id = "join" };
            var task = new UserActivityNode { Id = "l", TaskKey = "Check" };
            task.Outputs.Add(new ActivityOutputBinding { Parameter = "decision", Variable = "approved" });
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "par",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" }, split, task, new WaitNode { Id = "w", SignalName = "other" },
                    join, new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    Flow("s", "and"), Flow("and", "l"), Flow("and", "w"),
                    Flow("l", "join"), Flow("w", "join"), Flow("join", "e")
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("par");
            Token taskToken = instance.Tokens.Single(x => x.TaskKey == "Check");

            engine.CompleteUserTask(instance.Id, taskToken.Id,
                new Dictionary<string, object> { { "decision", "yes" } });

            WorkflowInstance reloaded = store.GetInstance(instance.Id);
            Assert.IsFalse(reloaded.Variables.ContainsKey("approved"),
                "the instance scope stays on the state of the split while the region is open.");
            Token branch = reloaded.Tokens.Single(x => x.Id == taskToken.Id);
            Assert.AreEqual("yes", branch.Variables["approved"]);
        }

        // --- Aufbau-Helfer --------------------------------------------------------------------------

        /// <summary>Start → Benutzer-Aufgabe → End.</summary>
        private static WorkflowDefinition OneTask(string id, Action<UserActivityNode> configure)
        {
            var node = new UserActivityNode { Id = "n" };
            configure(node);
            return new WorkflowDefinition
            {
                Id = id,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    node,
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "n"), Flow("n", "e") }
            };
        }

        private static ActivityInputBinding Input(string parameter, ParameterBindingKind kind,
            string source = null, object literal = null)
        {
            return new ActivityInputBinding
            {
                Parameter = parameter, Kind = kind, Source = source, Literal = literal
            };
        }

        private static SequenceFlow Flow(string from, string to)
        {
            return new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };
        }
    }
}
