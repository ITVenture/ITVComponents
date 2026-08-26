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
    /// Prueft den <see cref="TerminateEndNode"/>: er beendet die GANZE Instanz, nicht nur seinen Zweig.
    /// </summary>
    [TestClass]
    public class WorkflowTerminateTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>
        /// Start -&gt; AND-Split -&gt; (Zweig A wartet | Zweig B terminiert) - der klassische Abbruch aus
        /// einem Nebenstrang heraus.
        /// </summary>
        private void SaveParallelDefinition(TerminateEndNode terminate)
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new WaitNode { Id = "waits", SignalName = "never" },
                    new AutomatedActivityNode { Id = "decide", ActivityRef = "decide" },
                    terminate,
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "waits"), F("split", "decide"),
                    F("waits", "e"), F("decide", terminate.Id)
                }
            });
        }

        [TestMethod]
        public void Terminate_EndsEveryOtherBranch_AndCompletesTheInstance()
        {
            SaveParallelDefinition(new TerminateEndNode { Id = "stop" });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("decide", _ => { }));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status,
                "a terminate ends the workflow regularly - it is not a cancellation.");
            Assert.IsTrue(final.Tokens.All(t => t.Status == TokenStatus.Consumed),
                "the waiting sibling branch must be gone too - that is the whole point.");
            Assert.IsTrue(final.History.Any(h => h.Event == "Terminated"));
        }

        [TestMethod]
        public void Terminate_CarriesItsOwnResult()
        {
            // Ohne Ergebnis endete der Workflow auf diesem Weg ohne jede Aussage darueber, WARUM.
            var terminate = new TerminateEndNode { Id = "stop" };
            terminate.Outputs.Add(new ActivityOutputBinding { Parameter = "reason", Variable = "outcome" });
            SaveParallelDefinition(terminate);
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("decide",
                ctx => ctx.Variables["reason"] = "customer cancelled"));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual("customer cancelled", final.Variables["outcome"]);
        }

        [TestMethod]
        public void Terminate_PublishesTheTerminatingBranchScope()
        {
            // Der Abbruch geschieht INNERHALB einer parallelen Region: der Instanz-Stack steht dort noch
            // auf dem Stand des Splits. Ohne Veroeffentlichung zoege das Ergebnis aus dem falschen Stand.
            var terminate = new TerminateEndNode { Id = "stop" };
            terminate.Outputs.Add(new ActivityOutputBinding { Parameter = "reason", Variable = "outcome" });
            SaveParallelDefinition(terminate);
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("decide",
                ctx => ctx.Variables["reason"] = "written into the branch scope only"));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual("written into the branch scope only", final.Variables["outcome"],
                "the branch that terminates decides the result - its scope must reach the instance.");
        }

        [TestMethod]
        public void Terminate_CancelsRunningSubworkflows()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "child",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "cs" },
                    new WaitNode { Id = "cw", SignalName = "never" },
                    new EndNode { Id = "ce" }
                },
                Flows = new List<SequenceFlow> { F("cs", "cw"), F("cw", "ce") }
            });
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new CallWorkflowNode { Id = "call", SubDefinitionId = "child" },
                    new AutomatedActivityNode { Id = "decide", ActivityRef = "decide" },
                    new TerminateEndNode { Id = "stop" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "call"), F("split", "decide"),
                    F("call", "e"), F("decide", "stop")
                }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("decide", _ => { }));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance child = store.FindChildInstances(inst.Id).SingleOrDefault();
            Assert.IsNotNull(child, "the subworkflow was started before the terminate hit.");
            Assert.AreEqual(WorkflowStatus.Cancelled, store.GetInstance(child.Id).Status,
                "a child left running would be an orphan - nobody collects its result any more.");
        }
    }
}
