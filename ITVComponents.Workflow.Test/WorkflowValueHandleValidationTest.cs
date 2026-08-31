using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ITVComponents.Workflow.Test.ValueHandleBindings;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Der Riegel zur <b>Entwurfszeit</b>: hier ist er noch billig. Ein Griff darf nur dort entstehen,
    /// wo er den Schritt nicht ueberlebt - alles andere faellt im Editor auf und nicht erst als
    /// gefaulteter Vorgang.
    /// </summary>
    [TestClass]
    public class WorkflowValueHandleValidationTest
    {
        [TestMethod]
        public void OnAnActivity_AValueHandleIsFine()
        {
            WorkflowDefinition definition = Linear();
            Activity(definition).Inputs.Add(Handle("order", "orders", "o1"));

            Assert.IsFalse(HasError(WorkflowDefinitionValidator.Validate(definition)));
        }

        [TestMethod]
        public void InTheStartSignature_ItIsAnError()
        {
            WorkflowDefinition definition = Linear();
            ((StartNode)definition.Nodes.Single(n => n.Id == "s")).Inputs
                .Add(Handle("order", "orders", "o1"));

            Assert.IsTrue(Errors(definition).Any(m => m.Contains("instance variables")));
        }

        [TestMethod]
        public void OnAConnectionMapping_ItIsAnError()
        {
            WorkflowDefinition definition = Linear();
            definition.Flows.Single(f => f.Id == "f1").Inputs.Add(Handle("order", "orders", "o1"));

            Assert.IsTrue(Errors(definition).Any(m => m.Contains("branch scope")));
        }

        [TestMethod]
        public void OnASendNode_ItIsAnError()
        {
            WorkflowDefinition definition = Linear();
            var send = new SendMessageNode { Id = "m", SignalName = "go" };
            send.Inputs.Add(Handle("order", "orders", "o1"));
            definition.Nodes.Add(send);

            Assert.IsTrue(Errors(definition).Any(m => m.Contains("outbox")),
                "a message payload is stored and handed to the receiver - a handle there is a dead " +
                "reference by the time it arrives.");
        }

        [TestMethod]
        public void OnACallNode_ItIsAnError()
        {
            WorkflowDefinition definition = Linear();
            var call = new CallWorkflowNode { Id = "c" };
            call.Inputs.Add(Handle("order", "orders", "o1"));
            definition.Nodes.Add(call);

            Assert.IsTrue(Errors(definition).Any(m => m.Contains("child instance")));
        }

        [TestMethod]
        public void WithoutAHandlerName_ItIsAnError()
        {
            WorkflowDefinition definition = Linear();
            ActivityInputBinding binding = Handle("order", "orders", "o1");
            binding.HandlerName = null;
            Activity(definition).Inputs.Add(binding);

            Assert.IsTrue(Errors(definition).Any(m => m.Contains("without naming one")));
        }

        [TestMethod]
        public void WithoutArguments_ItIsAnError()
        {
            WorkflowDefinition definition = Linear();
            ActivityInputBinding binding = Handle("order", "orders", "o1");
            binding.HandlerArguments = null;
            Activity(definition).Inputs.Add(binding);

            Assert.IsTrue(Errors(definition).Any(m => m.Contains("which record to return")));
        }

        [TestMethod]
        public void ANestedHandleArgument_IsAnError()
        {
            WorkflowDefinition definition = Linear();
            ActivityInputBinding binding = Handle("order", "orders", "o1");
            binding.HandlerArguments.Add(Handle("nested", "orders", "o2"));
            Activity(definition).Inputs.Add(binding);

            Assert.IsTrue(Errors(definition).Any(m => m.Contains("can not nest")));
        }

        [TestMethod]
        public void TwoBindingsOnTheSameHandler_AreAWarning()
        {
            WorkflowDefinition definition = Linear();
            Activity(definition).Inputs.Add(Handle("a", "orders", "o1"));
            Activity(definition).Inputs.Add(Handle("b", "orders", "o1"));

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(definition);
            Assert.IsFalse(HasError(issues), "it is allowed - it just yields two handles.");
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning
                                          && i.Message.Contains("more than once")));
        }

        [TestMethod]
        public void OnAUserTask_HandleDeliveryIsAWarning()
        {
            WorkflowDefinition definition = Linear();
            var task = new UserActivityNode { Id = "u", TaskKey = "Edit" };
            task.Inputs.Add(Handle("order", "orders", "o1"));
            definition.Nodes.Add(task);

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(definition);
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning
                                          && i.Message.Contains("never sees one")));
        }

        [TestMethod]
        public void WritingBackInsideAParallelRegion_IsAnError_UnlessAllowed()
        {
            WorkflowDefinition definition = Parallel(
                Handle("order", "orders", "o1", ValueDelivery.Value, ValueWriteBackMode.OnSuccess));

            Assert.IsTrue(Errors(definition).Any(m => m.Contains("parallel region")),
                "the branch commit compares values from the variable blob - and there is nothing there " +
                "for this kind of binding.");

            WorkflowDefinition allowed = Parallel(Allowed(
                Handle("order", "orders", "o1", ValueDelivery.Value, ValueWriteBackMode.OnSuccess)));

            IReadOnlyList<ValidationIssue> issues = WorkflowDefinitionValidator.Validate(allowed);
            Assert.IsFalse(HasError(issues), "explicitly allowed turns the error into a warning.");
            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Warning
                                          && i.Message.Contains("handler's responsibility")));
        }

        // --- Hilfsmittel ----------------------------------------------------------------------------

        private static ActivityInputBinding Allowed(ActivityInputBinding binding)
        {
            binding.AllowInParallelRegion = true;
            return binding;
        }

        private static AutomatedActivityNode Activity(WorkflowDefinition definition)
            => (AutomatedActivityNode)definition.Nodes.Single(n => n.Id == "a");

        private static bool HasError(IEnumerable<ValidationIssue> issues)
            => issues.Any(i => i.Severity == ValidationSeverity.Error);

        private static IEnumerable<string> Errors(WorkflowDefinition definition)
            => WorkflowDefinitionValidator.Validate(definition)
                .Where(i => i.Severity == ValidationSeverity.Error).Select(i => i.Message);

        private static WorkflowDefinition Linear()
        {
            var definition = new WorkflowDefinition { TechnicalName = "wf", Version = 1 };
            definition.Nodes.Add(new StartNode { Id = "s" });
            definition.Nodes.Add(new AutomatedActivityNode { Id = "a", ActivityRef = "doit" });
            definition.Nodes.Add(new EndNode { Id = "e" });
            definition.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "s", TargetId = "a" });
            definition.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "a", TargetId = "e" });
            return definition;
        }

        /// <summary>Ein AND-Split mit zwei Zweigen; die Bindung haengt am Knoten des ersten.</summary>
        private static WorkflowDefinition Parallel(ActivityInputBinding binding)
        {
            var definition = new WorkflowDefinition { TechnicalName = "par", Version = 1 };
            var left = new AutomatedActivityNode { Id = "a", ActivityRef = "doit" };
            left.Inputs.Add(binding);
            definition.Nodes.Add(new StartNode { Id = "s" });
            definition.Nodes.Add(new ParallelGatewayNode { Id = "split" });
            definition.Nodes.Add(left);
            definition.Nodes.Add(new AutomatedActivityNode { Id = "b", ActivityRef = "doit" });
            definition.Nodes.Add(new ParallelGatewayNode { Id = "join" });
            definition.Nodes.Add(new EndNode { Id = "e" });
            definition.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "s", TargetId = "split" });
            definition.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "split", TargetId = "a" });
            definition.Flows.Add(new SequenceFlow { Id = "f3", SourceId = "split", TargetId = "b" });
            definition.Flows.Add(new SequenceFlow { Id = "f4", SourceId = "a", TargetId = "join" });
            definition.Flows.Add(new SequenceFlow { Id = "f5", SourceId = "b", TargetId = "join" });
            definition.Flows.Add(new SequenceFlow { Id = "f6", SourceId = "join", TargetId = "e" });
            return definition;
        }
    }
}
