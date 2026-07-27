using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Prueft den Fehler-Ausgang von Aktivitaeten (<see cref="AutomatedActivityNode.ErrorFlowId"/>): ein
    /// Aktivitaets-Fehler (Exception ODER kontrolliert via <see cref="WorkflowActivityContext.Fail"/>)
    /// faultet nicht die Instanz, sondern nimmt die Fehler-Kante - mit Fehlermeldung, Zwischenstand und
    /// Fehlversuchs-Zaehler. Damit lassen sich Retry-Schleifen und Benutzer-Korrektur im Graphen abbilden.
    /// </summary>
    [TestClass]
    public class WorkflowErrorRoutingTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        [TestMethod]
        public void ActivityException_WithErrorFlow_RoutesInsteadOfFaulting()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode
                    {
                        Id = "a", ActivityRef = "boom",
                        ErrorFlowId = "a->handled", ErrorVariable = "err", AttemptVariable = "tries"
                    },
                    new EndNode { Id = "ok" },
                    new EndNode { Id = "handled" }
                },
                Flows = new List<SequenceFlow> { F("s", "a"), F("a", "ok"), F("a", "handled") }
            });
            var engine = new WorkflowEngine(store,
                new ActivityRegistry().Register("boom", _ => throw new InvalidOperationException("kaboom")));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status, "the error flow handles the failure - no fault.");
            StringAssert.Contains((string)final.Variables["err"], "kaboom");
            Assert.AreEqual(1, final.Variables["tries"], "the attempt counter incremented on failure.");
        }

        [TestMethod]
        public void ControlledFail_KeepsIntermediateOutputs_AndTakesErrorFlow()
        {
            var node = new AutomatedActivityNode
            {
                Id = "a", ActivityRef = "partial", ErrorFlowId = "a->handled", ErrorVariable = "err"
            };
            node.Outputs.Add(new ActivityOutputBinding { Parameter = "failed", Variable = "failedFiles" });
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" }, node, new EndNode { Id = "ok" }, new EndNode { Id = "handled" }
                },
                Flows = new List<SequenceFlow> { F("s", "a"), F("a", "ok"), F("a", "handled") }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("partial", ctx =>
            {
                ctx.Outputs["failed"] = "3 files";   // Zwischenstand
                ctx.Fail("3 of 1000 failed");
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual("3 files", final.Variables["failedFiles"], "the controlled-failure outputs (intermediate state) are kept.");
            StringAssert.Contains((string)final.Variables["err"], "3 of 1000");
        }

        [TestMethod]
        public void ActivityFailure_WithoutErrorFlow_FaultsAsBefore()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "boom" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "a"), F("a", "e") }
            });
            var engine = new WorkflowEngine(store,
                new ActivityRegistry().Register("boom", _ => throw new InvalidOperationException("x")));

            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.AreEqual(WorkflowStatus.Faulted, store.GetInstance(inst.Id).Status,
                "without an error flow a failure still faults (backward compatible).");
        }

        [TestMethod]
        public void RetryLoop_AutoFixThenSucceeds_AndCounterResets()
        {
            // Start -> process (Fehler-Ausgang -> gw). process scheitert, solange "fixed" nicht gesetzt ist.
            // gw: tries <= 1 -> autofix (setzt fixed) -> zurueck zu process; sonst -> giveup-End.
            var process = new AutomatedActivityNode
            {
                Id = "process", ActivityRef = "process",
                ErrorFlowId = "process->gw", AttemptVariable = "tries", ErrorVariable = "err"
            };
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    process,
                    new ExclusiveGatewayNode { Id = "gw", DefaultFlowId = "gw->giveup" },
                    new AutomatedActivityNode { Id = "autofix", ActivityRef = "autofix" },
                    new EndNode { Id = "done" },
                    new EndNode { Id = "giveup" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "process"),
                    F("process", "done"),                                   // Erfolgs-Ausgang
                    F("process", "gw"),                                     // Fehler-Ausgang
                    new SequenceFlow { Id = "gw->autofix", SourceId = "gw", TargetId = "autofix", Condition = "tries <= 1" },
                    F("gw", "giveup"),                                      // Default (aufgeben)
                    F("autofix", "process")                                 // zurueck zur Aktivitaet
                }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry()
                .Register("process", ctx =>
                {
                    if (ctx.Variables.TryGetValue("fixed", out object f) && f is bool b && b)
                    {
                        ctx.Variables["result"] = "done";
                    }
                    else
                    {
                        ctx.Fail("not fixed yet");
                    }
                })
                .Register("autofix", ctx => ctx.Variables["fixed"] = true));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status, "the auto-fix retry loop completes the workflow.");
            Assert.AreEqual("done", final.Variables["result"], "the activity succeeded after the fix.");
            Assert.AreEqual(0, final.Variables["tries"], "the attempt counter reset to 0 on success.");
        }

        [TestMethod]
        public void ErrorPath_ToUserWait_ThenLoopBack_Succeeds()
        {
            // process scheitert -> Fehler-Ausgang -> Wait (Benutzer-UI) -> Signal setzt fixed -> zurueck -> Erfolg.
            var process = new AutomatedActivityNode
            {
                Id = "process", ActivityRef = "process", ErrorFlowId = "process->userfix"
            };
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    process,
                    new WaitNode { Id = "userfix", SignalName = "userdone" },
                    new EndNode { Id = "done" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "process"),
                    F("process", "done"),          // Erfolg
                    F("process", "userfix"),        // Fehler -> Benutzer
                    F("userfix", "process")         // nach Benutzer-Aktion zurueck
                }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("process", ctx =>
            {
                if (ctx.Variables.TryGetValue("fixed", out object f) && f is bool b && b)
                {
                    ctx.Variables["result"] = "done";
                }
                else
                {
                    ctx.Fail("needs user");
                }
            }));

            WorkflowInstance inst = engine.StartWorkflow("wf");
            Assert.AreEqual(WorkflowStatus.Waiting, store.GetInstance(inst.Id).Status,
                "after the failure the branch waits for the user at the error path.");

            // Benutzer erledigt die Korrektur und liefert das Signal (setzt fixed).
            engine.SignalWorkflow(inst.Id, "userdone", new Dictionary<string, object> { { "fixed", true } });

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status, "after the user fix the activity succeeds and the workflow continues.");
            Assert.AreEqual("done", final.Variables["result"]);
        }
    }
}
