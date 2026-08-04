using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Graph;
using ITVComponents.Workflow.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Test
{
    /// <summary>
    /// Prueft die Darstellung des Rueckabwicklungs-Pfads: er klebt am Rand seines Schritts, und zwar an
    /// der anderen Ecke als ein Fristen-Timer - sonst laegen die beiden Aussagen uebereinander.
    /// </summary>
    [TestClass]
    public class CompensationLayoutTest
    {
        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>Start -&gt; task -&gt; Ende, mit Frist und Rueckabwicklungs-Pfad am selben Schritt.</summary>
        private static WorkflowDefinition Definition()
        {
            return new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new UserActivityNode { Id = "task", TaskKey = "Approve" },
                    new EndNode { Id = "e" },
                    new BoundaryTimerNode { Id = "deadline", AttachedToNodeId = "task" },
                    new SidePathEndNode { Id = "dEnd" },
                    new CompensationNode { Id = "undoPath", AttachedToNodeId = "task" },
                    new AutomatedActivityNode { Id = "revoke", ActivityRef = "x" },
                    new SidePathEndNode { Id = "cEnd" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "task"), F("task", "e"),
                    F("deadline", "dEnd"),
                    F("undoPath", "revoke"), F("revoke", "cEnd")
                }
            };
        }

        [TestMethod]
        public void TheUndoPathSticksToTheBottomOfItsStep()
        {
            GraphLayout layout = GraphLayout.Compute(Definition());

            LaidOutNode host = layout.Nodes.Single(n => n.Id == "task");
            LaidOutNode handler = layout.Nodes.Single(n => n.Id == "undoPath");

            Assert.AreEqual(host.Y + host.Height - (handler.Height / 2), handler.Y, 0.01,
                "it hangs half-overlapping the bottom edge - the position is derived, not drawn.");
            Assert.IsTrue(handler.X >= host.X && handler.X + handler.Width <= host.X + host.Width,
                "it stays within the width of the step it belongs to.");
        }

        [TestMethod]
        public void ItDoesNotSitOnTopOfTheDeadline()
        {
            GraphLayout layout = GraphLayout.Compute(Definition());

            LaidOutNode timer = layout.Nodes.Single(n => n.Id == "deadline");
            LaidOutNode handler = layout.Nodes.Single(n => n.Id == "undoPath");

            Assert.IsTrue(handler.X + handler.Width <= timer.X,
                "the undo path docks left, the deadline right - two different statements must not overlap.");
        }

        [TestMethod]
        public void TheTriggerIsAWaitingNodeInTheFlow_NotAnEndpoint()
        {
            var definition = new WorkflowDefinition
            {
                Id = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new CompensateNode { Id = "undo" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "undo"), F("undo", "e") }
            };

            LaidOutNode trigger = GraphLayout.Compute(definition).Nodes.Single(n => n.Id == "undo");

            // Sechseck wie die uebrigen Wartepunkte: der Zweig parkt hier, bis die Ruecknahme durch ist.
            Assert.AreEqual(NodeShape.Hexagon, trigger.Shape);
            Assert.AreNotEqual(NodeShape.Ellipse, trigger.Shape,
                "a round node reads as start or end - but the trigger ends nothing, the flow carries on "
                + "through its outgoing connection.");
            StringAssert.StartsWith(trigger.Label, "↺");
        }
    }
}
