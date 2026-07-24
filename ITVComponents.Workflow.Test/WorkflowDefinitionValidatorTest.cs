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
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "s" });
            def.Nodes.Add(new ExclusiveGatewayNode { Id = "x" });
            def.Nodes.Add(new EndNode { Id = "e1" });
            def.Nodes.Add(new EndNode { Id = "e2" });
            def.Flows.Add(new SequenceFlow { Id = "f0", SourceId = "s", TargetId = "x" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "x", TargetId = "e1", Condition = "a > 1" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "x", TargetId = "e2", Condition = "a <= 1" });

            var issues = WorkflowDefinitionValidator.Validate(def);
            Assert.IsFalse(HasError(issues), "a gateway without default is a warning, not an error");
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning && i.NodeId == "x"));
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
    }
}
