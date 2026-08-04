using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using ITVComponents.Workflow.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die <b>Rueckabwicklung</b> (Kompensation/Saga): erledigte Schritte werden in umgekehrter
    /// Reihenfolge und mit dem Variablen-Stand ihrer Vollendung zurueckgenommen.
    /// </summary>
    [TestClass]
    public class WorkflowCompensationTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Start -&gt; reserve -&gt; pay -&gt; undo (Ausloeser) -&gt; Ende, mit je einem
        /// Rueckabwicklungs-Pfad an reserve und pay.
        /// </summary>
        private void SaveSaga(Action<CompensateNode> configure = null)
        {
            var undo = new CompensateNode { Id = "undo", Name = "Roll back" };
            configure?.Invoke(undo);

            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "reserve", ActivityRef = "work" },
                    new AutomatedActivityNode { Id = "pay", ActivityRef = "work" },
                    undo,
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "cReserve", AttachedToNodeId = "reserve" },
                    new AutomatedActivityNode { Id = "unreserve", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cReserveEnd" },
                    new CompensationNode { Id = "cPay", AttachedToNodeId = "pay" },
                    new AutomatedActivityNode { Id = "refund", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cPayEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "reserve"), F("reserve", "pay"), F("pay", "undo"), F("undo", "e"),
                    F("cReserve", "unreserve"), F("unreserve", "cReserveEnd"),
                    F("cPay", "refund"), F("refund", "cPayEnd")
                }
            });
        }

        private WorkflowEngine Engine(List<string> ran = null,
            Action<WorkflowActivityContext> onWork = null)
            => new WorkflowEngine(store, new ActivityRegistry().Register("work", ctx =>
            {
                ran?.Add(ctx.Node.Id);
                onWork?.Invoke(ctx);
            }));

        [TestMethod]
        public void TheUndoStepsRunInReverseOrder()
        {
            SaveSaga();
            var ran = new List<string>();

            WorkflowInstance inst = Engine(ran).StartWorkflow("wf");

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            CollectionAssert.AreEqual(new[] { "reserve", "pay", "refund", "unreserve" }, ran,
                "the payment is refunded before the reservation is released - the steps build on each other.");
        }

        [TestMethod]
        public void AnUndoStepSeesTheValuesOfItsOwnStep_NotTheLatestOnes()
        {
            SaveSaga();
            var seen = new Dictionary<string, object>();

            Engine(onWork: ctx =>
            {
                switch (ctx.Node.Id)
                {
                    case "reserve":
                        ctx.Variables["ticket"] = "R-1";
                        break;
                    case "pay":
                        // Der Schritt ueberschreibt, was reserve hinterlassen hat.
                        ctx.Variables["ticket"] = "P-9";
                        break;
                    case "unreserve":
                        seen["ticket"] = ctx.Variables.TryGetValue("ticket", out object v) ? v : null;
                        break;
                }
            }).StartWorkflow("wf");

            Assert.AreEqual("R-1", seen["ticket"],
                "the undo path runs with the snapshot taken when ITS step finished - the number it needs " +
                "to cancel is long overwritten by then.");
        }

        [TestMethod]
        public void OnlyCompletedStepsAreUndone()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "reserve", ActivityRef = "work" },
                    // pay wird nie erreicht - der Ausloeser haengt direkt hinter reserve.
                    new AutomatedActivityNode { Id = "pay", ActivityRef = "work" },
                    new CompensateNode { Id = "undo" },
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "cReserve", AttachedToNodeId = "reserve" },
                    new AutomatedActivityNode { Id = "unreserve", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cReserveEnd" },
                    new CompensationNode { Id = "cPay", AttachedToNodeId = "pay" },
                    new AutomatedActivityNode { Id = "refund", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cPayEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "reserve"), F("reserve", "undo"), F("undo", "e"),
                    F("cReserve", "unreserve"), F("unreserve", "cReserveEnd"),
                    F("cPay", "refund"), F("refund", "cPayEnd")
                }
            });
            var ran = new List<string>();

            Engine(ran).StartWorkflow("wf");

            CollectionAssert.DoesNotContain(ran, "refund",
                "a step that never ran has nothing to undo.");
            CollectionAssert.Contains(ran, "unreserve");
        }

        [TestMethod]
        public void ATargetedTriggerUndoesOnlyThatStep()
        {
            SaveSaga(undo => undo.TargetNodeId = "pay");
            var ran = new List<string>();

            Engine(ran).StartWorkflow("wf");

            CollectionAssert.Contains(ran, "refund");
            CollectionAssert.DoesNotContain(ran, "unreserve",
                "a trigger with a target undoes exactly that step, not everything before it.");
        }

        [TestMethod]
        public void AStepIsUndoneOnlyOnce()
        {
            // Zwei Ausloeser hintereinander: der zweite darf nichts mehr finden.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "reserve", ActivityRef = "work" },
                    new CompensateNode { Id = "undo1" },
                    new CompensateNode { Id = "undo2" },
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "cReserve", AttachedToNodeId = "reserve" },
                    new AutomatedActivityNode { Id = "unreserve", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "reserve"), F("reserve", "undo1"), F("undo1", "undo2"), F("undo2", "e"),
                    F("cReserve", "unreserve"), F("unreserve", "cEnd")
                }
            });
            var ran = new List<string>();

            Engine(ran).StartWorkflow("wf");

            Assert.AreEqual(1, ran.Count(r => r == "unreserve"),
                "an already undone step must not be undone a second time.");
        }

        [TestMethod]
        public void ATriggerWithNothingPending_JustCarriesOn()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new CompensateNode { Id = "undo" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "work" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "undo"), F("undo", "after"), F("after", "e") }
            });
            var ran = new List<string>();

            WorkflowInstance inst = Engine(ran).StartWorkflow("wf");

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status,
                "nothing to undo is the normal case of a flow that has not done anything yet - not a fault.");
            CollectionAssert.Contains(ran, "after");
        }

        [TestMethod]
        public void TheTriggeringBranchWaitsUntilTheUndoIsThrough()
        {
            // Im Rueckabwicklungs-Pfad ein Wartepunkt: der Ausloeser darf nicht weiterlaufen.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "reserve", ActivityRef = "work" },
                    new CompensateNode { Id = "undo" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "work" },
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "cReserve", AttachedToNodeId = "reserve" },
                    new WaitNode { Id = "confirm", SignalName = "undone" },
                    new SidePathEndNode { Id = "cEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "reserve"), F("reserve", "undo"), F("undo", "after"), F("after", "e"),
                    F("cReserve", "confirm"), F("confirm", "cEnd")
                }
            });
            var ran = new List<string>();
            WorkflowEngine engine = Engine(ran);

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance parked = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Waiting, parked.Status);
            Assert.AreEqual(TokenStatus.Waiting, parked.Tokens.Single(t => t.NodeId == "undo").Status,
                "the triggering branch parks until the undo is through.");
            CollectionAssert.DoesNotContain(ran, "after");

            engine.SignalWorkflow(inst.Id, "undone");

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            CollectionAssert.Contains(ran, "after", "once the undo is through, the branch carries on.");
        }

        [TestMethod]
        public void ATriggerInsideASection_UndoesOnlyThatSection()
        {
            // Aussen reserve, innen inner - der Ausloeser steht IM Abschnitt.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "reserve", ActivityRef = "work" },
                    new SubProcessNode { Id = "sub" },
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "cReserve", AttachedToNodeId = "reserve" },
                    new AutomatedActivityNode { Id = "unreserve", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cReserveEnd" },

                    new StartNode { Id = "iStart", ParentNodeId = "sub" },
                    new AutomatedActivityNode { Id = "inner", ActivityRef = "work", ParentNodeId = "sub" },
                    new CompensateNode { Id = "undo", ParentNodeId = "sub" },
                    new EndNode { Id = "iEnd", ParentNodeId = "sub" },
                    new CompensationNode { Id = "cInner", AttachedToNodeId = "inner", ParentNodeId = "sub" },
                    new AutomatedActivityNode { Id = "uninner", ActivityRef = "work", ParentNodeId = "sub" },
                    new SidePathEndNode { Id = "cInnerEnd", ParentNodeId = "sub" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "reserve"), F("reserve", "sub"), F("sub", "e"),
                    F("cReserve", "unreserve"), F("unreserve", "cReserveEnd"),
                    F("iStart", "inner"), F("inner", "undo"), F("undo", "iEnd"),
                    F("cInner", "uninner"), F("uninner", "cInnerEnd")
                }
            });
            var ran = new List<string>();

            WorkflowInstance inst = Engine(ran).StartWorkflow("wf");

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            CollectionAssert.Contains(ran, "uninner");
            CollectionAssert.DoesNotContain(ran, "unreserve",
                "a trigger inside a section undoes what happened in that section, not the whole process.");
        }

        [TestMethod]
        public void AUserTaskIsArmedWhenItIsCompleted()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new UserActivityNode { Id = "approve", TaskKey = "Approve" },
                    new CompensateNode { Id = "undo" },
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "cApprove", AttachedToNodeId = "approve" },
                    new AutomatedActivityNode { Id = "revoke", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "approve"), F("approve", "undo"), F("undo", "e"),
                    F("cApprove", "revoke"), F("revoke", "cEnd")
                }
            });
            var ran = new List<string>();
            WorkflowEngine engine = Engine(ran);

            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.AreEqual(0, store.GetInstance(inst.Id).Compensations.Count,
                "an open task has not done anything yet - there is nothing to undo.");

            Token task = store.GetInstance(inst.Id).Tokens.Single(t => t.NodeId == "approve");
            engine.CompleteUserTask(inst.Id, task.Id, new Dictionary<string, object>());
            engine.Advance(store.GetInstance(inst.Id));

            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            CollectionAssert.Contains(ran, "revoke", "the task is armed when it is completed.");
        }

        [TestMethod]
        public void AHandlerOnAWaitPoint_IsRejected()
        {
            var definition = new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "c", AttachedToNodeId = "w" },
                    new AutomatedActivityNode { Id = "undoIt", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "w"), F("w", "e"), F("c", "undoIt"), F("undoIt", "cEnd")
                }
            };

            var issues = WorkflowDefinitionValidator.Validate(definition);

            Assert.IsTrue(
                issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "c"
                                && i.Message.Contains("could be undone")),
                "a wait point leaves nothing behind that could be undone - a handler there is a silent " +
                "non-effect.");
        }

        [TestMethod]
        public void AHandlerWithAnIncomingConnection_IsRejected()
        {
            var definition = new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "work" },
                    new EndNode { Id = "e" },
                    new CompensationNode { Id = "c", AttachedToNodeId = "a" },
                    new AutomatedActivityNode { Id = "undoIt", ActivityRef = "work" },
                    new SidePathEndNode { Id = "cEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "a"), F("a", "e"), F("a", "c"), F("c", "undoIt"), F("undoIt", "cEnd")
                }
            };

            var issues = WorkflowDefinitionValidator.Validate(definition);

            Assert.IsTrue(
                issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "c"
                                && i.Message.Contains("incoming connection")),
                "a handler is triggered on demand, not reached by a connection.");
        }

        [TestMethod]
        public void ATriggerPointingNowhere_IsRejected()
        {
            var definition = new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new CompensateNode { Id = "undo", TargetNodeId = "ghost" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "undo"), F("undo", "e") }
            };

            var issues = WorkflowDefinitionValidator.Validate(definition);

            Assert.IsTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.NodeId == "undo"
                                         && i.Message.Contains("ghost")));
        }
    }
}
