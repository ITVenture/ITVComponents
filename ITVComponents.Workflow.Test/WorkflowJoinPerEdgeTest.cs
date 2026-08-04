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
    /// Prueft, dass ein Join <b>je eingehender Kante</b> zaehlt und nicht nur die Anzahl wartender
    /// Tokens.
    /// </summary>
    /// <remarks>
    /// Die blosse Anzahl haelt nur bei balancierten Graphen. Laufen ueber EINE Kante zwei Tokens ein,
    /// waehrend eine andere leer bleibt, stimmt die Summe - und der Join feuerte frueher mit halber
    /// Mannschaft. Sichtbar wird das erst im Ergebnis (ein Zweig fehlt im Merge), nicht in der Ursache.
    /// </remarks>
    [TestClass]
    public class WorkflowJoinPerEdgeTest
    {
        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        [TestMethod]
        public void TwoTokensOnTheSameEdge_DoNotFireTheJoin()
        {
            // Der Join hat zwei eingehende Kanten (von a und von b). Beide Tokens stehen aber ueber
            // DERSELBEN Kante an - die Summe stimmt, die Deckung nicht.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "noop" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "noop" },
                    new ParallelGatewayNode { Id = "join" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "noop" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "a"), F("a", "join"), F("b", "join"), F("join", "after"), F("after", "e")
                }
            });

            var instance = new WorkflowInstance
            {
                DefinitionId = "wf",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Running,
                Tokens = new List<Token>
                {
                    // Beide sind ueber a->join angekommen; b hat nie geliefert.
                    new Token
                    {
                        Id = "t1", NodeId = "join", Status = TokenStatus.Joining,
                        ArrivedViaFlowId = "a->join"
                    },
                    new Token
                    {
                        Id = "t2", NodeId = "join", Status = TokenStatus.Joining,
                        ArrivedViaFlowId = "a->join"
                    }
                }
            };
            store.SaveInstance(instance);
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("noop", _ => { }));

            engine.Advance(store.GetInstance(instance.Id));

            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.IsFalse(final.History.Any(h => h.Event == "ParallelJoin"),
                "the join must not fire while one of its incoming edges has delivered nothing.");
        }

        [TestMethod]
        public void OneTokenPerEdge_FiresTheJoin()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "noop" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "noop" },
                    new ParallelGatewayNode { Id = "join" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "noop" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "a"), F("a", "join"), F("b", "join"), F("join", "after"), F("after", "e")
                }
            });

            var instance = new WorkflowInstance
            {
                DefinitionId = "wf",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Running,
                Tokens = new List<Token>
                {
                    new Token
                    {
                        Id = "t1", NodeId = "join", Status = TokenStatus.Joining,
                        ArrivedViaFlowId = "a->join"
                    },
                    new Token
                    {
                        Id = "t2", NodeId = "join", Status = TokenStatus.Joining,
                        ArrivedViaFlowId = "b->join"
                    }
                }
            };
            store.SaveInstance(instance);
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("noop", _ => { }));

            engine.Advance(store.GetInstance(instance.Id));

            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.AreEqual(1, final.History.Count(h => h.Event == "ParallelJoin"));
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
        }

        [TestMethod]
        public void TokensWithoutAnArrivalEdge_FallBackToCounting()
        {
            // Instanzen, die beim Deployment schon am Join warteten, kennen ihre Kante nicht. Fuer die
            // muss die alte Zaehlung weitergelten - sonst haengen sie fuer immer.
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "noop" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "noop" },
                    new ParallelGatewayNode { Id = "join" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "noop" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "a"), F("a", "join"), F("b", "join"), F("join", "after"), F("after", "e")
                }
            });

            var instance = new WorkflowInstance
            {
                DefinitionId = "wf",
                DefinitionVersion = 1,
                Status = WorkflowStatus.Running,
                Tokens = new List<Token>
                {
                    new Token { Id = "t1", NodeId = "join", Status = TokenStatus.Joining },
                    new Token { Id = "t2", NodeId = "join", Status = TokenStatus.Joining }
                }
            };
            store.SaveInstance(instance);
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("noop", _ => { }));

            engine.Advance(store.GetInstance(instance.Id));

            WorkflowInstance final = store.GetInstance(instance.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status,
                "an instance parked before the arrival edge was tracked must still get through.");
        }

        [TestMethod]
        public void ANormalSplitAndJoin_StillWorksEndToEnd()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new ParallelGatewayNode { Id = "split" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "noop" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "noop" },
                    new ParallelGatewayNode { Id = "join" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "split"), F("split", "a"), F("split", "b"),
                    F("a", "join"), F("b", "join"), F("join", "e")
                }
            });
            var engine = new WorkflowEngine(store, new ActivityRegistry().Register("noop", _ => { }));

            WorkflowInstance inst = engine.StartWorkflow("wf");

            WorkflowInstance final = store.GetInstance(inst.Id);
            Assert.AreEqual(WorkflowStatus.Completed, final.Status);
            Assert.AreEqual(1, final.History.Count(h => h.Event == "ParallelJoin"));
        }
    }
}
