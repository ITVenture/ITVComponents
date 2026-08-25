using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Tests der statischen Definition-Pruefung <see cref="WorkflowDefinitionValidator"/>.
    /// </summary>
    [TestClass]
    public class WorkflowDefinitionValidatorTest
    {
        private static WorkflowDefinition Linear()
        {
            var def = new WorkflowDefinition { Id = "wf", Version = 1 };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "a", ActivityRef = "doit" });
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "s", TargetId = "a" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "a", TargetId = "e" });
            return def;
        }

        private static bool HasError(IEnumerable<ValidationIssue> issues)
            => issues.Any(i => i.Severity == ValidationSeverity.Error);

        [TestMethod]
        public void ValidLinearDefinition_HasNoErrors()
        {
            var issues = WorkflowDefinitionValidator.Validate(Linear());
            Assert.IsFalse(HasError(issues), "a well-formed linear definition should have no errors");
        }

        [TestMethod]
        public void MissingStart_IsError()
        {
            var def = Linear();
            def.Nodes.RemoveAll(n => n.Kind == NodeKind.Start);
            def.Flows.RemoveAll(f => f.SourceId == "s");
            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("start")));
        }

        [TestMethod]
        public void ActivityWithoutReference_IsError()
        {
            var def = Linear();
            ((AutomatedActivityNode)def.Nodes.Single(n => n.Id == "a")).ActivityRef = "";
            Assert.IsTrue(HasError(WorkflowDefinitionValidator.Validate(def)));
        }

        [TestMethod]
        public void DanglingConnection_IsError()
        {
            var def = Linear();
            def.Flows.Add(new SequenceFlow { Id = "bad", SourceId = "a", TargetId = "ghost" });
            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "bad"));
        }

        [TestMethod]
        public void NodeWithoutOutgoing_IsError()
        {
            var def = Linear();
            def.Flows.RemoveAll(f => f.SourceId == "a"); // activity has no way out
            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "a"));
        }

        [TestMethod]
        public void DuplicateNodeId_IsError()
        {
            var def = Linear();
            def.Nodes.Add(new EndNode { Id = "a" });
            Assert.IsTrue(HasError(WorkflowDefinitionValidator.Validate(def)));
        }

        [TestMethod]
        public void ExclusiveGatewayWithoutDefault_IsWarningNotError()
        {
            // Beide Zweige laufen auf DAS EINE Ende - eine Definition hat genau einen End-Knoten.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new ExclusiveGatewayNode { Id = "x" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "a1", ActivityRef = "big" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "a2", ActivityRef = "small" });
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "f0", SourceId = "s", TargetId = "x" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "x", TargetId = "a1", Condition = "a > 1" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "x", TargetId = "a2", Condition = "a <= 1" });
            def.Flows.Add(new SequenceFlow { Id = "f3", SourceId = "a1", TargetId = "e" });
            def.Flows.Add(new SequenceFlow { Id = "f4", SourceId = "a2", TargetId = "e" });

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(HasError(issues), "a gateway without default is a warning, not an error");
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning && i.NodeId == "x"));
        }

        [TestMethod]
        public void ConsolidationInsideParallelRegion_IsBranchLocal_NoWarning()
        {
            // Replace-Knoten liegt auf einem Zweig zwischen AND-Split und Join. Seit den Zweig-Scopes
            // raeumt er nur die Kopie SEINES Zweigs ab - kein Grund mehr zu warnen.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new ParallelGatewayNode { Id = "split" });
            def.Nodes.Add(new AutomatedActivityNode
                { Id = "cons", ActivityRef = "x", ScopeMode = ActivityScopeMode.Replace });
            def.Nodes.Add(new AutomatedActivityNode { Id = "b", ActivityRef = "y" });
            def.Nodes.Add(new ParallelGatewayNode { Id = "join" });
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "f0", SourceId = "s", TargetId = "split" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "split", TargetId = "cons" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "split", TargetId = "b" });
            def.Flows.Add(new SequenceFlow { Id = "f3", SourceId = "cons", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "f4", SourceId = "b", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "f5", SourceId = "join", TargetId = "e" });

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(HasError(issues), "the structure itself is valid.");
            Assert.IsFalse(issues.Any(i => i.NodeId == "cons"),
                "a consolidation inside a parallel region only affects its own branch scope.");
        }

        [TestMethod]
        public void ConsolidationAfterJoin_NoWarning()
        {
            // Replace-Knoten liegt NACH dem Join -> Ein-Zweig-Segment, keine Warnung.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new ParallelGatewayNode { Id = "split" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "a", ActivityRef = "x" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "b", ActivityRef = "y" });
            def.Nodes.Add(new ParallelGatewayNode { Id = "join" });
            def.Nodes.Add(new AutomatedActivityNode
                { Id = "cons", ActivityRef = "z", ScopeMode = ActivityScopeMode.Replace });
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "f0", SourceId = "s", TargetId = "split" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "split", TargetId = "a" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "split", TargetId = "b" });
            def.Flows.Add(new SequenceFlow { Id = "f3", SourceId = "a", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "f4", SourceId = "b", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "f5", SourceId = "join", TargetId = "cons" });
            def.Flows.Add(new SequenceFlow { Id = "f6", SourceId = "cons", TargetId = "e" });

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(issues.Any(i => i.NodeId == "cons" && i.Message.Contains("parallel region")),
                "a consolidation node after the join must not be flagged.");
        }

        /// <summary>
        /// Start -&gt; AND-Split -&gt; (Zweig A: Aktivitaet "a" | Zweig B: Aktivitaet "b") -&gt; Join -&gt; End.
        /// Die Output-Bindungen der beiden Aktivitaeten werden per Callback gesetzt.
        /// </summary>
        private static WorkflowDefinition ParallelWithOutputs(
            System.Action<AutomatedActivityNode> a, System.Action<AutomatedActivityNode> b)
        {
            var na = new AutomatedActivityNode { Id = "a", ActivityRef = "x" };
            var nb = new AutomatedActivityNode { Id = "b", ActivityRef = "y" };
            a(na);
            b(nb);
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new ParallelGatewayNode { Id = "split" });
            def.Nodes.Add(na);
            def.Nodes.Add(nb);
            def.Nodes.Add(new ParallelGatewayNode { Id = "join" });
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "f0", SourceId = "s", TargetId = "split" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "split", TargetId = "a" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "split", TargetId = "b" });
            def.Flows.Add(new SequenceFlow { Id = "f3", SourceId = "a", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "f4", SourceId = "b", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "f5", SourceId = "join", TargetId = "e" });
            return def;
        }

        [TestMethod]
        public void ParallelBranchesWritingSameVariable_IsWarning()
        {
            WorkflowDefinition def = ParallelWithOutputs(
                a => a.Outputs.Add(new ActivityOutputBinding { Parameter = "r", Variable = "shared" }),
                b => b.Outputs.Add(new ActivityOutputBinding { Parameter = "r", Variable = "shared" }));

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(HasError(issues), "the structure is valid - only a warning is expected.");
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning
                                          && i.Message.Contains("shared") && i.Message.Contains("parallel branches")),
                "two parallel branches writing 'shared' must be flagged.");
        }

        [TestMethod]
        public void ParallelBranchesWritingDifferentVariables_NoWarning()
        {
            WorkflowDefinition def = ParallelWithOutputs(
                a => a.Outputs.Add(new ActivityOutputBinding { Parameter = "r", Variable = "va" }),
                b => b.Outputs.Add(new ActivityOutputBinding { Parameter = "r", Variable = "vb" }));

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(issues.Any(i => i.Message.Contains("parallel branches")),
                "different target variables must not be flagged.");
        }

        [TestMethod]
        public void SameVariableTwiceOnSameBranch_NoWarning()
        {
            // Beide Schreibzugriffe liegen sequenziell auf DEMSELBEN Zweig -> kein paralleler Konflikt.
            var a1 = new AutomatedActivityNode { Id = "a1", ActivityRef = "x" };
            a1.Outputs.Add(new ActivityOutputBinding { Parameter = "r", Variable = "v" });
            var a2 = new AutomatedActivityNode { Id = "a2", ActivityRef = "x" };
            a2.Outputs.Add(new ActivityOutputBinding { Parameter = "r", Variable = "v" });
            var b = new AutomatedActivityNode { Id = "b", ActivityRef = "y" };

            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new ParallelGatewayNode { Id = "split" });
            def.Nodes.Add(a1);
            def.Nodes.Add(a2);
            def.Nodes.Add(b);
            def.Nodes.Add(new ParallelGatewayNode { Id = "join" });
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "f0", SourceId = "s", TargetId = "split" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "split", TargetId = "a1" });
            def.Flows.Add(new SequenceFlow { Id = "f1b", SourceId = "a1", TargetId = "a2" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "split", TargetId = "b" });
            def.Flows.Add(new SequenceFlow { Id = "f3", SourceId = "a2", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "f4", SourceId = "b", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "f5", SourceId = "join", TargetId = "e" });

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(issues.Any(i => i.Message.Contains("parallel branches")),
                "two sequential writes on the same branch are legitimate - no warning.");
        }

        [TestMethod]
        public void ErrorFlow_ValidWithOneSuccessAndOneErrorEdge_NoError()
        {
            var def = Linear(); // s -> a -> e
            def.Nodes.Add(new AutomatedActivityNode { Id = "handled", ActivityRef = "cleanup" });
            def.Flows.Add(new SequenceFlow { Id = "a->handled", SourceId = "a", TargetId = "handled" });
            def.Flows.Add(new SequenceFlow { Id = "handled->e", SourceId = "handled", TargetId = "e" });
            ((AutomatedActivityNode)def.Nodes.Single(n => n.Id == "a")).ErrorFlowId = "a->handled";

            Assert.IsFalse(HasError(WorkflowDefinitionValidator.Validate(def)),
                "one success + one error outgoing is a valid error-flow setup.");
        }

        [TestMethod]
        public void ErrorFlow_NotAnOutgoingEdge_IsError()
        {
            var def = Linear();
            ((AutomatedActivityNode)def.Nodes.Single(n => n.Id == "a")).ErrorFlowId = "does-not-exist";
            Assert.IsTrue(HasError(WorkflowDefinitionValidator.Validate(def)));
        }

        [TestMethod]
        public void ErrorFlow_WithoutASeparateSuccessEdge_IsError()
        {
            // Nur die Fehler-Kante ausgehend -> es fehlt der Erfolgs-Ausgang.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "a", ActivityRef = "x", ErrorFlowId = "a->h" });
            def.Nodes.Add(new EndNode { Id = "h" });
            def.Flows.Add(new SequenceFlow { Id = "s->a", SourceId = "s", TargetId = "a" });
            def.Flows.Add(new SequenceFlow { Id = "a->h", SourceId = "a", TargetId = "h" });

            Assert.IsTrue(HasError(WorkflowDefinitionValidator.Validate(def)),
                "an activity with an error flow must also have exactly one success flow.");
        }

        [TestMethod]
        public void CallWorkflowErrorFlow_WithoutASeparateSuccessEdge_IsError()
        {
            // Der Fehler-Ausgang gilt generalisiert auch fuer CallWorkflowNode.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new CallWorkflowNode { Id = "c", SubDefinitionId = "sub", ErrorFlowId = "c->h" });
            def.Nodes.Add(new EndNode { Id = "h" });
            def.Flows.Add(new SequenceFlow { Id = "s->c", SourceId = "s", TargetId = "c" });
            def.Flows.Add(new SequenceFlow { Id = "c->h", SourceId = "c", TargetId = "h" });

            Assert.IsTrue(HasError(WorkflowDefinitionValidator.Validate(def)),
                "a call node with an error flow must also have exactly one success flow.");
        }

        [TestMethod]
        public void StartParameters_OnTwoStartNodes_AreError()
        {
            // Die Signatur gehoert der Definition - zweimal deklariert waere sie mehrdeutig.
            var def = new WorkflowDefinition { Id = "wf" };
            var s1 = new StartNode { Id = "s1" };
            s1.Inputs.Add(new ActivityInputBinding { Parameter = "a", Kind = ParameterBindingKind.Literal, Literal = 1 });
            var s2 = new StartNode { Id = "s2" };
            s2.Inputs.Add(new ActivityInputBinding { Parameter = "b", Kind = ParameterBindingKind.Literal, Literal = 2 });
            def.Nodes.Add(s1);
            def.Nodes.Add(s2);
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "s1->e", SourceId = "s1", TargetId = "e" });
            def.Flows.Add(new SequenceFlow { Id = "s2->e", SourceId = "s2", TargetId = "e" });

            Assert.IsTrue(WorkflowDefinitionValidator.Validate(def)
                    .Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("signature")),
                "start parameters on two start nodes must be reported as an ambiguous signature.");
        }

        [TestMethod]
        public void Result_OnTwoEndNodes_IsError()
        {
            var def = new WorkflowDefinition { Id = "wf" };
            var e1 = new EndNode { Id = "e1" };
            e1.Outputs.Add(new ActivityOutputBinding { Parameter = "total", Variable = "result" });
            var e2 = new EndNode { Id = "e2" };
            e2.Outputs.Add(new ActivityOutputBinding { Parameter = "total", Variable = "other" });
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new ExclusiveGatewayNode { Id = "x", DefaultFlowId = "x->e1" });
            def.Nodes.Add(e1);
            def.Nodes.Add(e2);
            def.Flows.Add(new SequenceFlow { Id = "s->x", SourceId = "s", TargetId = "x" });
            def.Flows.Add(new SequenceFlow { Id = "x->e1", SourceId = "x", TargetId = "e1" });
            def.Flows.Add(new SequenceFlow { Id = "x->e2", SourceId = "x", TargetId = "e2" });

            Assert.IsTrue(WorkflowDefinitionValidator.Validate(def)
                    .Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("result")),
                "the result must not depend on which end node is reached.");
        }

        [TestMethod]
        public void MissingEnd_IsWarning()
        {
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new WaitNode { Id = "w", SignalName = "go" });
            def.Flows.Add(new SequenceFlow { Id = "f", SourceId = "s", TargetId = "w" });
            // w has no outgoing -> that's an error; but the missing-end warning must also be present.
            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning && (i.Message.Contains("end") || i.Message.Contains("End"))));
        }

        // --- Genau ein Start / ein Ende (7) ---------------------------------------------------------

        [TestMethod]
        public void TwoStartNodes_AreError()
        {
            // Mehrere Start-Knoten waren ein impliziter Parallelstart - das leistet ein AND-Split hinter
            // dem einen Start, und die Signatur der Definition bleibt eindeutig.
            var def = Linear();
            def.Nodes.Add(new StartNode { Id = "s2" });
            def.Flows.Add(new SequenceFlow { Id = "s2->a", SourceId = "s2", TargetId = "a" });

            Assert.IsTrue(WorkflowDefinitionValidator.Validate(def)
                .Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("one start node")));
        }

        [TestMethod]
        public void TwoEndNodes_AreError()
        {
            var def = Linear();
            def.Nodes.Add(new EndNode { Id = "e2" });
            def.Flows.Add(new SequenceFlow { Id = "a->e2", SourceId = "a", TargetId = "e2" });

            Assert.IsTrue(WorkflowDefinitionValidator.Validate(def)
                .Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("one end node")));
        }

        // --- Mapping auf der Verbindung (8) ---------------------------------------------------------

        [TestMethod]
        public void FlowMapping_WithoutVariableName_IsWarning()
        {
            var def = Linear();
            def.Flows.Single(f => f.Id == "f2").Inputs.Add(
                new ActivityInputBinding { Kind = ParameterBindingKind.Literal, Literal = 1 });

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(HasError(issues), "a half-typed mapping row must not block saving.");
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning && i.NodeId == "f2"));
        }

        [TestMethod]
        public void ConsolidatingFlowInsideParallelRegion_IsBranchLocal_NoWarning()
        {
            // Seit den Zweig-Scopes raeumt eine konsolidierende Kante nur die Kopie IHRES Zweigs ab -
            // die Geschwister merken davon nichts. Frueher war das eine Warnung, jetzt zulaessig.
            var def = ParallelSkeleton();
            def.Flows.Single(f => f.Id == "split->a").ScopeMode = ActivityScopeMode.Replace;

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(issues.Any(i => i.NodeId == "split->a"),
                "a consolidating connection inside a parallel region only affects its own branch scope.");
        }

        [TestMethod]
        public void EndNodeInsideParallelRegion_IsWarning()
        {
            // Ein Zweig, der ins Ende laeuft statt in seinen Join: sein Zweig-Scope geht verloren und die
            // Geschwister warten ewig.
            var def = ParallelSkeleton();
            def.Flows.Add(new SequenceFlow { Id = "split->e", SourceId = "split", TargetId = "e" });

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning && i.NodeId == "e"
                                          && i.Message.Contains("parallel region")),
                "an end node inside a parallel region drops the branch and deadlocks the join.");
        }

        [TestMethod]
        public void FlowMappings_WritingTheSameVariableOnTwoBranches_IsWarning()
        {
            // Die Kante ist seit dem Kanten-Mapping eine Schreibstelle wie ein Knoten - der parallele
            // Schreibkonflikt muss deshalb auch ueber Kanten erkannt werden (er faultet zur Laufzeit).
            var def = ParallelSkeleton();
            def.Flows.Single(f => f.Id == "split->a").Inputs.Add(
                new ActivityInputBinding { Parameter = "shared", Kind = ParameterBindingKind.Literal, Literal = 1 });
            def.Flows.Single(f => f.Id == "split->b").Inputs.Add(
                new ActivityInputBinding { Parameter = "shared", Kind = ParameterBindingKind.Literal, Literal = 2 });

            Assert.IsTrue(WorkflowDefinitionValidator.Validate(def)
                .Any(i => i.Severity == ValidationSeverity.Warning && i.Message.Contains("parallel branches")));
        }

        // --- Benutzer-Aufgabe ----------------------------------------------------------------------

        [TestMethod]
        public void UserTaskWithoutTaskKey_IsError()
        {
            // Ohne Aufgabenart taucht die Aufgabe in keiner Arbeitsliste auf - der Prozess bliebe fuer
            // immer stehen, ohne dass jemand sieht, worauf.
            Assert.IsTrue(WorkflowDefinitionValidator.Validate(WithUserTask(t => t.TaskKey = ""))
                .Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "u"
                          && i.Message.Contains("task key")));
        }

        [TestMethod]
        public void ValidUserTask_HasNoErrors()
        {
            Assert.IsFalse(HasError(WorkflowDefinitionValidator.Validate(WithUserTask(t => { }))));
        }

        [TestMethod]
        public void UserTaskWithTruncatedCultureJsonTitle_IsError()
        {
            // Der fiese Fall: die Laufzeit verlangt beide Klammern und laesst einen abgeschnittenen
            // Datensatz kommentarlos als Klartext durch - der Benutzer saehe '{"de":"Freigabe'.
            Assert.IsTrue(WorkflowDefinitionValidator
                .Validate(WithUserTask(t => t.Title = "{\"de\":\"Freigabe\""))
                .Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("per-culture")));
        }

        [TestMethod]
        public void UserTaskWithBrokenCultureJsonTitle_IsError()
        {
            // Sieht aus wie ein Kultur-Datensatz, ist aber keiner: die Oberflaeche wuerde das JSON
            // woertlich anzeigen. Besser rot beim Speichern.
            Assert.IsTrue(WorkflowDefinitionValidator
                .Validate(WithUserTask(t => t.Title = "{\"de\" \"Freigabe\"}"))
                .Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("per-culture")));
        }

        [TestMethod]
        public void UserTaskWithValidCultureJsonTitle_IsAccepted()
        {
            Assert.IsFalse(HasError(WorkflowDefinitionValidator
                .Validate(WithUserTask(t => t.Title = "{\"de\":\"Freigabe\",\"fr\":\"Approbation\"}"))));
        }

        [TestMethod]
        public void UserTaskWithPlainTextTitle_IsAccepted()
        {
            Assert.IsFalse(HasError(WorkflowDefinitionValidator
                .Validate(WithUserTask(t => t.Title = "Rechnung freigeben"))));
        }

        [TestMethod]
        public void UserTaskWithDuplicateFormField_IsError()
        {
            Assert.IsTrue(WorkflowDefinitionValidator.Validate(WithUserTask(t =>
                {
                    t.FormFields.Add(new UserTaskField { Name = "comment" });
                    t.FormFields.Add(new UserTaskField { Name = "Comment" });
                }))
                .Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("more than once")));
        }

        [TestMethod]
        public void UserTaskWithTwoOutgoingFlows_IsError()
        {
            // Die Engine faultet hier zur Laufzeit - der Editor soll es vorher sagen.
            WorkflowDefinition def = WithUserTask(t => { });
            def.Nodes.Add(new EndNode { Id = "e2" });
            def.Flows.Add(new SequenceFlow { Id = "u->e2", SourceId = "u", TargetId = "e2" });

            Assert.IsTrue(WorkflowDefinitionValidator.Validate(def)
                .Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "u"
                          && i.Message.Contains("outgoing connections")));
        }

        // --- Start-Maske (StartNode.FormFields) ---------------------------------------------------

        [TestMethod]
        public void StartFormWithDuplicateField_IsError()
        {
            Assert.IsTrue(WorkflowDefinitionValidator.Validate(WithStartForm(s =>
                {
                    s.FormFields.Add(new UserTaskField { Name = "amount" });
                    s.FormFields.Add(new UserTaskField { Name = "Amount" });
                }))
                .Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "s"
                          && i.Message.Contains("more than once")));
        }

        [TestMethod]
        public void StartFormFieldNotInStrictSignature_IsWarning()
        {
            // Strikte Signatur: was nicht deklariert ist, wird eingegeben und sofort verworfen.
            var issues = WorkflowDefinitionValidator.Validate(WithStartForm(s =>
            {
                s.ScopeMode = ActivityScopeMode.Replace;
                s.Inputs.Add(new ActivityInputBinding
                    { Parameter = "amount", Kind = ParameterBindingKind.Variable, Source = "amount" });
                s.FormFields.Add(new UserTaskField { Name = "amount" });
                s.FormFields.Add(new UserTaskField { Name = "comment" });
            }));

            Assert.IsFalse(HasError(issues), "a dropped field is a warning, not an error - it may be intended.");
            Assert.IsTrue(issues.Any(i => i.NodeId == "s" && i.Message.Contains("'comment'")
                                          && i.Message.Contains("dropped")));
            Assert.IsFalse(issues.Any(i => i.Message.Contains("'amount'") && i.Message.Contains("dropped")),
                "the declared field survives and must not be reported.");
        }

        [TestMethod]
        public void StartFormFieldsWithExtendScope_AreNotReported()
        {
            // Ohne strikte Signatur bleibt alles Uebergebene stehen - es gibt nichts zu warnen.
            var issues = WorkflowDefinitionValidator.Validate(WithStartForm(s =>
            {
                s.Inputs.Add(new ActivityInputBinding
                    { Parameter = "amount", Kind = ParameterBindingKind.Variable, Source = "amount" });
                s.FormFields.Add(new UserTaskField { Name = "comment" });
            }));

            Assert.IsFalse(issues.Any(i => i.Message.Contains("dropped")));
        }

        [TestMethod]
        public void StartFormReadOnlyField_IsWarning()
        {
            // ReadOnly zeigt einen Payload an - beim Start gibt es keinen, das Feld erschiene gar nicht.
            Assert.IsTrue(WorkflowDefinitionValidator.Validate(WithStartForm(s =>
                    s.FormFields.Add(new UserTaskField { Name = "info", ReadOnly = true })))
                .Any(i => i.Severity == ValidationSeverity.Warning && i.NodeId == "s"
                          && i.Message.Contains("read-only")));
        }

        [TestMethod]
        public void StartFormWithBrokenCultureJson_IsReported()
        {
            Assert.IsTrue(WorkflowDefinitionValidator.Validate(WithStartForm(s =>
                    s.FormDescription = "{\"de\":\"Neuer Fall\""))
                .Any(i => i.NodeId == "s" && i.Message.Contains("form description")));
        }

        /// <summary>Linear, mit Zugriff auf den Start-Knoten (Signatur + Start-Maske).</summary>
        private static WorkflowDefinition WithStartForm(System.Action<StartNode> configure)
        {
            WorkflowDefinition def = Linear();
            configure(def.Nodes.OfType<StartNode>().Single());
            return def;
        }

        /// <summary>Start → Benutzer-Aufgabe → Ende.</summary>
        private static WorkflowDefinition WithUserTask(System.Action<UserActivityNode> configure)
        {
            var task = new UserActivityNode { Id = "u", TaskKey = "Check" };
            configure(task);
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(task);
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "s->u", SourceId = "s", TargetId = "u" });
            def.Flows.Add(new SequenceFlow { Id = "u->e", SourceId = "u", TargetId = "e" });
            return def;
        }

        // --- Die Aufbewahrungsfristen -----------------------------------------------------------------
        //
        // Alle vier Faelle haben gemeinsam, dass die REGEL sie schweigend richtig behandelt: sie wirft
        // nirgends. Genau deshalb muessen sie hier auffallen - sonst erfaehrt der Autor seinen
        // Tippfehler nie und wundert sich Monate spaeter, warum eine Frist nicht gilt.

        [TestMethod]
        public void ANegativeRetentionPeriod_IsError()
        {
            WorkflowDefinition def = Linear();
            def.RetentionDays = -1;

            var issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error
                                          && i.Message.Contains("negative")),
                "a negative period would put the cut-off in the future - it is discarded, not rounded, "
                + "and that has to be said.");
        }

        [TestMethod]
        public void ZeroDays_IsNoComplaint()
        {
            WorkflowDefinition def = Linear();
            def.RetentionDays = 0;
            def.AttachmentRetentionDays = 0;

            Assert.IsFalse(HasError(WorkflowDefinitionValidator.Validate(def)),
                "zero means 'right after it ends' - the sharpest setting there is, and a valid one.");
        }

        [TestMethod]
        public void ContradictingBounds_AreError()
        {
            WorkflowDefinition def = Linear();
            def.AllowTenantRetentionOverride = true;
            def.MinTenantRetentionDays = 100;
            def.MaxTenantRetentionDays = 10;

            var issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error
                                          && i.Message.Contains("contradict")),
                "neither bound applies then - a tenant's wish stands unchanged, however far outside it "
                + "lies. That is the opposite of what the author meant.");
        }

        [TestMethod]
        public void BoundsWithoutPermission_AreAWarningNotAnError()
        {
            WorkflowDefinition def = Linear();
            def.MinTenantRetentionDays = 30;

            var issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsFalse(HasError(issues), "it is not wrong, only ineffective.");
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning
                                          && i.Message.Contains("nothing to")),
                "the bounds limit an objection nobody may raise - worth saying, not worth refusing.");
        }

        [TestMethod]
        public void ProperBounds_AreNoComplaint()
        {
            WorkflowDefinition def = Linear();
            def.AllowTenantRetentionOverride = true;
            def.RetentionDays = 90;
            def.MinTenantRetentionDays = 30;
            def.MaxTenantRetentionDays = 3650;
            def.AttachmentRetentionDays = 30;
            def.MinTenantAttachmentRetentionDays = 0;

            var issues = WorkflowDefinitionValidator.Validate(def);

            Assert.IsFalse(issues.Any(i => i.Message.Contains("period") || i.Message.Contains("bound")),
                "a sane setup must not produce noise - a validator that cries wolf gets ignored.");
        }

        /// <summary>Start → AND-Split → zwei Aktivitaeten → Join → Ende.</summary>
        private static WorkflowDefinition ParallelSkeleton()
        {
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new ParallelGatewayNode { Id = "split" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "a", ActivityRef = "x" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "b", ActivityRef = "y" });
            def.Nodes.Add(new ParallelGatewayNode { Id = "join" });
            def.Nodes.Add(new EndNode { Id = "e" });
            def.Flows.Add(new SequenceFlow { Id = "s->split", SourceId = "s", TargetId = "split" });
            def.Flows.Add(new SequenceFlow { Id = "split->a", SourceId = "split", TargetId = "a" });
            def.Flows.Add(new SequenceFlow { Id = "split->b", SourceId = "split", TargetId = "b" });
            def.Flows.Add(new SequenceFlow { Id = "a->join", SourceId = "a", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "b->join", SourceId = "b", TargetId = "join" });
            def.Flows.Add(new SequenceFlow { Id = "join->e", SourceId = "join", TargetId = "e" });
            return def;
        }
    }
}
