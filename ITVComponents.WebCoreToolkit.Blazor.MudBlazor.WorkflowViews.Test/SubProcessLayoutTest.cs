using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Graph;
using ITVComponents.Workflow.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Test
{
    /// <summary>
    /// Prueft die Darstellung eingebetteter Abschnitte: der Rahmen wird aus den KINDERN aufgezogen, ein
    /// zugeklappter Abschnitt verbirgt seinen Inhalt, und das automatische Layout schachtelt statt zu
    /// ueberlappen.
    /// </summary>
    /// <remarks>
    /// Im gerenderten SVG waeren das alles Augenmass-Fragen ("liegt der Rahmen um seine Knoten?") - hier
    /// sind sie nachpruefbar. Genau deshalb ist das Layout ein blazor-freier Baustein.
    /// </remarks>
    [TestClass]
    public class SubProcessLayoutTest
    {
        private static SequenceFlow F(string from, string to) =>
            new SequenceFlow { Id = $"{from}->{to}", SourceId = from, TargetId = to };

        /// <summary>Start -&gt; [Abschnitt: iStart -&gt; a -&gt; b -&gt; iEnd] -&gt; after -&gt; Ende.</summary>
        private static WorkflowDefinition Definition(bool collapsed = false)
        {
            return new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new SubProcessNode { Id = "sub", Name = "Check", Collapsed = collapsed },
                    new StartNode { Id = "iStart", ParentNodeId = "sub" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "x", ParentNodeId = "sub" },
                    new AutomatedActivityNode { Id = "b", ActivityRef = "x", ParentNodeId = "sub" },
                    new EndNode { Id = "iEnd", ParentNodeId = "sub" },
                    new AutomatedActivityNode { Id = "after", ActivityRef = "x" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "sub"), F("sub", "after"), F("after", "e"),
                    F("iStart", "a"), F("a", "b"), F("b", "iEnd")
                }
            };
        }

        [TestMethod]
        public void TheFrameEnclosesEveryChild()
        {
            GraphLayout layout = GraphLayout.Compute(Definition());

            LaidOutNode frame = layout.Nodes.Single(n => n.Id == "sub");
            Assert.IsTrue(frame.IsContainer, "an expanded section is drawn as a frame, not as a shape.");

            foreach (string childId in new[] { "iStart", "a", "b", "iEnd" })
            {
                LaidOutNode child = layout.Nodes.Single(n => n.Id == childId);
                Assert.IsTrue(child.X >= frame.X && child.Y >= frame.Y
                              && child.X + child.Width <= frame.X + frame.Width
                              && child.Y + child.Height <= frame.Y + frame.Height,
                    $"'{childId}' must lie inside the frame of its section.");
            }
        }

        [TestMethod]
        public void TheChildrenStayBelowTheHeaderBand()
        {
            // Das Kopfband traegt den Namen des Abschnitts - laege ein Knoten darin, ueberdeckte er ihn.
            GraphLayout layout = GraphLayout.Compute(Definition());

            LaidOutNode frame = layout.Nodes.Single(n => n.Id == "sub");
            foreach (LaidOutNode child in layout.Nodes.Where(n => n.Id is "iStart" or "a" or "b" or "iEnd"))
            {
                Assert.IsTrue(child.Y >= frame.Y + frame.HeaderHeight,
                    $"'{child.Id}' overlaps the section's header band.");
            }
        }

        [TestMethod]
        public void TheFrameDoesNotOverlapOuterNodes()
        {
            // Der Grund fuer das geschachtelte Auto-Layout: als zusammenhangloser Teilgraph landeten die
            // inneren Knoten sonst in denselben Zeilen wie die aeusseren, und der Rahmen laege quer
            // ueber allem.
            GraphLayout layout = GraphLayout.Compute(Definition());

            LaidOutNode frame = layout.Nodes.Single(n => n.Id == "sub");
            foreach (LaidOutNode outer in layout.Nodes.Where(n => n.Id is "s" or "after" or "e"))
            {
                bool overlaps = outer.X < frame.X + frame.Width && outer.X + outer.Width > frame.X
                                && outer.Y < frame.Y + frame.Height && outer.Y + outer.Height > frame.Y;
                Assert.IsFalse(overlaps, $"the outer node '{outer.Id}' sits inside the section's frame.");
            }
        }

        [TestMethod]
        public void CollapsedHidesTheContent()
        {
            GraphLayout layout = GraphLayout.Compute(Definition(collapsed: true));

            Assert.IsFalse(layout.Nodes.Any(n => n.Id is "iStart" or "a" or "b" or "iEnd"),
                "a collapsed section hides its nodes - that is the whole point.");
            LaidOutNode node = layout.Nodes.Single(n => n.Id == "sub");
            Assert.IsFalse(node.IsContainer, "collapsed it is drawn as an ordinary node.");
        }

        [TestMethod]
        public void CollapsedHidesTheInnerEdgesToo()
        {
            GraphLayout layout = GraphLayout.Compute(Definition(collapsed: true));

            Assert.IsFalse(layout.Edges.Any(e => e.Id is "iStart->a" or "a->b" or "b->iEnd"),
                "edges between hidden nodes must not be drawn - they would float over the diagram.");
            Assert.IsTrue(layout.Edges.Any(e => e.Id == "s->sub"),
                "the edges of the section itself stay.");
        }

        [TestMethod]
        public void AnEmptySectionStillHasASize()
        {
            var definition = new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new SubProcessNode { Id = "sub", Name = "Empty" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow> { F("s", "sub"), F("sub", "e") }
            };

            GraphLayout layout = GraphLayout.Compute(definition);

            LaidOutNode frame = layout.Nodes.Single(n => n.Id == "sub");
            Assert.IsTrue(frame.Width > 0 && frame.Height > 0,
                "an empty section must stay visible and hittable - otherwise nothing could be dragged in.");
        }

        [TestMethod]
        public void WithExplicitCoordinates_TheFrameFollowsItsChildren()
        {
            // Beim expliziten Layout (sobald jemand etwas verschoben hat) ist die Rahmen-Geometrie
            // abgeleitet - eigene Koordinaten waeren eine zweite Wahrheit, die beim Verschieben eines
            // Kindes sofort falsch wuerde.
            WorkflowDefinition definition = Definition();
            foreach (WorkflowNode node in definition.Nodes)
            {
                node.Diagram = new DiagramShape { X = 10, Y = 10 };
            }

            definition.GetNode("a")!.Diagram = new DiagramShape { X = 400, Y = 300 };

            GraphLayout layout = GraphLayout.Compute(definition);

            LaidOutNode frame = layout.Nodes.Single(n => n.Id == "sub");
            LaidOutNode moved = layout.Nodes.Single(n => n.Id == "a");
            Assert.IsTrue(moved.X + moved.Width <= frame.X + frame.Width
                          && moved.Y + moved.Height <= frame.Y + frame.Height,
                "moving a child must grow the frame, not leave the child outside it.");
        }

        [TestMethod]
        public void NestedSectionsAreEnclosedByTheirParent()
        {
            var definition = new WorkflowDefinition
            {
                TechnicalName = "wf",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new SubProcessNode { Id = "outer", Name = "Outer" },
                    new StartNode { Id = "oStart", ParentNodeId = "outer" },
                    new SubProcessNode { Id = "inner", Name = "Inner", ParentNodeId = "outer" },
                    new StartNode { Id = "iStart", ParentNodeId = "inner" },
                    new AutomatedActivityNode { Id = "deep", ActivityRef = "x", ParentNodeId = "inner" },
                    new EndNode { Id = "iEnd", ParentNodeId = "inner" },
                    new EndNode { Id = "oEnd", ParentNodeId = "outer" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    F("s", "outer"), F("outer", "e"),
                    F("oStart", "inner"), F("inner", "oEnd"),
                    F("iStart", "deep"), F("deep", "iEnd")
                }
            };

            GraphLayout layout = GraphLayout.Compute(definition);

            LaidOutNode outer = layout.Nodes.Single(n => n.Id == "outer");
            LaidOutNode inner = layout.Nodes.Single(n => n.Id == "inner");
            LaidOutNode deep = layout.Nodes.Single(n => n.Id == "deep");

            Assert.IsTrue(inner.X >= outer.X && inner.Y >= outer.Y
                          && inner.X + inner.Width <= outer.X + outer.Width
                          && inner.Y + inner.Height <= outer.Y + outer.Height,
                "the inner section must sit inside the outer one.");
            Assert.IsTrue(deep.X >= inner.X && deep.Y >= inner.Y
                          && deep.X + deep.Width <= inner.X + inner.Width
                          && deep.Y + deep.Height <= inner.Y + inner.Height,
                "a node two levels down must sit inside the innermost section.");
        }
    }
}
