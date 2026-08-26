using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Formatting;
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
        public void UserTask_Descriptor_CarriesTheFormatData_AndTheRawDescription()
        {
            // Der Titel wird beim PARKEN formatiert (er steht fertig in der Arbeitsliste), die Beschreibung
            // erst beim ANZEIGEN. Der Descriptor muss dafuer beides mitbringen: die rohe Beschreibung UND
            // das ausgewertete Datenobjekt.
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Title = "Invoice [InvoiceNo]";
                node.Description = "Please approve invoice [InvoiceNo] for [Customer].";
                node.FormatData = "return {InvoiceNo: number, Customer: customer};";
                node.FormatDataMode = ScriptMode.Block;
            }));

            WorkflowInstance instance = engine.StartWorkflow("t",
                new Dictionary<string, object> { { "number", "4711" }, { "customer", "ACME" } });
            Token token = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting);

            UserTaskDescriptor descriptor = engine.DescribeUserTask(instance.Id, token.Id);
            Assert.AreEqual("Invoice 4711", descriptor.Title, "der Titel ist beim Parken formatiert worden.");
            Assert.AreEqual("Please approve invoice [InvoiceNo] for [Customer].", descriptor.Description,
                "die Beschreibung kommt ROH heraus - formatiert wird sie in der Anzeige.");
            Assert.IsNotNull(descriptor.FormatData, "...und dafuer muss das Datenobjekt mitkommen.");

            // Genau der Schritt, den die Anzeige macht: Datenobjekt.FormatText(Prototyp).
            Assert.AreEqual("Please approve invoice 4711 for ACME.",
                descriptor.FormatData.FormatText(descriptor.Description,
                    TextFormat.DefaultFormatPolicyWithPrimitives));
        }

        [TestMethod]
        public void UserTask_FormatDataAsExpression_WithAnObjectLiteral_FormatsNothing()
        {
            // DIE Falle: ScriptMode.Expression ist der Standard, und der Ausdrucks-Parser lehnt ein
            // Objekt-Literal am Anfang ab. Titel UND Beschreibung bleiben dann als Prototyp stehen - der
            // Fehler steht nur im Log. Festgehalten, damit die Diagnose beim naechsten Mal schneller geht.
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Title = "Invoice [InvoiceNo]";
                node.Description = "Approve [InvoiceNo].";
                node.FormatData = "{InvoiceNo: number}";   // ohne 'return', Modus Expression (Standard)
            }));

            WorkflowInstance instance = engine.StartWorkflow("t",
                new Dictionary<string, object> { { "number", "4711" } });
            Token token = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting);

            Assert.AreEqual("Invoice [InvoiceNo]", token.TaskTitle,
                "der Prototyp bleibt stehen, wenn das Datenobjekt nicht auswertbar ist.");
            Assert.IsNull(engine.DescribeUserTask(instance.Id, token.Id).FormatData,
                "und die Anzeige bekommt kein Datenobjekt - die Beschreibung bliebe ebenso roh.");
        }

        [TestMethod]
        public void UserTask_FormatDataAsExpression_WithAnAssignedObjectLiteral_Works()
        {
            // Der Parser stoert sich nur an einem '{' am ANFANG (dort waere es ein Block). Steht das
            // Objekt-Literal rechts von einer Zuweisung, ist der Ausdrucks-Modus voellig in Ordnung - und
            // spart den Modus-Schalter. Der Wert der Zuweisung ist zugleich das Ergebnis des Ausdrucks.
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Title = "Invoice [InvoiceNo]";
                node.Description = "Approve [InvoiceNo].";
                node.FormatData = "x = {InvoiceNo: number}";   // Modus Expression (Standard)
            }));

            WorkflowInstance instance = engine.StartWorkflow("t",
                new Dictionary<string, object> { { "number", "4711" } });
            Token token = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting);

            Assert.AreEqual("Invoice 4711", token.TaskTitle);

            UserTaskDescriptor descriptor = engine.DescribeUserTask(instance.Id, token.Id);
            Assert.AreEqual("Approve 4711.",
                descriptor.FormatData.FormatText(descriptor.Description,
                    TextFormat.DefaultFormatPolicyWithPrimitives));

            // Und die Zuweisung darf nichts hinterlassen: der Auswerter arbeitet auf einer KOPIE des
            // Scopes. Sonst truege jede Instanz eine Hilfsvariable aus der Formatierung mit sich herum -
            // persistiert, sichtbar im Monitoring und im Zugriff jeder spaeteren Bedingung.
            Assert.IsFalse(instance.Variables.ContainsKey("x"),
                "die Hilfsvariable der Formatierung darf nicht im Instanz-Scope landen.");
        }

        [TestMethod]
        public void UserTask_FormatDataAsBlock_WithoutReturn_FormatsNothing()
        {
            // Die zweite Falle derselben Familie: ein Block OHNE return liefert null - ohne Fehler.
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Title = "Invoice [InvoiceNo]";
                node.FormatData = "{InvoiceNo: number}";
                node.FormatDataMode = ScriptMode.Block;
            }));

            WorkflowInstance instance = engine.StartWorkflow("t",
                new Dictionary<string, object> { { "number", "4711" } });

            Assert.AreEqual("Invoice [InvoiceNo]",
                instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).TaskTitle);
        }

        [TestMethod]
        public void UserTask_CultureJsonTitle_IsFormattedPerLanguage()
        {
            // Kultur-JSON: JEDE Sprach-Property wird als Prototyp formatiert, das Objekt bleibt JSON.
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Title = "{\"de\":\"Rechnung [InvoiceNo]\",\"fr\":\"Facture [InvoiceNo]\"}";
                node.FormatData = "return {InvoiceNo: number};";
                node.FormatDataMode = ScriptMode.Block;
            }));

            WorkflowInstance instance = engine.StartWorkflow("t",
                new Dictionary<string, object> { { "number", "4711" } });

            string title = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).TaskTitle;
            StringAssert.Contains(title, "Rechnung 4711");
            StringAssert.Contains(title, "Facture 4711");
            StringAssert.Contains(title, "\"de\"", "und bleibt mehrsprachig - uebersetzt wird beim Anzeigen.");
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

        // --- Ende des gefuehrten Teils --------------------------------------------------------------

        /// <summary>
        /// Der gefuehrte Ablauf ist eine Eigenschaft des KNOTENS und steht deshalb in der Beschreibung, die
        /// die Oberflaeche beim Oeffnen bekommt - egal, ob sie aus einem Modul heraus oder aus der
        /// Arbeitsliste oeffnet. Ohne das haenge der Assistent daran, WO der Benutzer eingestiegen ist.
        /// </summary>
        [TestMethod]
        public void Descriptor_TellsWhetherTheTaskRunsInAnAssistant()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.RunsInAssistant = true;
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            Assert.IsTrue(engine.DescribeUserTask(instance.Id, tokenId).RunsInAssistant);
        }

        /// <summary>Ohne Angabe steht eine Aufgabe fuer sich - das bisherige Verhalten.</summary>
        [TestMethod]
        public void Descriptor_DefaultsToNoAssistant()
        {
            store.SaveDefinition(OneTask("t", node => node.TaskKey = "Check"));
            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            Assert.IsFalse(engine.DescribeUserTask(instance.Id, tokenId).RunsInAssistant);
        }

        /// <summary>Ohne Ausdruck bleibt es beim Bisherigen: der Vorgang gilt als fortsetzbar.</summary>
        [TestMethod]
        public void CompleteUserTask_WithoutEndsAssistant_DoesNotEndIt()
        {
            store.SaveDefinition(OneTask("t", node => node.TaskKey = "Check"));
            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            Assert.IsFalse(engine.CompleteUserTask(instance.Id, tokenId).EndsAssistant);
        }

        /// <summary>
        /// Der eigentliche Punkt des Ausdrucks: er wird NACH dem Uebernehmen der Ergebniswerte
        /// ausgewertet - die Antwort darf also von dem abhaengen, was der Benutzer gerade eingegeben hat.
        /// Wuerde er vorher laufen, saehe er den Wert von vorhin und die Entscheidung waere immer einen
        /// Schritt zu spaet.
        /// </summary>
        [TestMethod]
        public void EndsAssistant_SeesTheResultOfTheStepItCloses()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Outputs.Add(new ActivityOutputBinding { Parameter = "decision", Variable = "approved" });
                node.EndsAssistant = "approved";
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            UserTaskCompletionResult result = engine.CompleteUserTask(instance.Id, tokenId,
                new Dictionary<string, object> { { "decision", true } });

            Assert.AreEqual(UserTaskCompletionStatus.Completed, result.Status);
            Assert.IsTrue(result.EndsAssistant);
        }

        /// <summary>Derselbe Knoten, andere Eingabe - der gefuehrte Teil laeuft weiter.</summary>
        [TestMethod]
        public void EndsAssistant_IsFalseWhenTheExpressionSaysSo()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.Outputs.Add(new ActivityOutputBinding { Parameter = "decision", Variable = "approved" });
                node.EndsAssistant = "approved";
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            Assert.IsFalse(engine.CompleteUserTask(instance.Id, tokenId,
                new Dictionary<string, object> { { "decision", false } }).EndsAssistant);
        }

        /// <summary>
        /// Ein kaputter Ausdruck faultet die Instanz NICHT - anders als bei der Zuweisung. Die Aufgabe ist
        /// erledigt und ihr Ergebnis gespeichert; daran darf eine Anzeige-Entscheidung nichts mehr aendern.
        /// </summary>
        [TestMethod]
        public void EndsAssistant_ThatFails_DoesNotFaultTheInstance()
        {
            store.SaveDefinition(OneTask("t", node =>
            {
                node.TaskKey = "Check";
                node.EndsAssistant = "kaputt(";
            }));

            WorkflowInstance instance = engine.StartWorkflow("t");
            string tokenId = instance.Tokens.Single(x => x.Status == TokenStatus.Waiting).Id;

            UserTaskCompletionResult result = engine.CompleteUserTask(instance.Id, tokenId);

            Assert.AreEqual(UserTaskCompletionStatus.Completed, result.Status);
            Assert.IsFalse(result.EndsAssistant);
            Assert.AreNotEqual(WorkflowStatus.Faulted, store.GetInstance(instance.Id).Status);
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
                TechnicalName = "par",
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
                TechnicalName = "par",
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
                TechnicalName = id,
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
