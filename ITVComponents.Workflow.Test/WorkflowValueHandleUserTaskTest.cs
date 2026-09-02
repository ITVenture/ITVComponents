using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using ITVComponents.Workflow.ValueHandles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ITVComponents.Workflow.Test.ValueHandleBindings;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft den Griff an der <b>Benutzer-Aufgabe</b>: die Maske sieht nie den Griff, sondern den
    /// ausgepackten Wert; die Feld-Pfade sind Lese- <b>und</b> Schreibziel; geschrieben wird beim
    /// Abschluss, genau einmal, und nur was der Knoten dafuer benennt.
    /// </summary>
    [TestClass]
    public class WorkflowValueHandleUserTaskTest
    {
        private InMemoryWorkflowStore store;
        private RecordingValueHandler handler;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            handler = new RecordingValueHandler();
            engine = new WorkflowEngine(store,
                new ActivityRegistry().RegisterValueHandler("orders", handler));
        }

        [TestMethod]
        public void TheMask_SeesTheRecord_AndTheFieldPathsAreResolvedForIt()
        {
            handler.With("o1", NewOrder());
            store.SaveDefinition(TaskWithPaths("show", writeBack: false));

            WorkflowInstance instance = engine.StartWorkflow("show");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            UserTaskDescriptor descriptor = engine.DescribeUserTask(instance.Id, token.Id);

            Assert.IsInstanceOfType(descriptor.Payload["customer"], typeof(Order),
                "a task must never see the handle - it would not survive a reconnect anyway.");
            Assert.AreEqual("Old Street", descriptor.Payload["customer.Ship.Street"],
                "what a field addresses by path must stand in the payload under that path.");
            Assert.AreEqual("Bern", descriptor.Payload["customer.Ship.City"]);
        }

        [TestMethod]
        public void TheArguments_AreFrozenWhenTheTaskIsParked()
        {
            handler.With("o1", NewOrder());
            store.SaveDefinition(TaskWithPaths("freeze", writeBack: true));

            WorkflowInstance instance = engine.StartWorkflow("freeze");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            Assert.IsNotNull(token.TaskValueHandleArguments);
            Assert.IsTrue(token.TaskValueHandleArguments.ContainsKey("customer"));
            StringAssert.Contains(token.TaskValueHandleArguments["customer"], "o1",
                "the arguments must be pinned at park time - otherwise the completion could address a " +
                "different record than the task showed.");
        }

        [TestMethod]
        public void Completing_WritesTheFieldPaths_AndLeavesTheRestAlone()
        {
            handler.With("o1", NewOrder());
            store.SaveDefinition(TaskWithPaths("write", writeBack: true));

            WorkflowInstance instance = engine.StartWorkflow("write");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            UserTaskCompletionResult result = engine.CompleteUserTask(instance.Id, token.Id,
                new Dictionary<string, object> { { "street", "New Street" } });

            Assert.AreEqual(UserTaskCompletionStatus.Completed, result.Status);
            CollectionAssert.AreEqual(new[] { "o1" }, handler.Writes.ToArray(),
                "one WriteBack per handle - not one per field.");

            var written = (Order)handler.Current("o1");
            Assert.AreEqual("New Street", written.Ship.Street);
            Assert.AreEqual("Bern", written.Ship.City,
                "a field the mask did not carry must survive untouched.");
            Assert.AreEqual("A", written.Customer);
        }

        [TestMethod]
        public void WithoutWriteBackParameters_NothingIsWritten()
        {
            handler.With("o1", NewOrder());
            store.SaveDefinition(TaskWithPaths("readonly", writeBack: false));

            WorkflowInstance instance = engine.StartWorkflow("readonly");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            engine.CompleteUserTask(instance.Id, token.Id,
                new Dictionary<string, object> { { "street", "New Street" } });

            Assert.AreEqual(0, handler.Writes.Count,
                "the node says which parameters go back - saying nothing means nothing goes back.");
            Assert.AreEqual("Old Street", ((Order)handler.Current("o1")).Ship.Street);
        }

        [TestMethod]
        public void AReadOnlyField_IsNotWrittenBack()
        {
            handler.With("o1", NewOrder());
            store.SaveDefinition(TaskWithPaths("ro", writeBack: true));

            WorkflowInstance instance = engine.StartWorkflow("ro");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            engine.CompleteUserTask(instance.Id, token.Id, new Dictionary<string, object>
            {
                { "street", "New Street" }, { "city", "Zuerich" }
            });

            Assert.AreEqual("Bern", ((Order)handler.Current("o1")).Ship.City,
                "a read-only field only shows - it must not travel back into foreign data.");
        }

        [TestMethod]
        public void AFailingWrite_LeavesTheTaskOpen()
        {
            handler.With("o1", NewOrder());
            handler.FailOnWrite = true;
            store.SaveDefinition(TaskWithPaths("fail", writeBack: true));

            WorkflowInstance instance = engine.StartWorkflow("fail");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            Assert.ThrowsExactly<InvalidOperationException>(() => engine.CompleteUserTask(instance.Id,
                token.Id, new Dictionary<string, object> { { "street", "New Street" } }));

            WorkflowInstance reloaded = store.GetInstance(instance.Id);
            Token still = reloaded.Tokens.Single(t => t.Id == token.Id);
            Assert.AreEqual(TokenStatus.Waiting, still.Status,
                "nothing was written, so the task stays open - that is the only variant in which nobody " +
                "loses their input.");
            Assert.IsNotNull(still.TaskKey);
        }

        // --- Hilfsmittel ----------------------------------------------------------------------------

        private static Order NewOrder()
            => new Order
            {
                Customer = "A", Amount = 10m,
                Ship = new Address { Street = "Old Street", City = "Bern" }
            };

        // --- Berechnete Felder ------------------------------------------------------------------------

        [TestMethod]
        public void AComputedField_ShowsAValueThatNoPathCouldReach()
        {
            handler.With("o1", NewOrder());
            store.SaveDefinition(TaskWithComputedField("calc", editable: false));

            WorkflowInstance instance = engine.StartWorkflow("calc");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            UserTaskDescriptor descriptor = engine.DescribeUserTask(instance.Id, token.Id);

            Assert.AreEqual("Bern, Old Street", descriptor.Payload["shipLine"],
                "the expression is evaluated against the payload and lands under the key the mask reads.");
        }

        [TestMethod]
        public void AComputedField_MayStillBeEdited_AndItsValueReachesTheVariables()
        {
            handler.With("o1", NewOrder());
            store.SaveDefinition(TaskWithComputedField("calcEdit", editable: true));

            WorkflowInstance instance = engine.StartWorkflow("calcEdit");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            UserTaskCompletionResult result = engine.CompleteUserTask(instance.Id, token.Id,
                new Dictionary<string, object> { { "shipLine", "typed by hand" } });

            Assert.AreEqual(UserTaskCompletionStatus.Completed, result.Status);
            WorkflowInstance done = store.GetInstance(instance.Id);
            Assert.AreEqual("typed by hand", done.Variables["line"],
                "an expression replaces the READING - where the input goes is the output binding's job.");
            Assert.AreEqual(0, handler.Writes.Count,
                "a computed field cannot write back into the record - an expression has no inverse.");
        }

        [TestMethod]
        public void ABrokenExpression_LeavesTheFieldEmpty_ButTheTaskStaysOpenable()
        {
            handler.With("o1", NewOrder());
            WorkflowDefinition definition = TaskWithComputedField("broken", editable: false);
            ((UserActivityNode)definition.Nodes[1]).FormFields[0].PayloadExpression = "customer.NoSuchThing.X";
            store.SaveDefinition(definition);

            WorkflowInstance instance = engine.StartWorkflow("broken");
            Token token = instance.Tokens.Single(t => t.Status == TokenStatus.Waiting);

            UserTaskDescriptor descriptor = engine.DescribeUserTask(instance.Id, token.Id);

            Assert.IsNotNull(descriptor, "a broken field expression must not make the task unopenable.");
            Assert.IsFalse(descriptor.Payload.ContainsKey("shipLine"));
        }

        /// <summary>
        /// Eine Aufgabe mit EINEM berechneten Feld: der Wert entsteht aus zwei Membern des Datensatzes und
        /// steht so an keiner Stelle, die ein Pfad erreichen koennte.
        /// </summary>
        private static WorkflowDefinition TaskWithComputedField(string id, bool editable)
        {
            var node = new UserActivityNode { Id = "n", TaskKey = "ShowOrder" };
            node.Inputs.Add(Handle("customer", "orders", "o1"));
            node.FormFields.Add(new UserTaskField
            {
                Name = "shipLine",
                Kind = UserTaskFieldKind.Text,
                ReadOnly = !editable,
                PayloadExpression =
                    "'System.String'.Format(\"{0}, {1}\", customer.Ship.City, customer.Ship.Street)"
            });
            if (editable)
            {
                node.Outputs.Add(new ActivityOutputBinding { Parameter = "shipLine", Variable = "line" });
            }

            return new WorkflowDefinition
            {
                TechnicalName = id,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    node,
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->n", SourceId = "s", TargetId = "n" },
                    new SequenceFlow { Id = "n->e", SourceId = "n", TargetId = "e" }
                }
            };
        }

        private static WorkflowDefinition TaskWithPaths(string id, bool writeBack)
        {
            var node = new UserActivityNode { Id = "n", TaskKey = "EditOrder" };
            node.Inputs.Add(Handle("customer", "orders", "o1"));
            node.FormFields.Add(new UserTaskField
            {
                Name = "street", Kind = UserTaskFieldKind.Text, PayloadName = "customer.Ship.Street"
            });
            node.FormFields.Add(new UserTaskField
            {
                Name = "city", Kind = UserTaskFieldKind.Text, PayloadName = "customer.Ship.City",
                ReadOnly = true
            });
            if (writeBack)
            {
                node.WriteBackParameters.Add("customer");
            }

            return new WorkflowDefinition
            {
                TechnicalName = id,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    node,
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->n", SourceId = "s", TargetId = "n" },
                    new SequenceFlow { Id = "n->e", SourceId = "n", TargetId = "e" }
                }
            };
        }
    }
}
