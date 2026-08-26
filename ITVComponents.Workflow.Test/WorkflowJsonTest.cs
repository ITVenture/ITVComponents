using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft das portable JSON-Export-/Import-Format (<see cref="WorkflowJson"/>): eine reichhaltige
    /// Definition (alle Knotentypen inkl. Subworkflow-Aufruf, Fehler-Ausgang, Datenfluss-Bindungen und
    /// typerhaltender Konfiguration) ueberlebt den Round-Trip, und der Diskriminator ist typnamen-unabhaengig.
    /// </summary>
    [TestClass]
    public class WorkflowJsonTest
    {
        private static WorkflowDefinition RichDefinition()
        {
            var activity = new AutomatedActivityNode
            {
                Id = "a", Name = "Do it", ActivityRef = "step",
                ExecutionTarget = "backend",
                ScopeMode = ActivityScopeMode.Replace,
                ErrorFlowId = "a->err", ErrorVariable = "err", AttemptVariable = "tries",
                Diagram = new DiagramShape { X = 10, Y = 20, Width = 140, Height = 54 }
            };
            activity.Configuration["retries"] = 3;                 // typerhaltender object-Wert
            activity.RetainVariables.Add("tenant");
            activity.Inputs.Add(new ActivityInputBinding { Parameter = "x", Kind = ParameterBindingKind.Variable, Source = "seed" });
            activity.Inputs.Add(new ActivityInputBinding { Parameter = "y", Kind = ParameterBindingKind.Literal, Literal = 7 });
            activity.Outputs.Add(new ActivityOutputBinding { Parameter = "sum", Variable = "total" });

            var call = new CallWorkflowNode { Id = "c", SubDefinitionId = "sub", SubDefinitionVersion = 2 };
            call.Inputs.Add(new ActivityInputBinding { Parameter = "n", Kind = ParameterBindingKind.Variable, Source = "total" });
            call.Outputs.Add(new ActivityOutputBinding { Parameter = "res", Variable = "answer" });

            // Signatur und Ergebnis der Definition (Start-Parameter / End-Result).
            var start = new StartNode
            {
                Id = "s", ScopeMode = ActivityScopeMode.Replace,
                FormDescription = "{\"de\":\"Neuen Fall eroeffnen\",\"fr\":\"Ouvrir un dossier\"}"
            };
            start.RetainVariables.Add("corr");
            start.Inputs.Add(new ActivityInputBinding { Parameter = "seed", Kind = ParameterBindingKind.Literal, Literal = 5 });

            // Die Start-MASKE: was ein Mensch eingibt, wenn er die Definition von Hand startet. Dieselbe
            // Feldbeschreibung wie bei der Benutzer-Aufgabe.
            start.FormFields.Add(new UserTaskField
            {
                Name = "seed", Kind = UserTaskFieldKind.Number, Required = true,
                Label = "{\"de\":\"Startwert\",\"fr\":\"Valeur initiale\"}"
            });
            start.FormFields.Add(new UserTaskField
            {
                Name = "mode", Kind = UserTaskFieldKind.Choice, HelpText = "How fast?",
                Choices = new List<UserTaskChoice>
                {
                    new UserTaskChoice { Value = "fast", Label = "Schnell" },
                    new UserTaskChoice { Value = "slow" }
                }
            });
            var end = new EndNode { Id = "e" };
            end.Outputs.Add(new ActivityOutputBinding { Parameter = "total", Variable = "result" });
            end.RetainVariables.Add("corr");

            // Mapping auf der Verbindung (was der Stack ist, wenn ein Token hier ankommt).
            var mapped = new SequenceFlow
            {
                Id = "c->x", SourceId = "c", TargetId = "x", ScopeMode = ActivityScopeMode.Replace
            };
            mapped.Inputs.Add(new ActivityInputBinding
                { Parameter = "n", Kind = ParameterBindingKind.Variable, Source = "answer" });
            mapped.RetainVariables.Add("corr");

            // Benutzer-Aufgabe: Zustaendigkeit, Titel (Kultur-JSON) und die Deklaration der generischen Maske.
            var userTask = new UserActivityNode
            {
                Id = "u", TaskKey = "ApproveInvoice", RequiredPermission = "Invoice.Approve",
                ViewKey = "invoice-approval", Assignment = "owner",
                Title = "{\"de\":\"Freigabe\",\"fr\":\"Approbation\"}",
                Description = "Bitte pruefen", DueInHours = 48,
                ScopeMode = ActivityScopeMode.Replace
            };
            userTask.RetainVariables.Add("corr");
            userTask.Inputs.Add(new ActivityInputBinding
                { Parameter = "amount", Kind = ParameterBindingKind.Variable, Source = "total" });
            userTask.Outputs.Add(new ActivityOutputBinding { Parameter = "decision", Variable = "approved" });
            var choiceField = new UserTaskField
            {
                Name = "decision", Label = "{\"de\":\"Entscheid\"}", Kind = UserTaskFieldKind.Choice,
                Required = true, HelpText = "hint"
            };
            choiceField.Choices.Add(new UserTaskChoice { Value = "yes", Label = "Ja" });
            choiceField.Choices.Add(new UserTaskChoice { Value = "no" });
            userTask.FormFields.Add(choiceField);
            userTask.FormFields.Add(new UserTaskField
                { Name = "amount", Kind = UserTaskFieldKind.Number, ReadOnly = true, PayloadName = "amount" });

            // Ergebnis einer parallelen Region (Join-Mapping).
            var join = new ParallelGatewayNode { Id = "p", ScopeMode = ActivityScopeMode.Replace };
            join.Outputs.Add(new ActivityOutputBinding { Parameter = "a", Variable = "resultA" });
            join.RetainVariables.Add("corr");

            return new WorkflowDefinition
            {
                TechnicalName = "rich", Version = 3, Name = "Rich", TenantId = "t1",
                Nodes = new List<WorkflowNode>
                {
                    start,
                    activity,
                    call,
                    new WaitNode { Id = "w", SignalName = "go", CorrelationExpression = "id" },
                    new TimerNode { Id = "ti", DueExpression = "'System.DateTime'.UtcNow" },
                    new ExclusiveGatewayNode { Id = "x", DefaultFlowId = "x->e" },
                    userTask,
                    join,
                    end,
                    new EndNode { Id = "err" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->a", SourceId = "s", TargetId = "a" },
                    new SequenceFlow { Id = "a->c", SourceId = "a", TargetId = "c" },
                    new SequenceFlow { Id = "a->err", SourceId = "a", TargetId = "err" },
                    mapped,
                    new SequenceFlow { Id = "x->e", SourceId = "x", TargetId = "e", Condition = "n > 1" }
                }
            };
        }

        [TestMethod]
        public void Definition_RoundTrips_ThroughExportImport()
        {
            WorkflowDefinition original = RichDefinition();

            string json = WorkflowJson.ExportDefinition(original);
            WorkflowDefinition copy = WorkflowJson.ImportDefinition(json);

            Assert.AreEqual("rich", copy.TechnicalName);
            Assert.AreEqual(3, copy.Version);
            Assert.AreEqual("t1", copy.TenantId);

            // Polymorphe Knotentypen bleiben erhalten.
            Assert.IsInstanceOfType<AutomatedActivityNode>(copy.GetNode("a"));
            Assert.IsInstanceOfType<CallWorkflowNode>(copy.GetNode("c"));
            Assert.IsInstanceOfType<WaitNode>(copy.GetNode("w"));
            Assert.IsInstanceOfType<TimerNode>(copy.GetNode("ti"));
            Assert.IsInstanceOfType<ExclusiveGatewayNode>(copy.GetNode("x"));
            Assert.IsInstanceOfType<ParallelGatewayNode>(copy.GetNode("p"));
            Assert.IsInstanceOfType<UserActivityNode>(copy.GetNode("u"));

            // Die Benutzer-Aufgabe traegt ihre Zustaendigkeit, ihre Texte (unaufgeloest) und die
            // Deklaration der generischen Maske durch den Round-Trip.
            var u = (UserActivityNode)copy.GetNode("u");
            Assert.AreEqual("ApproveInvoice", u.TaskKey);
            Assert.AreEqual("Invoice.Approve", u.RequiredPermission);
            Assert.AreEqual("invoice-approval", u.ViewKey);
            Assert.AreEqual("owner", u.Assignment);
            Assert.AreEqual("{\"de\":\"Freigabe\",\"fr\":\"Approbation\"}", u.Title,
                "the per-culture record stays raw - it is translated when displayed.");
            Assert.AreEqual(48, u.DueInHours);
            Assert.AreEqual(ActivityScopeMode.Replace, u.ScopeMode);
            CollectionAssert.AreEquivalent(new[] { "corr" }, u.RetainVariables);
            Assert.AreEqual("amount", u.Inputs.Single().Parameter);
            Assert.AreEqual("approved", u.Outputs.Single().Variable);
            Assert.AreEqual(2, u.FormFields.Count);
            UserTaskField decision = u.FormFields.Single(f => f.Name == "decision");
            Assert.AreEqual(UserTaskFieldKind.Choice, decision.Kind);
            Assert.IsTrue(decision.Required);
            Assert.AreEqual(2, decision.Choices.Count);
            Assert.AreEqual("Ja", decision.Choices.Single(c => c.Value == "yes").Label);
            Assert.IsTrue(u.FormFields.Single(f => f.Name == "amount").ReadOnly);

            var a = (AutomatedActivityNode)copy.GetNode("a");
            Assert.AreEqual("backend", a.ExecutionTarget);
            Assert.AreEqual(ActivityScopeMode.Replace, a.ScopeMode);
            Assert.AreEqual("a->err", a.ErrorFlowId);
            Assert.AreEqual("tries", a.AttemptVariable);
            Assert.IsInstanceOfType<int>(a.Configuration["retries"], "the object config value round-trips as int, not JsonElement.");
            Assert.AreEqual(3, a.Configuration["retries"]);
            CollectionAssert.AreEquivalent(new[] { "tenant" }, a.RetainVariables);
            Assert.AreEqual(2, a.Inputs.Count);
            Assert.IsInstanceOfType<int>(a.Inputs.Single(i => i.Parameter == "y").Literal, "the literal object round-trips as int.");
            Assert.AreEqual("total", a.Outputs.Single().Variable);
            Assert.AreEqual(10, a.Diagram.X, "diagram coordinates survive.");

            var c = (CallWorkflowNode)copy.GetNode("c");
            Assert.AreEqual("sub", c.SubDefinitionId);
            Assert.AreEqual(2, c.SubDefinitionVersion);
            Assert.AreEqual("n", c.Inputs.Single().Parameter);
            Assert.AreEqual("answer", c.Outputs.Single().Variable);

            // Signatur und Ergebnis ueberleben den Round-Trip.
            var s = (StartNode)copy.GetNode("s");
            Assert.AreEqual(ActivityScopeMode.Replace, s.ScopeMode);
            CollectionAssert.AreEquivalent(new[] { "corr" }, s.RetainVariables);
            Assert.IsInstanceOfType<int>(s.Inputs.Single().Literal, "the start parameter literal round-trips as int.");

            // Die Start-Maske ueberlebt den Round-Trip - sie ist Teil der Definition, nicht der Oberflaeche.
            Assert.AreEqual("{\"de\":\"Neuen Fall eroeffnen\",\"fr\":\"Ouvrir un dossier\"}", s.FormDescription,
                "the per-culture record stays raw - it is translated when displayed.");
            Assert.AreEqual(2, s.FormFields.Count);
            UserTaskField seed = s.FormFields.Single(f => f.Name == "seed");
            Assert.AreEqual(UserTaskFieldKind.Number, seed.Kind);
            Assert.IsTrue(seed.Required);
            Assert.AreEqual("{\"de\":\"Startwert\",\"fr\":\"Valeur initiale\"}", seed.Label);
            UserTaskField mode = s.FormFields.Single(f => f.Name == "mode");
            Assert.AreEqual("How fast?", mode.HelpText);
            Assert.AreEqual(2, mode.Choices.Count);
            Assert.AreEqual("Schnell", mode.Choices.Single(ch => ch.Value == "fast").Label);

            var e = (EndNode)copy.GetNode("e");
            Assert.AreEqual("total", e.Outputs.Single().Parameter);
            Assert.AreEqual("result", e.Outputs.Single().Variable);
            CollectionAssert.AreEquivalent(new[] { "corr" }, e.RetainVariables);

            Assert.AreEqual("n > 1", copy.Flows.Single(f => f.Id == "x->e").Condition);

            // Das Ergebnis-Mapping eines Joins ueberlebt den Round-Trip.
            var p = (ParallelGatewayNode)copy.GetNode("p");
            Assert.AreEqual(ActivityScopeMode.Replace, p.ScopeMode);
            Assert.AreEqual("resultA", p.Outputs.Single().Variable);
            CollectionAssert.AreEquivalent(new[] { "corr" }, p.RetainVariables);

            // Das Mapping der Kante ueberlebt den Round-Trip.
            SequenceFlow mapped = copy.Flows.Single(f => f.Id == "c->x");
            Assert.AreEqual(ActivityScopeMode.Replace, mapped.ScopeMode);
            Assert.AreEqual("answer", mapped.Inputs.Single().Source);
            Assert.AreEqual(ParameterBindingKind.Variable, mapped.Inputs.Single().Kind);
            CollectionAssert.AreEquivalent(new[] { "corr" }, mapped.RetainVariables);
        }

        [TestMethod]
        public void ExportJson_UsesStableKindDiscriminators_NotDotNetTypeNames()
        {
            string json = WorkflowJson.ExportDefinition(RichDefinition());

            StringAssert.Contains(json, "\"kind\": \"activity\"");
            StringAssert.Contains(json, "\"kind\": \"call\"");
            Assert.IsFalse(json.Contains("AutomatedActivityNode"),
                "the portable format must not embed .NET type names (rename-safe / cross-system).");
        }

        [TestMethod]
        public void ImportDefinition_EmptyOrBlank_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => WorkflowJson.ImportDefinition("  "));
        }
    }
}
