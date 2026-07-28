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

    /// <summary>
    /// Eine platzierte Kante: ein rechtwinkliger Streckenzug von Knotenrand zu Knotenrand samt
    /// Beschriftung.
    /// </summary>
    public sealed class LaidOutEdge
    {
        /// <summary>Kanten-Id.</summary>
        public string Id { get; init; } = "";

        /// <summary>Id des Quellknotens (fuer den Editor, der Kanten beim Ziehen neu berechnet).</summary>
        public string SourceId { get; init; } = "";

        /// <summary>Id des Zielknotens.</summary>
        public string TargetId { get; init; } = "";

        /// <summary>
        /// Der Streckenzug von Knotenrand zu Knotenrand. Aufeinanderfolgende Punkte sind immer
        /// achsenparallel verbunden (nur waagrechte und senkrechte Teilstuecke, Ecken dazwischen).
        /// </summary>
        public IReadOnlyList<GraphPoint> Points { get; init; } = Array.Empty<GraphPoint>();

        /// <summary>Startpunkt X (auf dem Rand des Quellknotens).</summary>
        public double X1 { get; init; }

        /// <summary>Startpunkt Y.</summary>
        public double Y1 { get; init; }

        /// <summary>Endpunkt X (auf dem Rand des Zielknotens).</summary>
        public double X2 { get; init; }

        /// <summary>Endpunkt Y.</summary>
        public double Y2 { get; init; }

        /// <summary>Der Streckenzug als SVG-Pfadangabe (<c>d</c>-Attribut), invariant formatiert.</summary>
        public string PathData
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < Points.Count; i++)
                {
                    sb.Append(i == 0 ? 'M' : 'L')
                      .Append(Points[i].X.ToString(System.Globalization.CultureInfo.InvariantCulture))
                      .Append(' ')
                      .Append(Points[i].Y.ToString(System.Globalization.CultureInfo.InvariantCulture))
                      .Append(' ');
                }

                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>Beschriftung (Name oder Bedingung), oder null.</summary>
        public string? Label { get; init; }

        /// <summary>
        /// Versatz des Andockpunktes am Quellknoten gegenueber der Seitenmitte (die zugeteilte Spur).
        /// Der Editor gibt ihn an die JS-Seite weiter, damit die Linie beim Ziehen dieselbe Spur behaelt.
        /// </summary>
        public double SourceOffset { get; init; }

        /// <summary>Versatz des Andockpunktes am Zielknoten gegenueber der Seitenmitte.</summary>
        public double TargetOffset { get; init; }

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

        /// <summary>Abstand zweier Andockpunkte an derselben Knotenseite.</summary>
        private const double LaneGap = 16;

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
                    Label = NodeLabel(node),
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

            IReadOnlyList<LaidOutEdge> edges = RouteEdges(definition, byId, nodes);

            double width = nodes.Count == 0 ? 2 * Margin : nodes.Max(n => n.X + n.Width) + Margin;
            double height = nodes.Count == 0 ? 2 * Margin : nodes.Max(n => n.Y + n.Height) + Margin;
            foreach (LaidOutEdge edge in edges)
            {
                // Ein Umweg um einen Knoten herum darf ueber die Knotenflaeche hinausreichen - sonst
                // waere die Linie am Rand der Zeichenflaeche abgeschnitten.
                foreach (GraphPoint p in edge.Points)
                {
                    width = Math.Max(width, p.X + Margin);
                    height = Math.Max(height, p.Y + Margin);
                }
            }

            return new GraphLayout(nodes, edges, width, height);
        }

        /// <summary>
        /// Legt die Wege aller Kanten fest. Drei Schritte, in dieser Reihenfolge, weil jeder auf dem
        /// vorigen aufbaut:
        /// <list type="number">
        ///   <item><description>Seitenwahl je Kantenende (Geometrie; Fehlerausgaenge sind auf den roten
        ///   Port festgenagelt, damit die Linie dort beginnt, wo der Nutzer sie gezogen hat).</description></item>
        ///   <item><description>Spurvergabe: alle Enden, die an derselben Knotenseite haengen, bekommen
        ///   je einen eigenen Andockpunkt. Das ist der Grund, warum Hin- und Rueckweg zwischen zwei
        ///   Knoten nebeneinander liegen statt uebereinander.</description></item>
        ///   <item><description>Wegsuche je Kante (<see cref="EdgeRouter"/>).</description></item>
        /// </list>
        /// </summary>
        private static IReadOnlyList<LaidOutEdge> RouteEdges(WorkflowDefinition definition,
            IReadOnlyDictionary<string, LaidOutNode> byId, IReadOnlyList<LaidOutNode> nodes)
        {
            var obstacles = new List<GraphRect>(nodes.Count);
            foreach (LaidOutNode n in nodes)
            {
                obstacles.Add(RectOf(n));
            }

            HashSet<string> errorFlows = ErrorFlowIds(definition);
            var plans = new List<EdgePlan>();
            foreach (SequenceFlow flow in definition.Flows)
            {
                if (flow.SourceId == null || flow.TargetId == null
                    || !byId.TryGetValue(flow.SourceId, out LaidOutNode? source)
                    || !byId.TryGetValue(flow.TargetId, out LaidOutNode? target))
                {
                    continue;
                }

                var plan = new EdgePlan { Flow = flow, Source = source, Target = target };
                if (ReferenceEquals(source, target))
                {
                    plan.SelfLoop = true;
                    plans.Add(plan);
                    continue;
                }

                (PortSide sourceSide, PortSide targetSide) = EdgeRouter.ChooseSides(RectOf(source), RectOf(target));
                if (!string.IsNullOrEmpty(flow.Id) && errorFlows.Contains(flow.Id))
                {
                    // Der Fehler-Port sitzt im Editor bei 85% der Hoehe an der rechten Seite. Die Kante
                    // dort beginnen zu lassen ist nicht nur huebscher: sie ist damit auch ohne die rote
                    // Farbe als der andere Ausgang erkennbar - und sie kann mit dem Erfolgspfad, der die
                    // Seitenmitte benutzt, nicht zusammenfallen.
                    sourceSide = PortSide.Right;
                    plan.SourcePinned = true;
                    plan.SourceOffset = source.Height * 0.35;
                }

                plan.SourceSide = sourceSide;
                plan.TargetSide = targetSide;
                plans.Add(plan);
            }

            AssignLanes(plans);

            var edges = new List<LaidOutEdge>(plans.Count);
            foreach (EdgePlan plan in plans)
            {
                IReadOnlyList<GraphPoint> points = plan.SelfLoop
                    ? EdgeRouter.RouteSelfLoop(RectOf(plan.Source))
                    : EdgeRouter.Route(RectOf(plan.Source), plan.SourceSide, plan.SourceOffset,
                        RectOf(plan.Target), plan.TargetSide, plan.TargetOffset, obstacles);

                (double labelX, double labelY) = LabelPosition(points);
                edges.Add(new LaidOutEdge
                {
                    Id = plan.Flow.Id ?? "",
                    SourceId = plan.Flow.SourceId!,
                    TargetId = plan.Flow.TargetId!,
                    Points = points,
                    SourceOffset = plan.SourceOffset,
                    TargetOffset = plan.TargetOffset,
                    X1 = points[0].X,
                    Y1 = points[0].Y,
                    X2 = points[points.Count - 1].X,
                    Y2 = points[points.Count - 1].Y,
                    Label = EdgeLabel(plan.Flow),
                    LabelX = labelX,
                    LabelY = labelY
                });
            }

            return edges;
        }

        /// <summary>
        /// Verteilt die Andockpunkte: alle Kantenenden an derselben Knotenseite bekommen einen eigenen
        /// Versatz aus der Seitenmitte. Sortiert wird nach der Lage des jeweils anderen Endes, damit
        /// sich die Linien direkt am Knoten nicht unnoetig ueberkreuzen.
        /// </summary>
        private static void AssignLanes(List<EdgePlan> plans)
        {
            var groups = new Dictionary<string, List<(int Plan, bool IsSource, double Sort)>>(StringComparer.Ordinal);
            for (int i = 0; i < plans.Count; i++)
            {
                EdgePlan plan = plans[i];
                if (plan.SelfLoop)
                {
                    continue;
                }

                if (!plan.SourcePinned)
                {
                    Add(groups, plan.Source.Id, plan.SourceSide, i, true, SortKey(plan.SourceSide, plan.Target));
                }

                Add(groups, plan.Target.Id, plan.TargetSide, i, false, SortKey(plan.TargetSide, plan.Source));
            }

            foreach (KeyValuePair<string, List<(int Plan, bool IsSource, double Sort)>> group in groups)
            {
                List<(int Plan, bool IsSource, double Sort)> anchors = group.Value;
                if (anchors.Count == 1)
                {
                    continue;
                }

                // Reiner Faecher: an dieser Knotenseite haengen NUR ausgehende (Split) ODER nur eingehende
                // (Join) Enden. Dann teilen sie sich EINEN Andockpunkt (Versatz bleibt 0), damit sich die
                // Linien auf dem gemeinsamen Anfangs- bzw. Endstueck ueberlagern und sich erst dort teilen,
                // wo die Wege auseinanderlaufen - so wie man es am Parallel-Split (mehrere Ausgaenge) und am
                // Join (mehrere Eingaenge) erwartet. Nur ein GEMISCHTES Ende (aus- UND eingehend an derselben
                // Seite, z.B. Hin- und Rueckweg zwischen zwei Knoten) wird gespreizt: dort laegen die Linien
                // sonst deckungsgleich uebereinander und waeren nicht mehr unterscheidbar.
                bool pureFan = anchors.All(a => a.IsSource) || anchors.All(a => !a.IsSource);
                if (pureFan)
                {
                    continue;
                }

                anchors.Sort((a, b) => a.Sort != b.Sort ? a.Sort.CompareTo(b.Sort) : a.Plan.CompareTo(b.Plan));
                for (int i = 0; i < anchors.Count; i++)
                {
                    EdgePlan plan = plans[anchors[i].Plan];
                    bool isSource = anchors[i].IsSource;
                    PortSide side = isSource ? plan.SourceSide : plan.TargetSide;
                    LaidOutNode node = isSource ? plan.Source : plan.Target;
                    double span = EdgeRouter.LeavesVertically(side) ? node.Width : node.Height;
                    double limit = Math.Max(0, (span / 2) - 8);
                    double offset = (i - ((anchors.Count - 1) / 2.0)) * LaneGap;
                    offset = Math.Max(-limit, Math.Min(limit, offset));

                    if (isSource)
                    {
                        plan.SourceOffset = offset;
                    }
                    else
                    {
                        plan.TargetOffset = offset;
                    }
                }
            }
        }

        private static void Add(Dictionary<string, List<(int Plan, bool IsSource, double Sort)>> groups,
            string nodeId, PortSide side, int plan, bool isSource, double sort)
        {
            string key = nodeId + "|" + side;
            if (!groups.TryGetValue(key, out List<(int Plan, bool IsSource, double Sort)>? list))
            {
                list = new List<(int Plan, bool IsSource, double Sort)>();
                groups[key] = list;
            }

            list.Add((plan, isSource, sort));
        }

        /// <summary>
        /// Die Lage des anderen Endes quer zur Seite - danach werden die Spuren einer Seite sortiert.
        /// </summary>
        private static double SortKey(PortSide side, LaidOutNode other)
            => EdgeRouter.LeavesVertically(side) ? other.CenterX : other.CenterY;

        private static GraphRect RectOf(LaidOutNode n) => new GraphRect(n.X, n.Y, n.Width, n.Height);

        /// <summary>
        /// Die Beschriftung sitzt in der Mitte des laengsten Teilstuecks - dort ist am ehesten Platz,
        /// und sie landet nicht auf einer Ecke.
        /// </summary>
        private static (double X, double Y) LabelPosition(IReadOnlyList<GraphPoint> points)
        {
            if (points.Count < 2)
            {
                return points.Count == 1 ? (points[0].X, points[0].Y) : (0, 0);
            }

            int best = 0;
            double bestLength = -1;
            for (int i = 0; i < points.Count - 1; i++)
            {
                double length = Math.Abs(points[i + 1].X - points[i].X) + Math.Abs(points[i + 1].Y - points[i].Y);
                if (length > bestLength)
                {
                    bestLength = length;
                    best = i;
                }
            }

            return ((points[best].X + points[best + 1].X) / 2, (points[best].Y + points[best + 1].Y) / 2);
        }

        /// <summary>
        /// Die Kanten, die Fehler-Ausgang eines Knotens sind. Sie docken am roten Port an statt an der
        /// geometrisch naechsten Seite.
        /// </summary>
        private static HashSet<string> ErrorFlowIds(WorkflowDefinition definition)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorkflowNode n in definition.Nodes)
            {
                string? id = n switch
                {
                    AutomatedActivityNode a => a.ErrorFlowId,
                    CallWorkflowNode c => c.ErrorFlowId,
                    _ => null
                };

                if (!string.IsNullOrEmpty(id))
                {
                    result.Add(id!);
                }
            }

            return result;
        }

        /// <summary>Der Bauplan einer Kante zwischen Seitenwahl und Wegsuche.</summary>
        private sealed class EdgePlan
        {
            public SequenceFlow Flow { get; init; } = default!;

            public LaidOutNode Source { get; init; } = default!;

            public LaidOutNode Target { get; init; } = default!;

            public PortSide SourceSide { get; set; }

            public PortSide TargetSide { get; set; }

            public double SourceOffset { get; set; }

            public double TargetOffset { get; set; }

            /// <summary>Fehlerausgang: der Andockpunkt ist gesetzt und nimmt an der Spurvergabe nicht teil.</summary>
            public bool SourcePinned { get; set; }

            public bool SelfLoop { get; set; }
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

        /// <summary>
        /// Beschriftung eines Knotens. Ein Join, der das Ergebnis seiner parallelen Region deklariert,
        /// bekommt denselben <c>{…}</c>-Marker wie eine Kante mit Mapping - sonst waere im Bild nicht zu
        /// sehen, dass hier nur ein Teil der Zweig-Ergebnisse weiterlaeuft.
        /// </summary>
        private static string NodeLabel(WorkflowNode node)
        {
            string text = string.IsNullOrEmpty(node.Name) ? node.Id : node.Name;
            if (node is UserActivityNode task)
            {
                // Eine Benutzer-Aufgabe haelt den Prozess an, bis ein MENSCH handelt - der teuerste
                // Unterschied im Bild. Ohne Marker saehe sie aus wie ein automatischer Schritt.
                return "👤 " + (string.IsNullOrEmpty(node.Name) && !string.IsNullOrWhiteSpace(task.TaskKey)
                    ? task.TaskKey
                    : text);
            }

            bool mapped = node is ParallelGatewayNode p
                          && (p.Outputs is { Count: > 0 } || p.ScopeMode == ActivityScopeMode.Replace);
            return mapped ? "{…} " + text : text;
        }

        private static string? EdgeLabel(SequenceFlow flow)
        {
            // Ein Mapping auf der Kante veraendert den Variablen-Stack, waere im Bild aber unsichtbar -
            // deshalb als Praefix an die Beschriftung. Ohne Beschriftung steht es fuer sich allein.
            string prefix = flow.Inputs is { Count: > 0 } || flow.ScopeMode == ActivityScopeMode.Replace
                ? "{…} "
                : "";

            if (!string.IsNullOrWhiteSpace(flow.Name))
            {
                return prefix + flow.Name;
            }

            if (!string.IsNullOrWhiteSpace(flow.Condition))
            {
                string c = flow.Condition!.Trim();
                return prefix + (c.Length <= 24 ? c : c.Substring(0, 21) + "…");
            }

            return prefix.Length == 0 ? null : prefix.Trim();
        }

    }
}
