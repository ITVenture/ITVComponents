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
    /// Prueft den verteilten Handoff (Phase 3b): eine Aktivitaet mit
    /// <see cref="AutomatedActivityNode.ExecutionTarget"/> laeuft nur auf einem Runner/einer Engine, die
    /// dieses Ziel bedient. Kann der aktuelle Host das Ziel nicht bedienen, parkt der Zweig
    /// (<see cref="TokenStatus.WaitingForTarget"/>) und wird von einer Engine mit passendem Ziel aufgenommen
    /// und dort ausgefuehrt.
    /// </summary>
    [TestClass]
    public class WorkflowHandoffTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            store.SaveDefinition(HandoffDef());
        }

        /// <summary>Start -&gt; Aktivitaet "remote" (Ziel "backend", setzt Variable "marked") -&gt; End.</summary>
        private static WorkflowDefinition HandoffDef()
        {
            return new WorkflowDefinition
            {
                Id = "ho",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "r", ActivityRef = "mark", ExecutionTarget = "backend" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->r", SourceId = "s", TargetId = "r" },
                    new SequenceFlow { Id = "r->e", SourceId = "r", TargetId = "e" }
                }
            };
        }

        private WorkflowEngine Engine(params string[] hostTargets)
        {
            return new WorkflowEngine(store,
                new ActivityRegistry().Register("mark", ctx => ctx.Variables["marked"] = true),
                hostTargets: hostTargets);
        }

        [TestMethod]
        public void ForeignTarget_ParksBranch_WithoutRunningActivity()
        {
            WorkflowEngine web = Engine("web"); // bedient "backend" NICHT
            WorkflowInstance inst = web.StartWorkflow("ho");

            WorkflowInstance stored = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Waiting, stored.Status, "the instance rests until the target host picks it up.");
            Token t = stored.Tokens.Single();
            Assert.AreEqual(TokenStatus.WaitingForTarget, t.Status);
            Assert.AreEqual("backend", t.WaitingTarget);
            Assert.AreEqual("r", t.NodeId, "the token stays on the activity node so the target host runs it.");
            Assert.IsFalse(stored.Variables.ContainsKey("marked"), "the activity must NOT have run on the wrong host.");
        }

        [TestMethod]
        public void MatchingTarget_RunsActivityHere()
        {
            WorkflowEngine backend = Engine("backend");
            WorkflowInstance inst = backend.StartWorkflow("ho");

            WorkflowInstance stored = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, stored.Status);
            Assert.AreEqual(true, stored.Variables["marked"], "the activity ran on the matching host.");
        }

        [TestMethod]
        public void NoTargetDeclared_RunsAnywhere_EvenWithoutHostTargets()
        {
            // Rueckwaerts-Kompatibilitaet: ohne ExecutionTarget laeuft die Aktivitaet auf jeder Engine.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "plain",
                Version = 1,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "r", ActivityRef = "mark" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->r", SourceId = "s", TargetId = "r" },
                    new SequenceFlow { Id = "r->e", SourceId = "r", TargetId = "e" }
                }
            });

            WorkflowInstance inst = Engine().StartWorkflow("plain"); // gar keine Host-Ziele
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(inst.Id).Status);
            Assert.AreEqual(true, store.GetInstance(inst.Id).Variables["marked"]);
        }

        [TestMethod]
        public void ReactivateForTargets_NonMatchingTarget_LeavesBranchParked()
        {
            WorkflowInstance inst = Engine("web").StartWorkflow("ho"); // parkt fuer "backend"

            IReadOnlyList<string> ids = Engine("frontend").ReactivateForTargets(inst.Id, new[] { "frontend" });

            Assert.AreEqual(0, ids.Count, "a host not serving 'backend' must not pick the branch up.");
            Assert.AreEqual(TokenStatus.WaitingForTarget, store.GetInstance(inst.Id).Tokens.Single().Status);
        }

        [TestMethod]
        public void EndToEnd_ParkedBranch_ResumedAndExecutedByTargetHost()
        {
            // Host A (web) parkt den Backend-Schritt.
            WorkflowEngine web = Engine("web");
            WorkflowInstance inst = web.StartWorkflow("ho");
            Assert.AreEqual(TokenStatus.WaitingForTarget, store.GetInstance(inst.Id).Tokens.Single().Status);

            // Host B (backend) findet den Zweig, reaktiviert ihn und treibt ihn voran -> Aktivitaet laeuft hier.
            WorkflowEngine backend = Engine("backend");
            List<WorkflowInstance> discovered = store.FindBranchesWaitingForTarget(new[] { "backend" }).ToList();
            CollectionAssert.Contains(discovered.Select(i => i.Id).ToList(), inst.Id,
                "the backend host must discover the parked branch.");

            IReadOnlyList<string> ids = backend.ReactivateForTargets(inst.Id, new[] { "backend" });
            Assert.AreEqual(1, ids.Count);
            backend.Advance(store.GetInstance(inst.Id));

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(true, final.Variables["marked"], "the backend host executed the handed-off activity.");
        }

        [TestMethod]
        public void ParkedBranchAtOpenJoin_IsNotADeadlock()
        {
            // Ein am Join geparktes Joining-Token PLUS ein ziel-wartendes Token: der ziel-wartende Zweig kann
            // noch aktiv werden und liefern -> die Instanz ruht (Waiting), kein Deadlock-Fault.
            var inst = new WorkflowInstance
            {
                DefinitionId = "ho",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Running,
                Tokens = new List<Token>
                {
                    new Token { Id = "j", NodeId = "join", Status = TokenStatus.Joining },
                    new Token { Id = "w", NodeId = "r", Status = TokenStatus.WaitingForTarget, WaitingTarget = "backend" }
                }
            };
            store.SaveInstance(inst);

            Engine("web").Advance(store.GetInstance(inst.Id));

            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(inst.Id).Status,
                "a still-resumable target branch must keep the join from being reported as a deadlock.");
        }
    }
}
