using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Graph;
using ITVComponents.Workflow.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Test
{
    /// <summary>
    /// Prueft die Zusicherungen der Kantenfuehrung. Sie sind der Grund, warum das Routing ein reiner,
    /// blazor-freier Baustein ist: im gerenderten SVG waeren "rechtwinklig", "kreuzt keinen Knoten" und
    /// "liegt nicht auf einer anderen Linie" nur mit dem Auge pruefbar - und genau das ist der Fehler,
    /// der bisher unbemerkt blieb (Hin- und Rueckweg exakt uebereinander).
    /// </summary>
    [TestClass]
    public class EdgeRoutingTest
    {
        private const double Tolerance = 0.001;

        [TestMethod]
        public void EverySegment_IsHorizontalOrVertical()
        {
            GraphLayout layout = GraphLayout.Compute(BranchingDefinition());

            foreach (LaidOutEdge edge in layout.Edges)
            {
                Assert.IsTrue(edge.Points.Count >= 2, $"edge '{edge.Id}' has no course");
                for (int i = 0; i < edge.Points.Count - 1; i++)
                {
                    GraphPoint a = edge.Points[i];
                    GraphPoint b = edge.Points[i + 1];
                    bool axisParallel = Math.Abs(a.X - b.X) <= Tolerance || Math.Abs(a.Y - b.Y) <= Tolerance;
                    Assert.IsTrue(axisParallel,
                        $"edge '{edge.Id}' segment {i} runs diagonally: ({a.X},{a.Y}) -> ({b.X},{b.Y})");
                }
            }
        }

        [TestMethod]
        public void Route_AvoidsANodeThatBlocksTheDirectLine()
        {
            // A --------------> C, mit B exakt dazwischen auf derselben Hoehe. Die gerade Verbindung
            // liefe mitten durch B; die Kante muss aussen herum.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(Activity("a", 0, 0));
            def.Nodes.Add(Activity("b", 200, 0));
            def.Nodes.Add(Activity("c", 400, 0));
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "a", TargetId = "c" });

            GraphLayout layout = GraphLayout.Compute(def);

            AssertNoNodeIsCrossed(layout);
            Assert.IsTrue(layout.Edges.Single().Points.Count > 2, "the edge must take a detour, not a straight line");
        }

        [TestMethod]
        public void NoEdge_CrossesANode_InABranchingWorkflow()
        {
            AssertNoNodeIsCrossed(GraphLayout.Compute(BranchingDefinition()));
        }

        [TestMethod]
        public void OppositeEdges_BetweenTheSamePair_DoNotShareTheirAnchors()
        {
            // Der gemeldete Fall: Fehlerpfad auf eine Benutzer-Aufgabe und von dort zurueck auf die
            // Aktivitaet (Wiederholung). Beide Kanten haengen an denselben zwei Knoten - lagen sie auf
            // denselben Andockpunkten, waere im Bild nur eine Linie zu sehen.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(Activity("a", 0, 0));
            def.Nodes.Add(Activity("b", 320, 0));
            def.Flows.Add(new SequenceFlow { Id = "there", SourceId = "a", TargetId = "b" });
            def.Flows.Add(new SequenceFlow { Id = "back", SourceId = "b", TargetId = "a" });

            GraphLayout layout = GraphLayout.Compute(def);
            LaidOutEdge there = layout.Edges.Single(e => e.Id == "there");
            LaidOutEdge back = layout.Edges.Single(e => e.Id == "back");

            // Beide docken an derselben Seite von A an (rechts) - aber nicht am selben Punkt.
            Assert.IsTrue(Math.Abs(there.Y1 - back.Y2) > 8,
                $"both edges attach to node A at almost the same spot ({there.Y1} vs {back.Y2})");
            Assert.IsTrue(Math.Abs(there.Y2 - back.Y1) > 8,
                $"both edges attach to node B at almost the same spot ({there.Y2} vs {back.Y1})");
            Assert.IsFalse(SameCourse(there, back), "the two edges are drawn on top of each other");
        }

        [TestMethod]
        public void ErrorFlow_LeavesAtTheErrorPort_NotAtTheSideCentre()
        {
            // Der rote Port sitzt bei 85% der Hoehe an der rechten Seite. Startete die Kante in der
            // Seitenmitte, laege sie auf dem Erfolgspfad - und der Nutzer saehe nicht, welcher Strang
            // welcher ist.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(new AutomatedActivityNode { Id = "act", ActivityRef = "x", ErrorFlowId = "err", Diagram = new DiagramShape { X = 0, Y = 0 } });
            def.Nodes.Add(new UserActivityNode { Id = "task", TaskKey = "t", Diagram = new DiagramShape { X = 320, Y = 200 } });
            def.Nodes.Add(new EndNode { Id = "end", Diagram = new DiagramShape { X = 320, Y = 0 } });
            def.Flows.Add(new SequenceFlow { Id = "ok", SourceId = "act", TargetId = "end" });
            def.Flows.Add(new SequenceFlow { Id = "err", SourceId = "act", TargetId = "task" });

            GraphLayout layout = GraphLayout.Compute(def);
            LaidOutNode act = layout.Nodes.Single(n => n.Id == "act");
            LaidOutEdge error = layout.Edges.Single(e => e.Id == "err");
            LaidOutEdge success = layout.Edges.Single(e => e.Id == "ok");

            Assert.AreEqual(act.X + act.Width, error.X1, Tolerance, "error flow must start on the right border");
            Assert.IsTrue(error.Y1 > act.CenterY, "error flow must start below the centre, at the red port");
            Assert.IsTrue(Math.Abs(error.Y1 - success.Y1) > 8, "error and success flow must not start at the same point");
        }

        [TestMethod]
        public void NearlyAlignedNodes_AreConnectedByOneStraightLine()
        {
            // Zwei Knoten, deren Mitten um wenige Pixel auseinanderliegen: ohne Ausrichtung entstuende
            // ein Z mit einem 4-Pixel-Versatz - das liest sich als Zeichenfehler, nicht als Absicht.
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(Activity("a", 0, 0));
            def.Nodes.Add(Activity("b", 300, 4));
            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "a", TargetId = "b" });

            LaidOutEdge edge = GraphLayout.Compute(def).Edges.Single();

            Assert.AreEqual(2, edge.Points.Count, "a nearly straight connection must not be drawn as a Z");
            Assert.AreEqual(edge.Y1, edge.Y2, Tolerance);
        }

        [TestMethod]
        public void SelfLoop_StartsAndEndsOnTheNodeBorder()
        {
            var def = new WorkflowDefinition { Id = "wf" };
            def.Nodes.Add(Activity("a", 100, 100));
            def.Flows.Add(new SequenceFlow { Id = "retry", SourceId = "a", TargetId = "a" });

            GraphLayout layout = GraphLayout.Compute(def);
            LaidOutNode node = layout.Nodes.Single();
            LaidOutEdge loop = layout.Edges.Single();

            Assert.AreEqual(node.X + node.Width, loop.X1, Tolerance, "loop should leave on the right");
            Assert.AreEqual(node.Y, loop.Y2, Tolerance, "loop should arrive on the top");
            AssertNoNodeIsCrossed(layout);
        }

        [TestMethod]
        public void Canvas_GrowsToContainDetours()
        {
            GraphLayout layout = GraphLayout.Compute(BranchingDefinition());

            foreach (LaidOutEdge edge in layout.Edges)
            {
                foreach (GraphPoint p in edge.Points)
                {
                    Assert.IsTrue(p.X <= layout.Width, $"edge '{edge.Id}' leaves the canvas to the right");
                    Assert.IsTrue(p.Y <= layout.Height, $"edge '{edge.Id}' leaves the canvas at the bottom");
                }
            }
        }

        /// <summary>
        /// Kein Teilstueck einer Kante darf durch das Innere eines Knotens laufen. Der Rand selbst ist
        /// erlaubt - dort docken die Kanten an.
        /// </summary>
        private static void AssertNoNodeIsCrossed(GraphLayout layout)
        {
            const double inset = 1;
            foreach (LaidOutEdge edge in layout.Edges)
            {
                for (int i = 0; i < edge.Points.Count - 1; i++)
                {
                    GraphPoint a = edge.Points[i];
                    GraphPoint b = edge.Points[i + 1];
                    double minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
                    double minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);

                    foreach (LaidOutNode n in layout.Nodes)
                    {
                        bool crosses = minX < n.X + n.Width - inset && maxX > n.X + inset
                                       && minY < n.Y + n.Height - inset && maxY > n.Y + inset;
                        Assert.IsFalse(crosses,
                            $"edge '{edge.Id}' segment {i} ({a.X},{a.Y})->({b.X},{b.Y}) runs through node '{n.Id}'");
                    }
                }
            }
        }

        /// <summary>Zwei Kanten sind deckungsgleich, wenn ihre Streckenzuege Punkt fuer Punkt gleich sind.</summary>
        private static bool SameCourse(LaidOutEdge a, LaidOutEdge b)
        {
            if (a.Points.Count != b.Points.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Points.Count; i++)
            {
                if (Math.Abs(a.Points[i].X - b.Points[i].X) > Tolerance || Math.Abs(a.Points[i].Y - b.Points[i].Y) > Tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private static AutomatedActivityNode Activity(string id, double x, double y)
            => new AutomatedActivityNode { Id = id, ActivityRef = "x", Diagram = new DiagramShape { X = x, Y = y } };

        /// <summary>
        /// Ein Ablauf mit Verzweigung, Zusammenfuehrung, Fehlerpfad und Ruecksprung - genug Kanten,
        /// damit die Zusicherungen etwas zu pruefen haben.
        /// </summary>
        private static WorkflowDefinition BranchingDefinition()
        {
            var def = new WorkflowDefinition { Id = "wf", Version = 1 };
            def.Nodes.Add(new StartNode { Id = "start", Diagram = new DiagramShape { X = 40, Y = 200 } });
            def.Nodes.Add(new AutomatedActivityNode { Id = "act", ActivityRef = "x", ErrorFlowId = "toTask", Diagram = new DiagramShape { X = 200, Y = 190 } });
            def.Nodes.Add(new ExclusiveGatewayNode { Id = "gw", Diagram = new DiagramShape { X = 420, Y = 195 } });
            def.Nodes.Add(new UserActivityNode { Id = "task", TaskKey = "check", Diagram = new DiagramShape { X = 200, Y = 380 } });
            def.Nodes.Add(new AutomatedActivityNode { Id = "act2", ActivityRef = "y", Diagram = new DiagramShape { X = 620, Y = 100 } });
            def.Nodes.Add(new EndNode { Id = "end", Diagram = new DiagramShape { X = 860, Y = 210 } });

            def.Flows.Add(new SequenceFlow { Id = "f1", SourceId = "start", TargetId = "act" });
            def.Flows.Add(new SequenceFlow { Id = "f2", SourceId = "act", TargetId = "gw" });
            def.Flows.Add(new SequenceFlow { Id = "toTask", SourceId = "act", TargetId = "task" });
            def.Flows.Add(new SequenceFlow { Id = "retry", SourceId = "task", TargetId = "act" });
            def.Flows.Add(new SequenceFlow { Id = "f3", SourceId = "gw", TargetId = "act2", Condition = "a > 1" });
            def.Flows.Add(new SequenceFlow { Id = "f4", SourceId = "gw", TargetId = "end" });
            def.Flows.Add(new SequenceFlow { Id = "f5", SourceId = "act2", TargetId = "end" });
            return def;
        }
    }
}
