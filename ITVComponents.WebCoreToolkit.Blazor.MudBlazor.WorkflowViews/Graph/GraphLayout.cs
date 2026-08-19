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

        /// <summary>
        /// Sechseck (Wartepunkte: Wait/Timer). Die abweichende Grundform trennt die WARTENDEN Knoten
        /// auf einen Blick von den AUSFUEHRENDEN (abgerundetes Rechteck) - welche Art Warten es ist,
        /// sagt dann der Indikator (Uhr = Timer, Sanduhr = Signal).
        /// </summary>
        Hexagon,

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

        /// <summary>
        /// Linke Kante. Setzbar, weil ein Fristen-Timer nach dem Aufbau an den Rand seines Schritts
        /// gerueckt wird (<c>DockBoundaryTimers</c>) - seine Position ist abgeleitet, nicht gezeichnet.
        /// </summary>
        public double X { get; internal set; }

        /// <summary>Obere Kante. Setzbar aus demselben Grund wie <see cref="X"/>.</summary>
        public double Y { get; internal set; }

        /// <summary>
        /// Breite. Setzbar, weil ein aufgeklappter Abschnitt seine Groesse aus seinen KINDERN bezieht -
        /// sie steht erst fest, wenn die platziert sind.
        /// </summary>
        public double Width { get; internal set; }

        /// <summary>Hoehe. Setzbar aus demselben Grund wie <see cref="Width"/>.</summary>
        public double Height { get; internal set; }

        /// <summary>
        /// Ob dieser Knoten ein aufgeklappter <b>Abschnitt</b> ist, also als Rahmen um andere Knoten
        /// gezeichnet wird statt als Form.
        /// </summary>
        /// <remarks>
        /// Ein Rahmen wird ZUERST gezeichnet (hinter allem) und traegt seine Beschriftung oben links im
        /// Kopfband - mittig staende sie quer ueber den Knoten, die er umschliesst.
        /// </remarks>
        public bool IsContainer { get; init; }

        /// <summary>Bei einem Rahmen: die Hoehe des Kopfbands, in dem die Beschriftung steht.</summary>
        public double HeaderHeight => IsContainer ? GraphLayout.ContainerHeader : 0;

        /// <summary>Hervorgehoben (z.B. aktuelle Token-Position einer Instanz).</summary>
        public bool Highlighted { get; init; }

        /// <summary>
        /// Die Konturfarbe, wenn dieser Knoten eine eigene traegt - sonst null (uebliche Linienfarbe).
        /// </summary>
        /// <remarks>
        /// Sie schlaegt die Auswahlfarbe, genau wie bei den Fehler-Kanten: die Eigenschaft ist
        /// dauerhaft, die Auswahl nicht - die zeigt sich in der Strichstaerke.
        /// </remarks>
        public string? OutlineColor { get; init; }

        /// <summary>
        /// Gestrichelte Kontur. Zweites, <b>farbunabhaengiges</b> Merkmal - es bleibt auch im
        /// Schwarzweiss-Ausdruck und fuer Farbfehlsichtige erhalten.
        /// </summary>
        public bool DashedOutline { get; init; }

        /// <summary>Mittelpunkt X.</summary>
        public double CenterX => X + (Width / 2);

        /// <summary>Mittelpunkt Y.</summary>
        public double CenterY => Y + (Height / 2);

        /// <summary>
        /// Das Zeichen, das <b>in</b> der Form steht, samt Schriftgroesse - oder null, wenn dieser
        /// Knoten keines hat.
        /// </summary>
        /// <remarks>
        /// Kleine Knoten tragen ihre Aussage im Symbol statt im Text: die Form ist zu klein fuer eine
        /// Beschriftung, das Zeichen nicht. Die Beschriftung steht bei ihnen unter der Form
        /// (<see cref="LabelBelow"/>).
        /// </remarks>
        public (string Text, int FontSize)? Symbol => Kind switch
        {
            NodeKind.ExclusiveGateway => ("×", 20),
            NodeKind.ParallelGateway => ("+", 20),
            // Der Kreis in der Raute - dieselbe Zeichnung wie in BPMN, und sie sagt genau das Richtige:
            // weder das eine (×) noch alle (+), sondern eine Auswahl.
            NodeKind.InclusiveGateway => ("○", 20),
            // Ein Blitz fuer das Rennen: hier entscheidet, was zuerst eintrifft. Bewusst nicht die
            // BPMN-Doppelkreis-Zeichnung - die braucht eine eigene Form, und der Unterschied zu AND/XOR
            // muss auf 50px vor allem SCHNELL lesbar sein.
            NodeKind.EventGateway => ("⚡", 18),
            NodeKind.BoundaryTimer => ("🔔", 15),
            // Der Umschlag am Schritt: dieselbe Aussage wie beim Wartepunkt (eine Nachricht trifft ein),
            // nur klebt dieser am Rand eines Schritts, an dem gerade gearbeitet wird. Neben der Glocke
            // der Frist ist er auch auf 44px sofort auseinanderzuhalten.
            NodeKind.BoundaryMessage => ("✉", 16),
            // Der Ruecklauf-Pfeil fuer beides: der Pfad, der einen Schritt zurueknimmt, und der
            // Ausloeser, der ihn anstoesst. Dass der eine am Schritt klebt und der andere im Fluss
            // steht, sagt schon die Position - dasselbe Zeichen macht den Zusammenhang lesbar.
            NodeKind.Compensation => ("↺", 17),
            // Das Kreuz im Kreis: dieselbe Grundform wie das Ende, aber unuebersehbar anders - der
            // Unterschied zwischen "dieser Zweig ist fertig" und "ALLES ist vorbei".
            NodeKind.TerminateEnd => ("✕", 20),
            _ => null
        };

        /// <summary>
        /// Ob die Beschriftung <b>unter</b> der Form steht statt in ihr. Gilt fuer alle Knoten, deren
        /// Form kleiner ist als ein lesbarer Text - ein Name mitten durch eine 40px-Raute waere
        /// breiter als der Knoten selbst.
        /// </summary>
        public bool LabelBelow => Shape is NodeShape.Ellipse or NodeShape.Diamond
                                  || Kind is NodeKind.BoundaryTimer or NodeKind.BoundaryMessage
                                      or NodeKind.Compensation;
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

        /// <summary>Luft zwischen dem Rahmen eines Abschnitts und den Knoten darin.</summary>
        internal const double ContainerPadding = 26;

        /// <summary>Hoehe des Kopfbands eines Abschnitts-Rahmens (dort steht sein Name).</summary>
        internal const double ContainerHeader = 24;

        /// <summary>Groesse eines aufgeklappten, aber noch LEEREN Abschnitts.</summary>
        private const double EmptyContainerWidth = 220;

        /// <summary>Siehe <see cref="EmptyContainerWidth"/>.</summary>
        private const double EmptyContainerHeight = 90;

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

            // Was ein zugeklappter Abschnitt umschliesst, wird nicht gezeichnet - weder die Knoten noch
            // die Kanten dazwischen. Genau das ist der Sinn des Zuklappens.
            HashSet<string> hidden = HiddenNodeIds(definition);

            IReadOnlyDictionary<string, (double X, double Y)> positions = HasExplicitLayout(definition)
                ? definition.Nodes.Where(n => n.Id != null && n.Diagram != null)
                    .ToDictionary(n => n.Id, n => (n.Diagram!.X, n.Diagram!.Y), StringComparer.Ordinal)
                : AutoLayout(definition, hidden);

            var nodes = new List<LaidOutNode>();
            var byId = new Dictionary<string, LaidOutNode>(StringComparer.Ordinal);
            foreach (WorkflowNode node in definition.Nodes)
            {
                if (node.Id != null && hidden.Contains(node.Id))
                {
                    continue;
                }

                (double w, double h) = SizeFor(node.Kind);
                // Die Id einmal greifen: sie kann fehlen (der Validator meldet das, gezeichnet wird
                // trotzdem). Ohne sie gibt es keinen Eintrag in der Positionstabelle - TryGetValue
                // wuerfe mit null sogar -, und der Knoten landet am Rand wie jeder unbekannte auch.
                string? id = node.Id;
                (double x, double y) = id != null && positions.TryGetValue(id, out var p)
                    ? p
                    : (Margin, Margin);
                bool container = node is SubProcessNode { Collapsed: false };
                var laid = new LaidOutNode
                {
                    Id = id ?? string.Empty,
                    Label = NodeLabel(node),
                    Kind = node.Kind,
                    Shape = container ? NodeShape.Rectangle : ShapeFor(node.Kind),
                    IsContainer = container,
                    X = x,
                    Y = y,
                    Width = w,
                    Height = h,
                    Highlighted = id != null && highlight.Contains(id),
                    OutlineColor = OutlineColorFor(node),
                    // Gestrichelt = der Nebenpfad laeuft NEBENHER; durchgezogen = der Hauptfluss nimmt
                    // ihn. Dieselbe Lesart wie in BPMN, wo das nicht unterbrechende Boundary-Event
                    // gestrichelt gezeichnet wird.
                    DashedOutline = node is BoundaryTimerNode { Interrupting: false }
                                            or BoundaryMessageNode { Interrupting: false }
                };
                nodes.Add(laid);
                // Ein Knoten ohne Id bleibt aus der Nachschlagetabelle heraus: Kanten koennen ihn nicht
                // meinen, und mehrere von ihnen wuerden sich sonst unter demselben leeren Schluessel
                // gegenseitig verdraengen.
                if (!string.IsNullOrEmpty(laid.Id))
                {
                    byId[laid.Id] = laid;
                }
            }

            // Die Rahmen der aufgeklappten Abschnitte aus ihren Kindern aufziehen - VOR dem Andocken und
            // der Kantenfuehrung, damit beides die endgueltige Geometrie sieht.
            SizeContainers(definition, byId, hidden);

            // Fristen-Timer an den Rand ihres Schritts setzen - VOR der Kantenfuehrung, damit ihr
            // Nebenpfad von der endgueltigen Position aus geroutet wird.
            DockBoundaryTimers(definition, byId);

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
                // Ein Abschnitts-Rahmen ist kein Koerper, sondern eine Umrandung: seine Flaeche gehoert
                // den Knoten darin. Als Hindernis gefuehrt, wuerden deren eigene Kanten um ihn
                // herumlaufen wollen - und faenden keinen Weg, weil sie in ihm beginnen.
                if (!n.IsContainer)
                {
                    obstacles.Add(RectOf(n));
                }
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
        /// <summary>
        /// Die Ids aller Knoten, die in einem <b>zugeklappten</b> Abschnitt liegen (auch mittelbar).
        /// </summary>
        private static HashSet<string> HiddenNodeIds(WorkflowDefinition definition)
        {
            var hidden = new HashSet<string>(StringComparer.Ordinal);
            var collapsed = new HashSet<string>(
                definition.Nodes.OfType<SubProcessNode>().Where(n => n.Collapsed && n.Id != null)
                    .Select(n => n.Id), StringComparer.Ordinal);
            if (collapsed.Count == 0)
            {
                return hidden;
            }

            foreach (WorkflowNode node in definition.Nodes)
            {
                if (node?.Id == null)
                {
                    continue;
                }

                // Nach oben laufen: ein Knoten ist verborgen, sobald IRGENDEIN Vorfahre zugeklappt ist.
                // Der Zaehler bricht eine (fehlerhaft) zyklische Verschachtelung ab, statt sich
                // aufzuhaengen - der Validator meldet den Zyklus separat.
                string? parent = node.ParentNodeId;
                for (int depth = 0; parent != null && depth <= definition.Nodes.Count; depth++)
                {
                    if (collapsed.Contains(parent))
                    {
                        hidden.Add(node.Id);
                        break;
                    }

                    parent = definition.GetNode(parent)?.ParentNodeId;
                }
            }

            return hidden;
        }

        /// <summary>
        /// Zieht die Rahmen der aufgeklappten Abschnitte um ihre Kinder auf - von INNEN nach aussen,
        /// damit ein geschachtelter Abschnitt schon seine endgueltige Groesse hat, wenn der aeussere ihn
        /// umschliesst.
        /// </summary>
        /// <remarks>
        /// Die Geometrie eines Rahmens ist damit <b>abgeleitet</b>, wie die eines angedockten
        /// Fristen-Timers: seine eigenen Diagramm-Koordinaten waeren eine zweite Wahrheit, die beim
        /// Verschieben eines Kindes sofort falsch wuerde. Ein leerer Abschnitt bekommt eine Vorgabegroesse,
        /// sonst waere er ein Strich und nicht zu treffen.
        /// </remarks>
        private static void SizeContainers(WorkflowDefinition definition,
            Dictionary<string, LaidOutNode> byId, HashSet<string> hidden)
        {
            List<SubProcessNode> containers = definition.Nodes.OfType<SubProcessNode>()
                .Where(n => !n.Collapsed && n.Id != null && !hidden.Contains(n.Id))
                .OrderByDescending(n => Depth(definition, n))
                .ToList();

            foreach (SubProcessNode container in containers)
            {
                if (!byId.TryGetValue(container.Id, out LaidOutNode? frame))
                {
                    continue;
                }

                List<LaidOutNode> children = definition.Nodes
                    .Where(n => n?.Id != null && n.ParentNodeId == container.Id && byId.ContainsKey(n.Id))
                    .Select(n => byId[n.Id])
                    .ToList();

                if (children.Count == 0)
                {
                    frame.Width = EmptyContainerWidth;
                    frame.Height = EmptyContainerHeight;
                    continue;
                }

                double left = children.Min(c => c.X);
                double top = children.Min(c => c.Y);
                double right = children.Max(c => c.X + c.Width);
                double bottom = children.Max(c => c.Y + c.Height);

                frame.X = left - ContainerPadding;
                frame.Y = top - ContainerPadding - ContainerHeader;
                frame.Width = (right - left) + (2 * ContainerPadding);
                frame.Height = (bottom - top) + (2 * ContainerPadding) + ContainerHeader;
            }
        }

        /// <summary>Die Verschachtelungstiefe eines Knotens (0 = oberste Ebene).</summary>
        private static int Depth(WorkflowDefinition definition, WorkflowNode? node)
        {
            int depth = 0;
            string? parent = node?.ParentNodeId;
            while (parent != null && depth <= definition.Nodes.Count)
            {
                depth++;
                parent = definition.GetNode(parent)?.ParentNodeId;
            }

            return depth;
        }

        /// <summary>
        /// Automatisches Layout, <b>ebenenweise</b>: erst die Knoten innerhalb der Abschnitte (von innen
        /// nach aussen), dann die oberste Ebene - dabei zaehlt ein aufgeklappter Abschnitt mit der Groesse,
        /// die seine Kinder ergeben haben.
        /// </summary>
        /// <remarks>
        /// Ohne diese Schachtelung waeren die Knoten eines Abschnitts ein zusammenhangloser Teilgraph:
        /// sie landeten in denselben Zeilen wie die aeusseren Knoten, und der Rahmen laege quer ueber
        /// allem.
        /// </remarks>
        private static IReadOnlyDictionary<string, (double X, double Y)> AutoLayout(
            WorkflowDefinition definition, HashSet<string> hidden)
        {
            var size = new Dictionary<string, (double W, double H)>(StringComparer.Ordinal);
            foreach (WorkflowNode node in definition.Nodes.Where(n => n?.Id != null))
            {
                size[node.Id] = SizeFor(node.Kind);
            }

            // Von innen nach aussen: die Groesse eines Abschnitts steht erst fest, wenn seine Kinder
            // platziert sind.
            var innerLayouts = new Dictionary<string, Dictionary<string, (double X, double Y)>>(
                StringComparer.Ordinal);
            foreach (SubProcessNode container in definition.Nodes.OfType<SubProcessNode>()
                         .Where(n => !n.Collapsed && n.Id != null && !hidden.Contains(n.Id))
                         .OrderByDescending(n => Depth(definition, n)))
            {
                Dictionary<string, (double X, double Y)> inner = LayoutLevel(definition, container.Id, size);
                innerLayouts[container.Id] = inner;

                if (inner.Count == 0)
                {
                    size[container.Id] = (EmptyContainerWidth, EmptyContainerHeight);
                    continue;
                }

                double right = inner.Max(p => p.Value.X + size[p.Key].W);
                double bottom = inner.Max(p => p.Value.Y + size[p.Key].H);
                size[container.Id] = (right + (2 * ContainerPadding),
                    bottom + (2 * ContainerPadding) + ContainerHeader);
            }

            var result = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, (double X, double Y)> pair in LayoutLevel(definition, null, size))
            {
                result[pair.Key] = pair.Value;
            }

            // Die Kinder in ihren Rahmen schieben - ebenfalls von aussen nach innen, damit ein
            // geschachtelter Abschnitt seine eigene Lage schon kennt.
            foreach (SubProcessNode container in definition.Nodes.OfType<SubProcessNode>()
                         .Where(n => n.Id != null && innerLayouts.ContainsKey(n.Id))
                         .OrderBy(n => Depth(definition, n)))
            {
                if (!result.TryGetValue(container.Id, out (double X, double Y) origin))
                {
                    continue;
                }

                foreach (KeyValuePair<string, (double X, double Y)> child in innerLayouts[container.Id])
                {
                    result[child.Key] = (origin.X + ContainerPadding + child.Value.X,
                        origin.Y + ContainerHeader + ContainerPadding + child.Value.Y);
                }
            }

            return result;
        }

        /// <summary>
        /// Platziert EINE Ebene (die Knoten mit demselben Behaelter) nach Laengster-Pfad-Schichtung -
        /// relativ zum Ursprung dieser Ebene.
        /// </summary>
        private static Dictionary<string, (double X, double Y)> LayoutLevel(WorkflowDefinition definition,
            string? parentNodeId, IReadOnlyDictionary<string, (double W, double H)> size)
        {
            List<WorkflowNode> level = definition.Nodes
                .Where(n => n?.Id != null && n.ParentNodeId == parentNodeId)
                .ToList();
            var ids = new HashSet<string>(level.Select(n => n.Id), StringComparer.Ordinal);
            var layer = level.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);

            int maxIterations = level.Count + 1;
            for (int i = 0; i < maxIterations; i++)
            {
                bool changed = false;
                foreach (SequenceFlow flow in definition.Flows)
                {
                    // Nur Kanten INNERHALB dieser Ebene schichten - eine Kante nach draussen wuerde die
                    // Ebene an der aeusseren Struktur ausrichten.
                    if (flow.SourceId != null && flow.TargetId != null
                        && ids.Contains(flow.SourceId) && ids.Contains(flow.TargetId)
                        && layer.TryGetValue(flow.SourceId, out int sl)
                        && layer.TryGetValue(flow.TargetId, out int tl) && tl < sl + 1)
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

            // Spalte und Zeile je Knoten festlegen (in Definitionsreihenfolge, damit dasselbe Modell
            // stabil dasselbe Bild ergibt).
            var rowInLayer = new Dictionary<int, int>();
            var cell = new Dictionary<string, (int Layer, int Row)>(StringComparer.Ordinal);
            foreach (WorkflowNode node in level)
            {
                int l = layer.TryGetValue(node.Id, out int lv) ? lv : 0;
                int row = rowInLayer.TryGetValue(l, out int r) ? r : 0;
                rowInLayer[l] = row + 1;
                cell[node.Id] = (l, row);
            }

            // Spaltenbreiten und Zeilenhoehen aus dem BREITESTEN bzw. HOECHSTEN Knoten darin - und
            // daraus kumulative Offsets. Mit einem festen Schritt je Knoten (so lief es frueher) schoebe
            // ein breiter Knoten die folgende Spalte nicht weit genug: ein aufgeklappter Abschnitt ist um
            // ein Vielfaches breiter als ein Schritt, und der naechste Knoten landete mitten in seinem
            // Rahmen.
            var columnWidth = new Dictionary<int, double>();
            var rowHeight = new Dictionary<int, double>();
            foreach (WorkflowNode node in level)
            {
                (double w, double h) = SizeOfNode(node, size);
                (int l, int row) = cell[node.Id];
                columnWidth[l] = Math.Max(columnWidth.TryGetValue(l, out double cw) ? cw : ColumnWidth, w);
                rowHeight[row] = Math.Max(rowHeight.TryGetValue(row, out double rh) ? rh : RowHeight, h);
            }

            var columnX = new Dictionary<int, double>();
            double x0 = Margin;
            foreach (int l in columnWidth.Keys.OrderBy(k => k))
            {
                columnX[l] = x0;
                x0 += columnWidth[l] + (LayerGap - ColumnWidth);
            }

            var rowY = new Dictionary<int, double>();
            double y0 = Margin;
            foreach (int r in rowHeight.Keys.OrderBy(k => k))
            {
                rowY[r] = y0;
                y0 += rowHeight[r] + (RowGap - RowHeight);
            }

            var result = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            foreach (WorkflowNode node in level)
            {
                (double w, double h) = SizeOfNode(node, size);
                (int l, int row) = cell[node.Id];
                // In der Zelle zentriert - so bleibt das Bild bei gleich grossen Knoten genau das von
                // vorher.
                result[node.Id] = (columnX[l] + ((columnWidth[l] - w) / 2),
                    rowY[row] + ((rowHeight[row] - h) / 2));
            }

            return result;
        }

        /// <summary>Die Groesse eines Knotens - fuer einen Abschnitt die vorab berechnete Rahmengroesse.</summary>
        private static (double W, double H) SizeOfNode(WorkflowNode node,
            IReadOnlyDictionary<string, (double W, double H)> size)
            => node.Id != null && size.TryGetValue(node.Id, out var s) ? s : SizeFor(node.Kind);

        /// <summary>
        /// Setzt jeden Fristen-Timer auf den unteren Rand des Schritts, an dem er haengt - halb
        /// ueberlappend, wie man ein Boundary-Event kennt. Mehrere Timer am selben Schritt werden von
        /// rechts nach links aufgereiht.
        /// </summary>
        /// <remarks>
        /// Die Position eines Fristen-Timers ist <b>abgeleitet</b>, nicht gezeichnet: sie folgt seinem
        /// Schritt. Deshalb gewinnt sie auch gegen ein explizites <c>Diagram</c> aus dem Editor - ein
        /// Timer, der neben seinem Schritt herumschwebt, waere schlicht falsch zu lesen. Haengt er an
        /// einem Schritt, den es nicht gibt, bleibt er, wo das Layout ihn hingelegt hat (der Validator
        /// meldet den Fall).
        /// </remarks>
        private static void DockBoundaryTimers(WorkflowDefinition definition,
            IReadOnlyDictionary<string, LaidOutNode> byId)
        {
            // Fristen und Nachrichten-Empfaenge teilen sich die untere KANTE ihres Schritts und deshalb
            // auch den Zaehler: zwei getrennte Laeufe legten den ersten Empfang genau auf die erste
            // Frist. Beide sind "was passiert, WAEHREND hier gearbeitet wird" - dass sie nebeneinander
            // aufreihen, ist die richtige Aussage.
            var perHost = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (WorkflowNode attached in definition.Nodes
                         .Where(n => n is BoundaryTimerNode or BoundaryMessageNode))
            {
                string? hostId = attached switch
                {
                    BoundaryTimerNode timer => timer.AttachedToNodeId,
                    BoundaryMessageNode message => message.AttachedToNodeId,
                    _ => null
                };

                if (attached.Id == null || hostId == null
                    || !byId.TryGetValue(attached.Id, out LaidOutNode? laid)
                    || !byId.TryGetValue(hostId, out LaidOutNode? host))
                {
                    continue;
                }

                int index = perHost.TryGetValue(hostId, out int n) ? n : 0;
                perHost[hostId] = index + 1;

                // Von der rechten unteren Ecke nach links: der erste sitzt eingerueckt, jeder weitere
                // eine Knotenbreite daneben.
                double step = laid.Width + 6;
                laid.X = host.X + host.Width - laid.Width - 12 - (index * step);
                laid.Y = host.Y + host.Height - (laid.Height / 2);
            }

            // Der Rueckabwicklungs-Pfad haengt genauso an seinem Schritt - aber an der LINKEN unteren
            // Ecke. Sonst saesse er auf demselben Platz wie ein Fristen-Timer, und die beiden Aussagen
            // ("wenn die Frist reisst" gegen "wenn zurueckgenommen wird") waeren im Bild nicht mehr
            // auseinanderzuhalten.
            var perCompensated = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CompensationNode handler in definition.Nodes.OfType<CompensationNode>())
            {
                if (handler.Id == null || handler.AttachedToNodeId == null
                    || !byId.TryGetValue(handler.Id, out LaidOutNode? laid)
                    || !byId.TryGetValue(handler.AttachedToNodeId, out LaidOutNode? host))
                {
                    continue;
                }

                int index = perCompensated.TryGetValue(handler.AttachedToNodeId, out int n) ? n : 0;
                perCompensated[handler.AttachedToNodeId] = index + 1;

                laid.X = host.X + 12 + (index * (laid.Width + 6));
                laid.Y = host.Y + host.Height - (laid.Height / 2);
            }
        }

        /// <summary>
        /// Die Zeichengroesse eines Knotens seiner Art. Oeffentlich, weil der Editor sie braucht, um
        /// einen abgelegten Knoten auf den Mauszeiger zu zentrieren - mit einer eigenen Annahme laege
        /// er bei den kleinen Arten (Gateway, Fristen-Timer) sichtbar daneben.
        /// </summary>
        /// <param name="kind">die Art des Knotens</param>
        /// <returns>Breite und Hoehe</returns>
        public static (double W, double H) SizeOf(NodeKind kind) => SizeFor(kind);

        private static (double W, double H) SizeFor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Start:
                case NodeKind.End:
                    return (46, 46);
                case NodeKind.ExclusiveGateway:
                case NodeKind.ParallelGateway:
                case NodeKind.EventGateway:
                case NodeKind.InclusiveGateway:
                    return (50, 50);
                case NodeKind.TerminateEnd:
                    // Wie das Ende: es IST ein Ende - nur ein durchgreifendes.
                    return (46, 46);
                case NodeKind.SidePathEnd:
                    // Etwas kleiner als das Ende: ein Nebenpfad-Abschluss ist die leisere Aussage.
                    return (38, 38);
                case NodeKind.Compensation:
                    // Wie der Fristen-Timer: er klebt am Rand seines Schritts.
                    return (44, 32);
                case NodeKind.BoundaryMessage:
                case NodeKind.BoundaryTimer:
                    // Klein, weil er am Rand seines Schritts klebt und ihn nicht verdecken soll - aber
                    // breiter als hoch, damit die Grundform ein kurzes Sechseck bleibt (bei gleicher
                    // Breite und Hoehe faellt sie zur Raute zusammen und sieht aus wie ein Gateway).
                    return (44, 32);
                default:
                    return (140, 54);
            }
        }

        /// <summary>
        /// Die Konturfarbe eines Knotens, oder null fuer die uebliche Linienfarbe.
        /// </summary>
        /// <remarks>
        /// Bisher nur der Fristen-Timer, und dort aus gutem Grund: <b>unterbrechend</b> oder nicht ist
        /// der groesste Unterschied, den zwei sonst gleich aussehende Knoten haben koennen - einmal
        /// laeuft eine Erinnerung nebenher, einmal wird der Schritt ABGEBROCHEN und die wartende
        /// Aufgabe verschwindet aus der Arbeitsliste. Das stand bisher nur im Eigenschaften-Popup; im
        /// Bild waren beide dasselbe Symbol.
        /// <para>
        /// Orange = greift in den Hauptfluss ein, Blau = laeuft nebenher. Bewusst nicht Rot: der
        /// unterbrechende Timer ist kein Fehlerpfad, und Rot ist im Graphen bereits vergeben.
        /// </para>
        /// </remarks>
        private static string? OutlineColorFor(WorkflowNode node)
        {
            if (node is not BoundaryTimerNode timer)
            {
                return null;
            }

            return timer.Interrupting ? "var(--mud-palette-warning)" : "var(--mud-palette-info)";
        }

        private static NodeShape ShapeFor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Start:
                case NodeKind.End:
                case NodeKind.TerminateEnd:
                    return NodeShape.Ellipse;
                case NodeKind.ExclusiveGateway:
                case NodeKind.ParallelGateway:
                case NodeKind.EventGateway:
                case NodeKind.InclusiveGateway:
                    return NodeShape.Diamond;
                case NodeKind.Wait:
                case NodeKind.Timer:
                case NodeKind.BoundaryTimer:
                case NodeKind.BoundaryMessage:
                case NodeKind.Compensation:
                // Sechseck wie die uebrigen Wartepunkte, und aus demselben Grund: der Zweig PARKT hier,
                // bis die Ruecknahme durch ist, und laeuft danach weiter. Bewusst KEIN Kreis - der
                // gehoert Start und Ende, und ein runder Knoten mitten im Fluss laese sich als
                // Endpunkt lesen. Der Ausloeser beendet aber nichts.
                case NodeKind.Compensate:
                    return NodeShape.Hexagon;
                case NodeKind.SidePathEnd:
                    // Wie das Ende - aber der Nebenpfad-Endpunkt beendet nur seinen Pfad, nicht die
                    // Instanz. Die Beschriftung unter dem Kreis macht den Unterschied lesbar.
                    return NodeShape.Ellipse;
                default:
                    return NodeShape.RoundedRectangle;
            }
        }

        /// <summary>
        /// Die waagrechte Einrueckung der beiden schraegen Sechseck-Kanten. Die Deckelung auf ein
        /// Viertel der Breite ist der Grund, warum auch ein kleines Sechseck eines bleibt: ohne sie
        /// waere die Einrueckung bei einem 44px breiten Knoten die halbe Breite - und die Form damit
        /// eine Raute.
        /// </summary>
        /// <param name="width">Breite des Knotens</param>
        /// <param name="height">Hoehe des Knotens</param>
        /// <returns>die Einrueckung in Zeichenkoordinaten</returns>
        public static double HexagonInset(double width, double height)
            => Math.Min(18, Math.Min(width / 4, height / 2));

        /// <summary>
        /// Beschriftung eines Knotens. Ein Join, der das Ergebnis seiner parallelen Region deklariert,
        /// bekommt denselben <c>{…}</c>-Marker wie eine Kante mit Mapping - sonst waere im Bild nicht zu
        /// sehen, dass hier nur ein Teil der Zweig-Ergebnisse weiterlaeuft.
        /// </summary>
        private static string NodeLabel(WorkflowNode node)
        {
            // Solange der Editor die Abschnitte nicht als Rahmen zeichnet, ist dieser Marker die einzige
            // Stelle, an der man sieht, dass ein Knoten INNEN liegt - sonst schwebt er frei im Bild und
            // wirkt wie ein Fehler.
            string inside = node.ParentNodeId != null ? "▸ " : string.Empty;
            string text = inside + (string.IsNullOrEmpty(node.Name) ? node.Id : node.Name);
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
            if (mapped)
            {
                return "{…} " + text;
            }

            // Ein knapper Indikator vor der Beschriftung macht die Art des Knotens erkennbar, auch wenn
            // mehrere Arten dieselbe Grundform teilen: Zahnrad = automatischer Schritt, Kette = Aufruf
            // eines Unter-Workflows (beide abgerundete Rechtecke); Uhr = Timer, Sanduhr = Signal-Wait
            // (beide Sechsecke) - hier betont er, WORAUF gewartet wird. Gleiche Idee wie das 👤 oben.
            return node.Kind switch
            {
                NodeKind.AutomatedActivity => "⚙️ " + text,
                NodeKind.CallWorkflow => "🔗 " + text,
                NodeKind.Timer => "🕐 " + text,
                NodeKind.Wait => "⏳ " + text,
                NodeKind.Compensate => "↺ " + text,
                // Briefumschlag gegen Sanduhr: senden und warten sind die beiden Seiten derselben
                // Sache, und im Bild muss sofort klar sein, welche man vor sich hat.
                NodeKind.SendMessage => "📨 " + text,
                // Der Abschnitt teilt die abgerundete Form mit Aktivitaet und Aufruf - das Rahmen-Zeichen
                // sagt, dass hier weitere Knoten drinstecken.
                NodeKind.SubProcess => "▣ " + text,
                // Der Fristen-Timer bekommt hier bewusst KEINEN Marker: seine Glocke steht in der Form
                // (LaidOutNode.Symbol), sein Name darunter. Als Praefix vorangestellt waere sie Teil
                // eines Textes, der neben einem 44px-Knoten stuende - deshalb dort und nicht hier.
                _ => text
            };
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
