using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Serialization;
using ITVComponents.Workflow.Stores;
using ITVComponents.Workflow.ValueHandles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ITVComponents.Workflow.Test.ValueHandleBindings;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft den <b>Griff auf fremde Daten</b> am automatisierten Weg: die Aktivitaet bekommt je Bindung
    /// den Griff oder den ausgepackten Wert, die Engine schreibt zurueck, wenn die Bindung es sagt - und
    /// die Riegel halten den Griff aus allem heraus, was den Schritt ueberlebt.
    /// </summary>
    [TestClass]
    public class WorkflowValueHandleTest
    {
        private InMemoryWorkflowStore store;
        private ActivityRegistry activities;
        private RecordingValueHandler handler;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            handler = new RecordingValueHandler();
            activities = new ActivityRegistry().RegisterValueHandler("orders", handler);
            engine = new WorkflowEngine(store, activities);
        }

        // --- Zustellung -----------------------------------------------------------------------------

        [TestMethod]
        public void Handle_IsHandedToTheActivity_WhichDecidesWhatAndWhen()
        {
            handler.With("o1", new Order { Customer = "A" });
            activities.Register("touch", ctx =>
            {
                var handle = (ValueHandle)ctx.Inputs["order"];
                ((Order)handle.Value).Customer = "B";
                handle.WriteBack();
            });

            store.SaveDefinition(OneActivity("h", "touch",
                n => n.Inputs.Add(Handle("order", "orders", "o1"))));

            WorkflowInstance instance = engine.StartWorkflow("h");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            CollectionAssert.AreEqual(new[] { "o1" }, handler.Writes.ToArray());
            Assert.AreEqual("B", ((Order)handler.Current("o1")).Customer);
        }

        [TestMethod]
        public void Value_IsUnpacked_AndTheActivityNeedNotKnowAnything()
        {
            handler.With("o1", new Order { Customer = "A" });
            object seen = null;
            activities.Register("touch", ctx =>
            {
                seen = ctx.Inputs["order"];
                ((Order)seen).Customer = "B";
            });

            store.SaveDefinition(OneActivity("v", "touch",
                n => n.Inputs.Add(Handle("order", "orders", "o1", ValueDelivery.Value,
                    ValueWriteBackMode.OnSuccess))));

            WorkflowInstance instance = engine.StartWorkflow("v");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.IsInstanceOfType(seen, typeof(Order),
                "with Delivery=Value the activity must see the record, not the handle.");
            CollectionAssert.AreEqual(new[] { "o1" }, handler.Writes.ToArray());
            Assert.AreEqual("B", ((Order)handler.Current("o1")).Customer,
                "the activity mutated the very object that sits in the backing field.");
        }

        [TestMethod]
        public void Value_WithoutWriteBack_IsPureFetching()
        {
            handler.With("o1", new Order { Customer = "A" });
            activities.Register("touch", ctx => ((Order)ctx.Inputs["order"]).Customer = "B");

            store.SaveDefinition(OneActivity("nw", "touch",
                n => n.Inputs.Add(Handle("order", "orders", "o1", ValueDelivery.Value))));

            engine.StartWorkflow("nw");

            Assert.AreEqual(0, handler.Writes.Count, "WriteBack=Never must not write.");
        }

        [TestMethod]
        public void AnActivityThatFails_WritesNothing()
        {
            handler.With("o1", new Order { Customer = "A" });
            activities.Register("boom", ctx =>
            {
                ((Order)ctx.Inputs["order"]).Customer = "B";
                throw new InvalidOperationException("no");
            });

            store.SaveDefinition(OneActivity("f", "boom",
                n => n.Inputs.Add(Handle("order", "orders", "o1", ValueDelivery.Value,
                    ValueWriteBackMode.OnSuccess))));

            WorkflowInstance instance = engine.StartWorkflow("f");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
            Assert.AreEqual(0, handler.Writes.Count,
                "a failed activity must not have its half-finished state written to foreign data.");
        }

        [TestMethod]
        public void AFailingWriteBack_FaultsTheNode_AndDoesNotApplyOutputs()
        {
            handler.With("o1", new Order { Customer = "A" });
            handler.FailOnWrite = true;
            activities.Register("touch", ctx =>
            {
                ((Order)ctx.Inputs["order"]).Customer = "B";
                ctx.Outputs["done"] = true;
            });

            store.SaveDefinition(OneActivity("fw", "touch", n =>
            {
                n.Inputs.Add(Handle("order", "orders", "o1", ValueDelivery.Value,
                    ValueWriteBackMode.OnSuccess));
                n.Outputs.Add(new ActivityOutputBinding { Parameter = "done", Variable = "done" });
            }));

            WorkflowInstance instance = engine.StartWorkflow("fw");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
            Assert.IsFalse(instance.Variables.ContainsKey("done"),
                "nothing was written - the workflow must not carry on as if it had been.");
        }

        [TestMethod]
        public void AFailingRead_FaultsTheNode()
        {
            handler.FailOnRead = true;
            activities.Register("touch", ctx => { });

            store.SaveDefinition(OneActivity("fr", "touch",
                n => n.Inputs.Add(Handle("order", "orders", "o1"))));

            WorkflowInstance instance = engine.StartWorkflow("fr");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
        }

        [TestMethod]
        public void AnUnconfiguredHandlerName_FaultsTheNode()
        {
            activities.Register("touch", ctx => { });

            // 'archive' ist nirgends eingerichtet - der Scope loest den Namen nicht auf.
            store.SaveDefinition(OneActivity("nh", "touch",
                n => n.Inputs.Add(Handle("order", "archive", "o1"))));

            WorkflowInstance instance = engine.StartWorkflow("nh");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status,
                "a handler name that is set up nowhere must say so, not run without the value.");
        }

        [TestMethod]
        public void TheArgumentsReachTheHandler()
        {
            handler.With("o1", new Order());
            ValueHandleRequest seen = null;
            activities.Register("touch", ctx => seen = ((ValueHandle)ctx.Inputs["order"]).Request);

            store.SaveDefinition(OneActivity("arg", "touch",
                n => n.Inputs.Add(Handle("order", "orders", "o1"))));

            WorkflowInstance instance = engine.StartWorkflow("arg");

            Assert.IsNotNull(seen);
            Assert.AreEqual("o1", seen["key"]);
            Assert.AreEqual("orders", seen.HandlerName);
            Assert.AreEqual("order", seen.Parameter);
            Assert.AreEqual("n", seen.NodeId);
            Assert.AreEqual(instance.Id, seen.InstanceId);
        }

        // --- Die Riegel -----------------------------------------------------------------------------

        [TestMethod]
        public void AHandleInTheStartSignature_IsRefusedAtRuntime()
        {
            var definition = new WorkflowDefinition
            {
                TechnicalName = "sig",
                Nodes = new List<WorkflowNode>
                {
                    NewStart("s", Handle("order", "orders", "o1")),
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { Flow("s", "e") }
            };
            store.SaveDefinition(definition);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => engine.StartWorkflow("sig"));

            StringAssert.Contains(ex.Message, "outlives the step",
                "the message must name the reason - the handle would be a dead reference after a restart.");
        }

        [TestMethod]
        public void AHandleAsAnArgumentOfAHandle_IsRefused()
        {
            handler.With("o1", new Order());
            activities.Register("touch", ctx => { });

            ActivityInputBinding binding = Handle("order", "orders", "o1");
            binding.HandlerArguments.Add(Handle("nested", "orders", "o2"));

            store.SaveDefinition(OneActivity("nest", "touch", n => n.Inputs.Add(binding)));

            WorkflowInstance instance = engine.StartWorkflow("nest");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
        }

        [TestMethod]
        public void AHandleThatReachesTheVariables_IsRefusedBySerialization()
        {
            handler.With("o1", new Order());
            ValueHandle captured = null;
            activities.Register("touch", ctx => captured = (ValueHandle)ctx.Inputs["order"]);

            store.SaveDefinition(OneActivity("ser", "touch",
                n => n.Inputs.Add(Handle("order", "orders", "o1"))));
            engine.StartWorkflow("ser");

            Assert.IsNotNull(captured);
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                WorkflowJson.SerializeVariables(new Dictionary<string, object> { { "stash", captured } }));

            StringAssert.Contains(ex.Message, "value handle",
                "the last lock must say what it refused and why.");
        }

        // --- Hilfsmittel ----------------------------------------------------------------------------

        private static StartNode NewStart(string id, params ActivityInputBinding[] inputs)
        {
            var start = new StartNode { Id = id };
            start.Inputs.AddRange(inputs);
            return start;
        }

        private static WorkflowDefinition OneActivity(string id, string activityRef,
            Action<AutomatedActivityNode> configure)
        {
            var node = new AutomatedActivityNode { Id = "n", ActivityRef = activityRef };
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

        private static SequenceFlow Flow(string from, string to)
            => new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };
    }
}
