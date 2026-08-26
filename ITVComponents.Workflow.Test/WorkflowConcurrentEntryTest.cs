using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft die nebenlaeufigen Einstiegspunkte der Engine, die der Runner nutzt: <c>CreateInstance</c>
    /// (anlegen ohne Vortrieb) sowie <c>ReactivateSignal</c>/<c>ReactivateTimers</c> (Tokens ueber den
    /// Wartepunkt schieben, ohne sie selbst voranzutreiben).
    /// </summary>
    [TestClass]
    public class WorkflowConcurrentEntryTest
    {
        private InMemoryWorkflowStore store;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            engine = new WorkflowEngine(store, new ActivityRegistry());
        }

        private static WorkflowDefinition WaitDef()
        {
            return new WorkflowDefinition
            {
                TechnicalName = "wd",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "act" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->a", SourceId = "w", TargetId = "a" },
                    new SequenceFlow { Id = "a->e", SourceId = "a", TargetId = "e" }
                }
            };
        }

        private WorkflowInstance WaitingAt(string nodeId, string signal = null, DateTime? due = null)
        {
            var inst = new WorkflowInstance
            {
                DefinitionKey = store.GetDefinition("wd", 1).Key,                DefinitionId = "wd",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Waiting,
                Tokens = new List<Token>
                {
                    new Token { Id = "t", NodeId = nodeId, Status = TokenStatus.Waiting, WaitingSignal = signal, DueUtc = due }
                }
            };
            store.SaveInstance(inst);
            return inst;
        }

        [TestMethod]
        public void CreateInstance_CreatesActiveStartToken_WithoutAdvancing()
        {
            store.SaveDefinition(WaitDef());
            WorkflowInstance inst = engine.CreateInstance("wd");

            Assert.AreEqual(WorkflowStatus.Running, inst.Status);
            Assert.AreEqual(1, inst.Tokens.Count);
            Token t = inst.Tokens.Single();
            Assert.AreEqual(TokenStatus.Active, t.Status);
            Assert.AreEqual("s", t.NodeId, "the start token must still sit on the start node - NOT advanced.");
        }

        [TestMethod]
        public void ReactivateSignal_MovesWaitingTokenPastTheWaitNode()
        {
            store.SaveDefinition(WaitDef());
            WorkflowInstance inst = WaitingAt("w", signal: "go");

            IReadOnlyList<string> ids = engine.ReactivateSignal(inst.Id, "go");

            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual("t", ids[0]);
            Token t = store.GetInstance(inst.Id).Tokens.Single(x => x.Id == "t");
            Assert.AreEqual(TokenStatus.Active, t.Status);
            Assert.AreEqual("a", t.NodeId, "the token moved past the wait node - ready for a branch-task.");
        }

        [TestMethod]
        public void ReactivateSignal_NoMatchingWait_ReturnsEmpty()
        {
            store.SaveDefinition(WaitDef());
            WorkflowInstance inst = WaitingAt("w", signal: "other");

            Assert.AreEqual(0, engine.ReactivateSignal(inst.Id, "go").Count);
        }

        [TestMethod]
        public void ReactivateTimers_MovesDueTokenPastItsNode()
        {
            store.SaveDefinition(WaitDef());
            WorkflowInstance inst = WaitingAt("w", due: DateTime.UtcNow.AddMinutes(-1));

            IReadOnlyList<string> ids = engine.ReactivateTimers(inst.Id, DateTime.UtcNow);

            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual("a", store.GetInstance(inst.Id).Tokens.Single(x => x.Id == "t").NodeId);
        }

        [TestMethod]
        public void ReactivateTimers_NotYetDue_ReturnsEmpty()
        {
            store.SaveDefinition(WaitDef());
            WorkflowInstance inst = WaitingAt("w", due: DateTime.UtcNow.AddHours(1));

            Assert.AreEqual(0, engine.ReactivateTimers(inst.Id, DateTime.UtcNow).Count);
        }
    }
}
