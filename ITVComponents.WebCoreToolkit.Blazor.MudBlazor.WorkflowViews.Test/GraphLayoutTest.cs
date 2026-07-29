using System.Linq;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Graph;
using ITVComponents.Workflow.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Test
{
    /// <summary>
    /// Tests der Layout-/Geometrieberechnung des Graph-Renderers. Der reine (blazor-freie) Kern ist
    /// hier direkt testbar - das ist der Grund, warum er aus der Razor-Komponente ausgelagert wurde.
    /// </summary>
    [TestClass]
    public class GraphLayoutTest
    {
        private static WorkflowDefinition LinearDefinition(bool withDiagram)
        {
            var def = new WorkflowDefinition { Id = "wf", Version = 1, Name = "Test" };
            def.Nodes.Add(new StartNode { Id = "start", Name = "Start" });
            def.Nodes.Add(new AutomatedActivityNode { Id = "act", Name = "Do it", ActivityRef = "x" });
            def.Nodes.Add(new EndNode { Id = "end", Name = "End" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "start", TargetId = "act" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "act", TargetId = "end", Name = "then" });

            if (withDiagram)
            {
                def.Nodes[0].Diagram = new DiagramShape { X = 10, Y = 20 };
                def.Nodes[1].Diagram = new DiagramShape { X = 200, Y = 20 };
                def.Nodes[2].Diagram = new DiagramShape { X = 400, Y = 20 };
            }

            return def;
        }

        [TestMethod]
        public void AutoLayout_LinearFlow_LaysOutLeftToRight()
        {
            GraphLayout layout = GraphLayout.Compute(LinearDefinition(withDiagram: false));

            Assert.AreEqual(3, layout.Nodes.Count);
            Assert.AreEqual(2, layout.Edges.Count);

            LaidOutNode start = layout.Nodes.Single(n => n.Id == "start");
            LaidOutNode act = layout.Nodes.Single(n => n.Id == "act");
            LaidOutNode end = layout.Nodes.Single(n => n.Id == "end");

            // Jeder Nachfolger liegt eine Schicht weiter rechts.
            Assert.IsTrue(start.X < act.X, "activity should be right of start");
            Assert.IsTrue(act.X < end.X, "end should be right of activity");

            Assert.IsTrue(layout.Width > 0);
            Assert.IsTrue(layout.Height > 0);
        }

        [TestMethod]
        public void Compute_MapsKindsToShapes()
        {
            GraphLayout layout = GraphLayout.Compute(LinearDefinition(withDiagram: false));

            Assert.AreEqual(NodeShape.Ellipse, layout.Nodes.Single(n => n.Id == "start").Shape);
            Assert.AreEqual(NodeShape.RoundedRectangle, layout.Nodes.Single(n => n.Id == "act").Shape);
            Assert.AreEqual(NodeShape.Ellipse, layout.Nodes.Single(n => n.Id == "end").Shape);
        }

        [TestMethod]
        public void Compute_WaitingKinds_AreHexagons_ExecutingKinds_AreRounded()
        {
            var def = new WorkflowDefinition { Id = "wf", Version = 1, Name = "Kinds" };
            def.Nodes.Add(new AutomatedActivityNode { Id = "auto", Name = "Auto", ActivityRef = "x" });
            def.Nodes.Add(new CallWorkflowNode { Id = "sub", Name = "Sub", SubDefinitionId = "child" });
            def.Nodes.Add(new WaitNode { Id = "wait", Name = "Wait" });
            def.Nodes.Add(new TimerNode { Id = "timer", Name = "Timer" });

            GraphLayout layout = GraphLayout.Compute(def);

            // Wartende Knoten heben sich als Sechseck von den ausfuehrenden (abgerundetes Rechteck) ab.
            Assert.AreEqual(NodeShape.RoundedRectangle, layout.Nodes.Single(n => n.Id == "auto").Shape);
            Assert.AreEqual(NodeShape.RoundedRectangle, layout.Nodes.Single(n => n.Id == "sub").Shape);
            Assert.AreEqual(NodeShape.Hexagon, layout.Nodes.Single(n => n.Id == "wait").Shape);
            Assert.AreEqual(NodeShape.Hexagon, layout.Nodes.Single(n => n.Id == "timer").Shape);
        }

        [TestMethod]
        public void Compute_NodeLabels_CarryKindIndicatorGlyphs()
        {
            var def = new WorkflowDefinition { Id = "wf", Version = 1, Name = "Glyphs" };
            def.Nodes.Add(new AutomatedActivityNode { Id = "auto", Name = "Auto", ActivityRef = "x" });
            def.Nodes.Add(new CallWorkflowNode { Id = "sub", Name = "Sub", SubDefinitionId = "child" });
            def.Nodes.Add(new WaitNode { Id = "wait", Name = "Wait" });
            def.Nodes.Add(new TimerNode { Id = "timer", Name = "Timer" });
            def.Nodes.Add(new UserActivityNode { Id = "user", Name = "User" });

            GraphLayout layout = GraphLayout.Compute(def);

            Assert.AreEqual("⚙️ Auto", layout.Nodes.Single(n => n.Id == "auto").Label);
            Assert.AreEqual("🔗 Sub", layout.Nodes.Single(n => n.Id == "sub").Label);
            Assert.AreEqual("⏳ Wait", layout.Nodes.Single(n => n.Id == "wait").Label);
            Assert.AreEqual("🕐 Timer", layout.Nodes.Single(n => n.Id == "timer").Label);
            Assert.AreEqual("👤 User", layout.Nodes.Single(n => n.Id == "user").Label);
        }

        [TestMethod]
        public void ExplicitDiagram_UsesGivenCoordinates()
        {
            GraphLayout layout = GraphLayout.Compute(LinearDefinition(withDiagram: true));

            LaidOutNode start = layout.Nodes.Single(n => n.Id == "start");
            LaidOutNode end = layout.Nodes.Single(n => n.Id == "end");

            Assert.AreEqual(10, start.X);
            Assert.AreEqual(20, start.Y);
            Assert.AreEqual(400, end.X);
        }

        [TestMethod]
        public void Highlight_MarksOnlyRequestedNodes()
        {
            GraphLayout layout = GraphLayout.Compute(LinearDefinition(withDiagram: false), new[] { "act" });

            Assert.IsFalse(layout.Nodes.Single(n => n.Id == "start").Highlighted);
            Assert.IsTrue(layout.Nodes.Single(n => n.Id == "act").Highlighted);
            Assert.IsFalse(layout.Nodes.Single(n => n.Id == "end").Highlighted);
        }

        [TestMethod]
        public void Edge_EndpointsSitOnNodeBorders_NotCenters()
        {
            GraphLayout layout = GraphLayout.Compute(LinearDefinition(withDiagram: true));

            LaidOutNode start = layout.Nodes.Single(n => n.Id == "start");
            LaidOutNode act = layout.Nodes.Single(n => n.Id == "act");
            LaidOutEdge edge = layout.Edges.Single(e => e.Id == "f1");

            // Startpunkt liegt auf dem Rand des Quellknotens (nicht in dessen Mitte), Richtung Ziel.
            Assert.IsTrue(edge.X1 > start.CenterX, "edge should leave the source toward the target");
            Assert.IsTrue(edge.X1 <= start.X + start.Width + 0.001, "edge start must not exceed the source border");
            // Endpunkt liegt auf dem linken Rand des Zielknotens.
            Assert.IsTrue(edge.X2 < act.CenterX, "edge should arrive at the target's near border");
        }

        [TestMethod]
        public void Edge_LabelPrefersNameThenCondition()
        {
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new StartNode { Id = "start" });
            def.Nodes.Add(new EndNode { Id = "end" });
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "start", TargetId = "end", Condition = "a > 1" });

            GraphLayout layout = GraphLayout.Compute(def);

            Assert.AreEqual("a > 1", layout.Edges.Single().Label);
        }

        [TestMethod]
        public void Edge_CarriesSourceAndTargetIds()
        {
            // Der Editor zeichnet Kanten beim Ziehen client-seitig neu und braucht dafuer die
            // Endknoten-Ids je Kante.
            GraphLayout layout = GraphLayout.Compute(LinearDefinition(withDiagram: true));

            LaidOutEdge f1 = layout.Edges.Single(e => e.Id == "f1");
            Assert.AreEqual("start", f1.SourceId);
            Assert.AreEqual("act", f1.TargetId);
        }

        [TestMethod]
        public void EmptyDefinition_ProducesNonZeroCanvas()
        {
            GraphLayout layout = GraphLayout.Compute(new WorkflowDefinition());

            Assert.AreEqual(0, layout.Nodes.Count);
            Assert.IsTrue(layout.Width > 0);
            Assert.IsTrue(layout.Height > 0);
        }
    }
}
