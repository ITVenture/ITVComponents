using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Model;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Graph
{
    /// <summary>Die Grundform, mit der ein Knoten gezeichnet wird.</summary>
    public enum NodeShape
    {
        /// <summary>Ellipse (Start/Ende).</summary>
        Ellipse,

        /// <summary>Abgerundetes Rechteck (Aktivitaet).</summary>
        RoundedRectangle,

        /// <summary>Rechteck (Wartepunkt/Timer).</summary>
        Rectangle,

        /// <summary>Raute (Gateway).</summary>
        Diamond
    }

    /// <summary>Ein platzierter Knoten (Geometrie + Darstellung), fertig zum Zeichnen.</summary>
    public sealed class LaidOutNode
    {
        /// <summary>Knoten-Id.</summary>
        public string Id { get; init; } = "";

        /// <summary>Beschriftung (Name, sonst Id).</summary>
        public string Label { get; init; } = "";

        /// <summary>Die Art des Knotens.</summary>
        public NodeKind Kind { get; init; }

        /// <summary>Die Zeichenform.</summary>
        public NodeShape Shape { get; init; }

        /// <summary>Linke Kante.</summary>
        public double X { get; init; }

        /// <summary>Obere Kante.</summary>
        public double Y { get; init; }

        /// <summary>Breite.</summary>
        public double Width { get; init; }

        /// <summary>Hoehe.</summary>
        public double Height { get; init; }

        /// <summary>Hervorgehoben (z.B. aktuelle Token-Position einer Instanz).</summary>
        public bool Highlighted { get; init; }

        /// <summary>Mittelpunkt X.</summary>
        public double CenterX => X + (Width / 2);

        /// <summary>Mittelpunkt Y.</summary>
        public double CenterY => Y + (Height / 2);
    }

    /// <summary>Eine platzierte Kante (Endpunkte auf den Knotenraendern) samt Beschriftung.</summary>
    public sealed class LaidOutEdge
    {
        /// <summary>Kanten-Id.</summary>
        public string Id { get; init; } = "";

        /// <summary>Id des Quellknotens (fuer den Editor, der Kanten beim Ziehen neu berechnet).</summary>
        public string SourceId { get; init; } = "";

        /// <summary>Id des Zielknotens.</summary>
        public string TargetId { get; init; } = "";

        /// <summary>Startpunkt X (auf dem Rand des Quellknotens).</summary>
        public double X1 { get; init; }

        /// <summary>Startpunkt Y.</summary>
        public double Y1 { get; init; }

        /// <summary>Endpunkt X (auf dem Rand des Zielknotens).</summary>
        public double X2 { get; init; }

        /// <summary>Endpunkt Y.</summary>
        public double Y2 { get; init; }

        /// <summary>Beschriftung (Name oder Bedingung), oder null.</summary>
        public string? Label { get; init; }

        /// <summary>Position der Beschriftung (Mitte der Kante).</summary>
        public double LabelX { get; init; }

        /// <summary>Position der Beschriftung.</summary>
        public double LabelY { get; init; }
    }

    /// <summary>
    /// Berechnet die Geometrie zum Zeichnen einer <see cref="WorkflowDefinition"/>: Knotenpositionen
    /// (aus den Diagramm-Angaben des Modells, sonst ein automatisches Schichtenlayout), Kanten-
    /// Endpunkte auf den Knotenraendern und die Gesamtgroesse.
    /// </summary>
    /// <remarks>
    /// Bewusst reine, blazor-freie Berechnung - so ist der nichttriviale Teil (Layout/Geometrie)
    /// testbar, und die Razor-Komponente bleibt dumme Darstellung.
    /// </remarks>
    public sealed class GraphLayout
    {
        private const double Margin = 40;
        private const double LayerGap = 190;
        private const double RowGap = 110;
        private const double ColumnWidth = 150;
        private const double RowHeight = 60;

        private GraphLayout(IReadOnlyList<LaidOutNode> nodes, IReadOnlyList<LaidOutEdge> edges,
            double width, double height)
        {
            Nodes = nodes;
            Edges = edges;
            Width = width;
            Height = height;
        }

        /// <summary>Die platzierten Knoten.</summary>
        public IReadOnlyList<LaidOutNode> Nodes { get; }

        /// <summary>Die platzierten Kanten.</summary>
        public IReadOnlyList<LaidOutEdge> Edges { get; }

        /// <summary>Gesamtbreite der Zeichenflaeche.</summary>
        public double Width { get; }

        /// <summary>Gesamthoehe der Zeichenflaeche.</summary>
        public double Height { get; }

        /// <summary>
        /// Berechnet das Layout einer Definition.
        /// </summary>
        /// <param name="definition">die Definition</param>
        /// <param name="highlightNodeIds">Knoten, die hervorgehoben werden (oder null)</param>
        public static GraphLayout Compute(WorkflowDefinition definition,
            IReadOnlyCollection<string>? highlightNodeIds = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var highlight = highlightNodeIds != null
                ? new HashSet<string>(highlightNodeIds, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            IReadOnlyDictionary<string, (double X, double Y)> positions = HasExplicitLayout(definition)
                ? definition.Nodes.ToDictionary(n => n.Id, n => (n.Diagram!.X, n.Diagram!.Y))
                : AutoLayout(definition);

            var nodes = new List<LaidOutNode>();
            var byId = new Dictionary<string, LaidOutNode>(StringComparer.Ordinal);
            foreach (WorkflowNode node in definition.Nodes)
            {
                (double w, double h) = SizeFor(node.Kind);
                (double x, double y) = positions.TryGetValue(node.Id, out var p) ? p : (Margin, Margin);
                var laid = new LaidOutNode
                {
                    Id = node.Id,
                    Label = string.IsNullOrEmpty(node.Name) ? node.Id : node.Name,
                    Kind = node.Kind,
                    Shape = ShapeFor(node.Kind),
                    X = x,
                    Y = y,
                    Width = w,
                    Height = h,
                    Highlighted = node.Id != null && highlight.Contains(node.Id)
                };
                nodes.Add(laid);
                if (laid.Id != null)
                {
                    byId[laid.Id] = laid;
                }
            }

            var edges = new List<LaidOutEdge>();
            foreach (SequenceFlow flow in definition.Flows)
            {
                if (flow.SourceId == null || flow.TargetId == null
                    || !byId.TryGetValue(flow.SourceId, out LaidOutNode? source)
                    || !byId.TryGetValue(flow.TargetId, out LaidOutNode? target))
                {
                    continue;
                }

                (double sx, double sy) = ClipToBorder(source, target.CenterX, target.CenterY);
                (double tx, double ty) = ClipToBorder(target, source.CenterX, source.CenterY);
                edges.Add(new LaidOutEdge
                {
                    Id = flow.Id ?? "",
                    SourceId = flow.SourceId,
                    TargetId = flow.TargetId,
                    X1 = sx,
                    Y1 = sy,
                    X2 = tx,
                    Y2 = ty,
                    Label = EdgeLabel(flow),
                    LabelX = (sx + tx) / 2,
                    LabelY = (sy + ty) / 2
                });
            }

            double width = nodes.Count == 0 ? 2 * Margin : nodes.Max(n => n.X + n.Width) + Margin;
            double height = nodes.Count == 0 ? 2 * Margin : nodes.Max(n => n.Y + n.Height) + Margin;
            return new GraphLayout(nodes, edges, width, height);
        }

        private static bool HasExplicitLayout(WorkflowDefinition definition)
        {
            return definition.Nodes.Count > 0 && definition.Nodes.All(n => n.Diagram != null);
        }

        /// <summary>
        /// Ein einfaches Schichtenlayout (links nach rechts): jeder Knoten kommt eine Schicht hinter
        /// seinen Vorgaenger. Zyklen sind durch die Iterationsgrenze abgefangen.
        /// </summary>
        private static IReadOnlyDictionary<string, (double X, double Y)> AutoLayout(WorkflowDefinition definition)
        {
            var layer = definition.Nodes.Where(n => n.Id != null)
                .ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);

            int maxIterations = definition.Nodes.Count + 1;
            for (int i = 0; i < maxIterations; i++)
            {
                bool changed = false;
                foreach (SequenceFlow flow in definition.Flows)
                {
                    if (flow.SourceId != null && flow.TargetId != null
                        && layer.TryGetValue(flow.SourceId, out int sl)
                        && layer.TryGetValue(flow.TargetId, out int tl)
                        && tl < sl + 1)
                    {
                        layer[flow.TargetId] = sl + 1;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            var rowInLayer = new Dictionary<int, int>();
            var result = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            // In Definitionsreihenfolge, damit dasselbe Modell stabil dasselbe Bild ergibt.
            foreach (WorkflowNode node in definition.Nodes)
            {
                if (node.Id == null)
                {
                    continue;
                }

                int l = layer.TryGetValue(node.Id, out int lv) ? lv : 0;
                int row = rowInLayer.TryGetValue(l, out int r) ? r : 0;
                rowInLayer[l] = row + 1;

                (double w, double h) = SizeFor(node.Kind);
                double x = Margin + (l * LayerGap) + ((ColumnWidth - w) / 2);
                double y = Margin + (row * RowGap) + ((RowHeight - h) / 2);
                result[node.Id] = (x, y);
            }

            return result;
        }

        private static (double W, double H) SizeFor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Start:
                case NodeKind.End:
                    return (46, 46);
                case NodeKind.ExclusiveGateway:
                case NodeKind.ParallelGateway:
                    return (50, 50);
                default:
                    return (140, 54);
            }
        }

        private static NodeShape ShapeFor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Start:
                case NodeKind.End:
                    return NodeShape.Ellipse;
                case NodeKind.ExclusiveGateway:
                case NodeKind.ParallelGateway:
                    return NodeShape.Diamond;
                case NodeKind.Wait:
                case NodeKind.Timer:
                    return NodeShape.Rectangle;
                default:
                    return NodeShape.RoundedRectangle;
            }
        }

        private static string? EdgeLabel(SequenceFlow flow)
        {
            if (!string.IsNullOrWhiteSpace(flow.Name))
            {
                return flow.Name;
            }

            if (!string.IsNullOrWhiteSpace(flow.Condition))
            {
                string c = flow.Condition!.Trim();
                return c.Length <= 24 ? c : c.Substring(0, 21) + "…";
            }

            return null;
        }

        /// <summary>
        /// Schneidet die Verbindungslinie vom Mittelpunkt des Knotens zum Zielpunkt auf dem
        /// (rechteckig genaeherten) Rand des Knotens ab.
        /// </summary>
        private static (double X, double Y) ClipToBorder(LaidOutNode node, double towardX, double towardY)
        {
            double dx = towardX - node.CenterX;
            double dy = towardY - node.CenterY;
            if (dx == 0 && dy == 0)
            {
                return (node.CenterX, node.CenterY);
            }

            double halfW = node.Width / 2;
            double halfH = node.Height / 2;
            double scaleX = dx != 0 ? halfW / Math.Abs(dx) : double.PositiveInfinity;
            double scaleY = dy != 0 ? halfH / Math.Abs(dy) : double.PositiveInfinity;
            double t = Math.Min(scaleX, scaleY);
            return (node.CenterX + (dx * t), node.CenterY + (dy * t));
        }
    }
}
