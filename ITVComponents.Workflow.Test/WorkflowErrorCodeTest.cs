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
    /// Prueft den <b>Fehler-Code</b>: den kurzen, stabilen Schluessel der FehlerART neben der Meldung.
    /// </summary>
    /// <remarks>
    /// Er existiert, damit ein Prozess nach dem GRUND verzweigen kann, ohne die Meldung zu parsen -
    /// sonst haenge der Ablauf an einer Formulierung, die jederzeit jemand umschreibt oder uebersetzt.
    /// </remarks>
    [TestClass]
    public class WorkflowErrorCodeTest
    {
        private InMemoryWorkflowStore store;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            store = new InMemoryWorkflowStore();
            engine = new WorkflowEngine(store, new ActivityRegistry()
                .Register("denied", ctx => ctx.Fail("the credit line is exhausted", "CreditDenied"))
                .Register("plainFail", ctx => ctx.Fail("something went wrong"))
                .Register("boom", _ => throw new System.InvalidOperationException("crashed")));
        }

        /// <summary>Start -&gt; Aktivitaet (mit Fehlerkante) -&gt; Ende / Fehlerpfad.</summary>
        private void SaveDefinition(string activityRef, bool withErrorFlow = true)
        {
            var activity = new AutomatedActivityNode { Id = "a", ActivityRef = activityRef };
            var flows = new List<SequenceFlow>
            {
                new SequenceFlow { Id = "s->a", SourceId = "s", TargetId = "a" },
                new SequenceFlow { Id = "a->e", SourceId = "a", TargetId = "e" }
            };
            var nodes = new List<WorkflowNode>
            {
                new StartNode { Id = "s" }, activity, new EndNode { Id = "e" }
            };

            if (withErrorFlow)
            {
                activity.ErrorFlowId = "a->err";
                activity.ErrorVariable = "errMessage";
                activity.ErrorCodeVariable = "errCode";
                nodes.Add(new SidePathEndNode { Id = "err-end" });
                flows.Add(new SequenceFlow { Id = "a->err", SourceId = "a", TargetId = "err-end" });
            }

            store.SaveDefinition(new WorkflowDefinition { Id = "wf", Nodes = nodes, Flows = flows });
        }

        [TestMethod]
        public void ControlledFailure_PutsTheCodeIntoTheVariable()
        {
            SaveDefinition("denied");

            WorkflowInstance instance = engine.StartWorkflow("wf");

            Assert.AreEqual("CreditDenied", instance.Variables["errCode"],
                "the process branches on this - not on the wording of the message.");
            Assert.AreEqual("the credit line is exhausted", instance.Variables["errMessage"]);
        }

        [TestMethod]
        public void FailureWithoutCode_ClearsTheVariable()
        {
            // Sonst stuende beim zweiten Anlauf noch der Code des ersten dort, und die Verzweigung folgte
            // einem Fehler, den es nicht mehr gibt.
            SaveDefinition("plainFail");

            WorkflowInstance instance = engine.StartWorkflow("wf",
                new Dictionary<string, object> { { "errCode", "StaleFromEarlier" } });

            Assert.IsNull(instance.Variables["errCode"]);
        }

        [TestMethod]
        public void Crash_LeavesTheCodeEmpty()
        {
            // Eine abgestuerzte Aktivitaet hat keine Fehlerart gemeldet - sie behauptet auch keine.
            SaveDefinition("boom");

            WorkflowInstance instance = engine.StartWorkflow("wf");

            Assert.IsNull(instance.Variables["errCode"]);
            StringAssert.Contains((string)instance.Variables["errMessage"], "crashed");
        }

        [TestMethod]
        public void WithoutErrorFlow_TheCodeReachesTheInstanceFault()
        {
            // Das ist der Weg, auf dem die Fehlerart aus einem SUBWORKFLOW herauskommt: sie haengt am
            // Fault der Kind-Instanz, und der Aufrufer liest sie von dort.
            SaveDefinition("denied", withErrorFlow: false);

            WorkflowInstance instance = engine.StartWorkflow("wf");

            Assert.AreEqual(WorkflowStatus.Faulted, instance.Status);
            Assert.AreEqual("CreditDenied", instance.FaultCode);
        }

        [TestMethod]
        public void Retry_ClearsTheFaultCode()
        {
            SaveDefinition("denied", withErrorFlow: false);
            WorkflowInstance instance = engine.StartWorkflow("wf");
            Assert.AreEqual("CreditDenied", instance.FaultCode, "precondition.");

            engine.RetryFaulted(instance.Id);

            Assert.IsNull(store.GetInstance(instance.Id).FaultCode,
                "the code belongs to the fault and goes with it - otherwise a running instance still "
                + "carries the failure of before.");
        }

        [TestMethod]
        public void TheCodeIsInTheHistory()
        {
            SaveDefinition("denied", withErrorFlow: false);

            WorkflowInstance instance = engine.StartWorkflow("wf");

            HistoryEntry faulted = instance.History.Last(h => h.Event == "Faulted");
            StringAssert.Contains(faulted.Detail, "CreditDenied");
        }
    }
}
