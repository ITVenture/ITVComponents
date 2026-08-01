using System;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Graph
{
    /// <summary>Die Seite eines Knotens, an der eine Kante andockt.</summary>
    public enum PortSide
    {
        /// <summary>Linke Kante.</summary>
        Left,

        /// <summary>Obere Kante.</summary>
        Top,

        /// <summary>Rechte Kante.</summary>
        Right,

        /// <summary>Untere Kante.</summary>
        Bottom
    }

    /// <summary>Ein Punkt der Zeichenflaeche.</summary>
    public readonly struct GraphPoint
    {
        /// <summary>Erzeugt einen Punkt.</summary>
        /// <param name="x">X-Koordinate</param>
        /// <param name="y">Y-Koordinate</param>
        public GraphPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>X-Koordinate.</summary>
        public double X { get; }

        /// <summary>Y-Koordinate.</summary>
        public double Y { get; }
    }

    /// <summary>Ein achsenparalleles Rechteck (Knotenflaeche bzw. Hindernis).</summary>
    public readonly struct GraphRect
    {
        /// <summary>Erzeugt ein Rechteck.</summary>
        /// <param name="x">linke Kante</param>
        /// <param name="y">obere Kante</param>
        /// <param name="width">Breite</param>
        /// <param name="height">Hoehe</param>
        public GraphRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>Linke Kante.</summary>
        public double X { get; }

        /// <summary>Obere Kante.</summary>
        public double Y { get; }

        /// <summary>Breite.</summary>
        public double Width { get; }

        /// <summary>Hoehe.</summary>
        public double Height { get; }

        /// <summary>Linke Kante.</summary>
        public double Left => X;

        /// <summary>Obere Kante.</summary>
        public double Top => Y;

        /// <summary>Rechte Kante.</summary>
        public double Right => X + Width;

        /// <summary>Untere Kante.</summary>
        public double Bottom => Y + Height;

        /// <summary>Mittelpunkt X.</summary>
        public double CenterX => X + (Width / 2);

        /// <summary>Mittelpunkt Y.</summary>
        public double CenterY => Y + (Height / 2);

        /// <summary>Erzeugt ein um <paramref name="d"/> nach allen Seiten vergroessertes Rechteck.</summary>
        /// <param name="d">der Zuschlag je Seite</param>
        public GraphRect Inflate(double d) => new GraphRect(X - d, Y - d, Width + (2 * d), Height + (2 * d));

        /// <summary>Prueft, ob sich zwei Rechtecke ueberschneiden.</summary>
        /// <param name="other">das andere Rechteck</param>
        public bool Intersects(GraphRect other)
            => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
    }

    /// <summary>
    /// Berechnet den Verlauf einer Kante als rechtwinkligen Streckenzug: nur waagrechte und senkrechte
    /// Teilstuecke (Ecken erlaubt), und kein Teilstueck laeuft durch einen Knoten. Linien duerfen
    /// einander kreuzen und sich teilweise ueberlagern - vollstaendige Deckungsgleichheit verhindert
    /// nicht der Router, sondern die Spurvergabe in <see cref="GraphLayout"/>: zwei Kanten, die
    /// dieselbe Knotenseite benutzen, docken an verschiedenen Punkten an und bleiben dadurch auf ihrem
    /// eindeutigen Anfangs- bzw. Endstueck unterscheidbar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verfahren: um jeden Knoten wird ein Sicherheitsrand (<see cref="Clearance"/>) gelegt; die
    /// Kanten dieser vergroesserten Rechtecke bilden ein duennes Koordinatengitter, auf dem ein A*
    /// den billigsten Weg sucht. Kosten = Weglaenge + Aufschlag je Richtungswechsel, damit das
    /// Ergebnis so wenige Ecken wie moeglich hat.
    /// </para>
    /// <para>
    /// Bewusst reine Berechnung ohne Blazor-Bezug (wie <see cref="GraphLayout"/>), damit die
    /// Zusicherungen "rechtwinklig" und "kreuzt keinen Knoten" testbar sind statt nur behauptet.
    /// Eine strukturgleiche Fassung liegt in <c>wwwroot/graph-editor.js</c> - der Editor zeichnet
    /// waehrend des Ziehens client-seitig neu und muss dieselben Wege ergeben.
    /// </para>
    /// </remarks>
    public static class EdgeRouter
    {
        /// <summary>Abstand, den eine Linie zu jedem Knoten haelt.</summary>
        public const double Clearance = 14;

        /// <summary>
        /// Laenge des geraden Endstuecks VOR dem Zielknoten - dort, wo die Pfeilspitze sitzt.
        /// </summary>
        /// <remarks>
        /// Bewusst laenger als <see cref="Clearance"/>: der Marker wird in Vielfachen der Strichstaerke
        /// gezeichnet (SVG-Standard <c>markerUnits="strokeWidth"</c>), seine 8 Einheiten sind bei
        /// Strichstaerke 1.5 also 12 und bei einer ausgewaehlten Kante (2.5) rund 20 Pixel. Mit einem
        /// Endstueck von 14 blieben davon wenige Pixel gerade Linie uebrig - der Pfeil sass praktisch
        /// auf der letzten Ecke und die Richtung war nicht mehr abzulesen. Der Selbstbezug
        /// (<see cref="RouteSelfLoop"/>) faehrt aus demselben Grund schon immer mit
        /// <c>2 * Clearance</c> ein.
        /// </remarks>
        public const double TargetStub = 2 * Clearance;

        /// <summary>Aufschlag je Richtungswechsel - macht wenige Ecken billiger als kurze Wege.</summary>
        private const double TurnPenalty = 30;

        /// <summary>Zusaetzliche Gasse aussen herum, damit ein Umweg um alles herum moeglich bleibt.</summary>
        private const double EscapeMargin = 40;

        /// <summary>Nur Hindernisse in diesem Umkreis der beiden Enden werden beruecksichtigt.</summary>
        private const double LocalMargin = 160;

        /// <summary>
        /// Obergrenze der Gitterzellen. Das Layout wird bei jedem Render neu berechnet; oberhalb dieser
        /// Groesse waere die Suche teurer als der Gewinn, und es wird auf die einfache Z-Fuehrung
        /// zurueckgefallen (die kann dann durch Knoten laufen - sichtbar, aber nicht falsch).
        /// </summary>
        private const int MaxGridCells = 4000;

        private const double Eps = 0.5;

        /// <summary>Richtungen: 0 = rechts, 1 = links, 2 = runter, 3 = hoch.</summary>
        private static readonly (int Dx, int Dy)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>
        /// Berechnet den Streckenzug einer Kante vom Rand des Quellknotens zum Rand des Zielknotens.
        /// </summary>
        /// <param name="source">Quellknoten</param>
        /// <param name="sourceSide">Seite, an der die Kante den Quellknoten verlaesst</param>
        /// <param name="sourceOffset">Versatz des Andockpunktes gegenueber der Seitenmitte (Spur)</param>
        /// <param name="target">Zielknoten</param>
        /// <param name="targetSide">Seite, an der die Kante den Zielknoten erreicht</param>
        /// <param name="targetOffset">Versatz des Andockpunktes gegenueber der Seitenmitte (Spur)</param>
        /// <param name="obstacles">alle Knotenflaechen (Quelle und Ziel eingeschlossen)</param>
        /// <returns>mindestens zwei Punkte; aufeinanderfolgende Punkte sind immer achsenparallel verbunden</returns>
        public static IReadOnlyList<GraphPoint> Route(
            GraphRect source, PortSide sourceSide, double sourceOffset,
            GraphRect target, PortSide targetSide, double targetOffset,
            IReadOnlyList<GraphRect> obstacles)
        {
            GraphPoint p = Anchor(source, sourceSide, sourceOffset);
            GraphPoint q = Anchor(target, targetSide, targetOffset);
            (p, q) = Align(source, sourceSide, p, target, targetSide, q);
            GraphPoint s = Advance(p, sourceSide, Clearance);
            // Zielseite laenger: dort steht die Pfeilspitze (siehe TargetStub). Der Punkt liegt damit
            // ausserhalb des aufgeblasenen Hindernis-Rechtecks statt genau auf dessen Rand - fuer die
            // Wegsuche unkritisch, sie sucht ohnehin nur ab hier.
            GraphPoint t = Advance(q, targetSide, TargetStub);

            IReadOnlyList<GraphPoint> middle = FindPath(s, sourceSide, t, targetSide, obstacles)
                                              ?? Fallback(s, sourceSide, t, targetSide);

            var points = new List<GraphPoint>(middle.Count + 2) { p };
            points.AddRange(middle);
            points.Add(q);
            return Simplify(points);
        }

        /// <summary>
        /// Der Weg einer Kante, die auf ihren eigenen Knoten zurueckfuehrt (Wiederholung). Fest
        /// gefuehrt: rechts raus, oben herum, oben wieder rein - ein A* haette hier keine Aufgabe.
        /// </summary>
        /// <param name="node">der Knoten</param>
        public static IReadOnlyList<GraphPoint> RouteSelfLoop(GraphRect node)
        {
            double outX = node.Right + (2 * Clearance);
            double topY = node.Top - (2 * Clearance);
            double y = node.CenterY - (node.Height / 4);
            return new[]
            {
                new GraphPoint(node.Right, y),
                new GraphPoint(outX, y),
                new GraphPoint(outX, topY),
                new GraphPoint(node.CenterX, topY),
                new GraphPoint(node.CenterX, node.Top)
            };
        }

        /// <summary>
        /// Waehlt die Seiten, an denen eine Kante andockt: die Achse mit dem groesseren Abstand
        /// entscheidet, damit die Linie in die Richtung zeigt, in der die Knoten tatsaechlich
        /// auseinanderliegen.
        /// </summary>
        /// <param name="source">Quellknoten</param>
        /// <param name="target">Zielknoten</param>
        public static (PortSide Source, PortSide Target) ChooseSides(GraphRect source, GraphRect target)
        {
            double dx = target.CenterX - source.CenterX;
            double dy = target.CenterY - source.CenterY;
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return dx >= 0 ? (PortSide.Right, PortSide.Left) : (PortSide.Left, PortSide.Right);
            }

            return dy >= 0 ? (PortSide.Bottom, PortSide.Top) : (PortSide.Top, PortSide.Bottom);
        }

        /// <summary>
        /// Gibt an, ob eine Kante diese Seite senkrecht verlaesst (Ober-/Unterkante). Das entscheidet
        /// zugleich die Achse der Spurversetzung: senkrechter Abgang =&gt; Spuren liegen in X.
        /// </summary>
        /// <param name="side">die Seite</param>
        public static bool LeavesVertically(PortSide side) => side is PortSide.Top or PortSide.Bottom;

        /// <summary>Der Andockpunkt auf dem Knotenrand, um <paramref name="offset"/> aus der Mitte versetzt.</summary>
        /// <param name="r">der Knoten</param>
        /// <param name="side">die Seite</param>
        /// <param name="offset">Versatz gegenueber der Seitenmitte</param>
        public static GraphPoint Anchor(GraphRect r, PortSide side, double offset)
        {
            switch (side)
            {
                case PortSide.Left:
                    return new GraphPoint(r.Left, Along(r.CenterY + offset, r.Top, r.Bottom));
                case PortSide.Right:
                    return new GraphPoint(r.Right, Along(r.CenterY + offset, r.Top, r.Bottom));
                case PortSide.Top:
                    return new GraphPoint(Along(r.CenterX + offset, r.Left, r.Right), r.Top);
                default:
                    return new GraphPoint(Along(r.CenterX + offset, r.Left, r.Right), r.Bottom);
            }
        }

        /// <summary>
        /// Zieht die beiden Andockpunkte auf eine Linie, wenn sie ohnehin fast fluchten. Ohne das
        /// entstuende bei zwei Knoten, deren Mitten um wenige Pixel auseinanderliegen, ein Z mit einem
        /// winzigen Versatz - das sieht nicht nach Absicht aus, sondern nach kaputter Zeichnung.
        /// Verschoben wird nur das Zielende, und nur solange es auf seiner Seite Platz hat.
        /// </summary>
        private static (GraphPoint P, GraphPoint Q) Align(GraphRect source, PortSide sourceSide, GraphPoint p,
            GraphRect target, PortSide targetSide, GraphPoint q)
        {
            const double snap = 10;
            if (LeavesVertically(sourceSide) != LeavesVertically(targetSide))
            {
                return (p, q);
            }

            if (LeavesVertically(sourceSide))
            {
                if (Math.Abs(p.X - q.X) <= snap && Math.Abs(Along(p.X, target.Left, target.Right) - p.X) <= 1e-6)
                {
                    return (p, new GraphPoint(p.X, q.Y));
                }

                return (p, q);
            }

            if (Math.Abs(p.Y - q.Y) <= snap && Math.Abs(Along(p.Y, target.Top, target.Bottom) - p.Y) <= 1e-6)
            {
                return (p, new GraphPoint(q.X, p.Y));
            }

            return (p, q);
        }

        /// <summary>Haelt den Andockpunkt von den Ecken der Seite fern.</summary>
        private static double Along(double value, double lo, double hi)
        {
            double inset = Math.Min(6, (hi - lo) / 3);
            double min = lo + inset;
            double max = hi - inset;
            return value < min ? min : value > max ? max : value;
        }

        private static GraphPoint Advance(GraphPoint p, PortSide side, double d)
        {
            switch (side)
            {
                case PortSide.Left:
                    return new GraphPoint(p.X - d, p.Y);
                case PortSide.Right:
                    return new GraphPoint(p.X + d, p.Y);
                case PortSide.Top:
                    return new GraphPoint(p.X, p.Y - d);
                default:
                    return new GraphPoint(p.X, p.Y + d);
            }
        }

        private static int OutwardDir(PortSide side) => side switch
        {
            PortSide.Right => 0,
            PortSide.Left => 1,
            PortSide.Bottom => 2,
            _ => 3
        };

        private static IReadOnlyList<GraphPoint>? FindPath(GraphPoint s, PortSide sourceSide,
            GraphPoint t, PortSide targetSide, IReadOnlyList<GraphRect> obstacles)
        {
            GraphRect bounds = Bounds(s, t, LocalMargin);
            var blocked = new List<GraphRect>();
            foreach (GraphRect o in obstacles)
            {
                GraphRect inflated = o.Inflate(Clearance);
                if (inflated.Intersects(bounds))
                {
                    blocked.Add(inflated);
                }
            }

            // Der haeufigste Fall: die beiden Enden liegen auf einer Linie und nichts steht dazwischen.
            if ((Math.Abs(s.X - t.X) <= Eps || Math.Abs(s.Y - t.Y) <= Eps) && IsClear(s, t, blocked))
            {
                return new[] { s, t };
            }

            List<double> xs = Axis(blocked, true, s.X, t.X, bounds);
            List<double> ys = Axis(blocked, false, s.Y, t.Y, bounds);
            if (xs.Count * ys.Count > MaxGridCells)
            {
                return null;
            }

            int nx = xs.Count;
            int ny = ys.Count;
            int si = IndexOf(xs, s.X);
            int sj = IndexOf(ys, s.Y);
            int ti = IndexOf(xs, t.X);
            int tj = IndexOf(ys, t.Y);
            if (si < 0 || sj < 0 || ti < 0 || tj < 0)
            {
                return null;
            }

            int states = nx * ny * 4;
            var cost = new double[states];
            var from = new int[states];
            var done = new bool[states];
            for (int i = 0; i < states; i++)
            {
                cost[i] = double.PositiveInfinity;
                from[i] = -1;
            }

            int start = State(si, sj, OutwardDir(sourceSide), ny);
            cost[start] = 0;
            int forbidden = OutwardDir(targetSide);
            double tx = xs[ti];
            double ty = ys[tj];

            var queue = new PriorityQueue<int, double>();
            queue.Enqueue(start, Math.Abs(s.X - tx) + Math.Abs(s.Y - ty));

            int goal = -1;
            while (queue.TryDequeue(out int state, out _))
            {
                if (done[state])
                {
                    continue;
                }

                done[state] = true;
                int ix = (state / 4) / ny;
                int iy = (state / 4) % ny;
                int dir = state % 4;

                if (ix == ti && iy == tj && dir != forbidden)
                {
                    goal = state;
                    break;
                }

                var a = new GraphPoint(xs[ix], ys[iy]);
                for (int d = 0; d < 4; d++)
                {
                    int jx = ix + Dirs[d].Dx;
                    int jy = iy + Dirs[d].Dy;
                    if (jx < 0 || jx >= nx || jy < 0 || jy >= ny)
                    {
                        continue;
                    }

                    var b = new GraphPoint(xs[jx], ys[jy]);
                    if (!IsClear(a, b, blocked))
                    {
                        continue;
                    }

                    int next = State(jx, jy, d, ny);
                    double g = cost[state] + Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y)
                               + (d == dir ? 0 : TurnPenalty);
                    if (g + 1e-9 < cost[next])
                    {
                        cost[next] = g;
                        from[next] = state;
                        queue.Enqueue(next, g + Math.Abs(b.X - tx) + Math.Abs(b.Y - ty));
                    }
                }
            }

            if (goal < 0)
            {
                return null;
            }

            var path = new List<GraphPoint>();
            for (int st = goal; st >= 0; st = from[st])
            {
                path.Add(new GraphPoint(xs[(st / 4) / ny], ys[(st / 4) % ny]));
            }

            path.Reverse();
            return path;
        }

        private static int State(int ix, int iy, int dir, int ny) => (((ix * ny) + iy) * 4) + dir;

        /// <summary>
        /// Die Stuetzkoordinaten einer Achse: die Raender aller (vergroesserten) Hindernisse - dort
        /// entlang laeuft eine Linie knapp an einem Knoten vorbei - plus die beiden Endpunkte und
        /// je eine Gasse aussen herum.
        /// </summary>
        private static List<double> Axis(List<GraphRect> blocked, bool horizontal, double a, double b, GraphRect bounds)
        {
            var values = new List<double>((blocked.Count * 2) + 4);
            foreach (GraphRect r in blocked)
            {
                values.Add(horizontal ? r.Left : r.Top);
                values.Add(horizontal ? r.Right : r.Bottom);
            }

            values.Add((horizontal ? bounds.Left : bounds.Top) - EscapeMargin);
            values.Add((horizontal ? bounds.Right : bounds.Bottom) + EscapeMargin);
            values.Sort();

            var result = new List<double>(values.Count + 2);
            foreach (double v in values)
            {
                if (result.Count == 0 || v - result[result.Count - 1] > Eps)
                {
                    result.Add(v);
                }
            }

            Ensure(result, a);
            Ensure(result, b);
            return result;
        }

        /// <summary>
        /// Nimmt eine Koordinate in die Stuetzliste auf. Liegt bereits ein Wert dicht daneben, wird
        /// dieser auf den exakten Wert gezogen - sonst entstuende am Endpunkt ein Knick von unter
        /// einem Pixel, der als ausgefranste Linie sichtbar wird.
        /// </summary>
        private static void Ensure(List<double> sorted, double value)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                if (Math.Abs(sorted[i] - value) <= Eps)
                {
                    sorted[i] = value;
                    return;
                }

                if (sorted[i] > value)
                {
                    sorted.Insert(i, value);
                    return;
                }
            }

            sorted.Add(value);
        }

        private static int IndexOf(List<double> sorted, double value)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                if (Math.Abs(sorted[i] - value) <= Eps)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Prueft, ob das (achsenparallele) Teilstueck frei ist. Verglichen wird gegen das offene
        /// Innere der Hindernisse: entlang eines Randes darf eine Linie laufen, hindurch nicht.
        /// </summary>
        private static bool IsClear(GraphPoint a, GraphPoint b, List<GraphRect> blocked)
        {
            double minX = Math.Min(a.X, b.X);
            double maxX = Math.Max(a.X, b.X);
            double minY = Math.Min(a.Y, b.Y);
            double maxY = Math.Max(a.Y, b.Y);
            foreach (GraphRect r in blocked)
            {
                if (minX < r.Right - Eps && maxX > r.Left + Eps && minY < r.Bottom - Eps && maxY > r.Top + Eps)
                {
                    return false;
                }
            }

            return true;
        }

        private static GraphRect Bounds(GraphPoint a, GraphPoint b, double margin)
        {
            double left = Math.Min(a.X, b.X) - margin;
            double top = Math.Min(a.Y, b.Y) - margin;
            double right = Math.Max(a.X, b.X) + margin;
            double bottom = Math.Max(a.Y, b.Y) + margin;
            return new GraphRect(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// Notfuehrung, wenn die Suche nicht laeuft (zu grosses Gitter oder kein Weg): ein Z bzw. ein
        /// L. Rechtwinklig ist sie damit weiterhin, ausweichen kann sie nicht.
        /// </summary>
        private static IReadOnlyList<GraphPoint> Fallback(GraphPoint s, PortSide sourceSide, GraphPoint t, PortSide targetSide)
        {
            bool sourceVertical = LeavesVertically(sourceSide);
            bool targetVertical = LeavesVertically(targetSide);
            if (!sourceVertical && !targetVertical)
            {
                double mid = (s.X + t.X) / 2;
                return new[] { s, new GraphPoint(mid, s.Y), new GraphPoint(mid, t.Y), t };
            }

            if (sourceVertical && targetVertical)
            {
                double mid = (s.Y + t.Y) / 2;
                return new[] { s, new GraphPoint(s.X, mid), new GraphPoint(t.X, mid), t };
            }

            return sourceVertical
                ? new[] { s, new GraphPoint(s.X, t.Y), t }
                : new[] { s, new GraphPoint(t.X, s.Y), t };
        }

        /// <summary>Entfernt doppelte und auf einer Geraden liegende Zwischenpunkte.</summary>
        private static IReadOnlyList<GraphPoint> Simplify(List<GraphPoint> points)
        {
            var result = new List<GraphPoint>(points.Count);
            foreach (GraphPoint p in points)
            {
                if (result.Count > 0)
                {
                    GraphPoint last = result[result.Count - 1];
                    if (Math.Abs(last.X - p.X) <= 1e-6 && Math.Abs(last.Y - p.Y) <= 1e-6)
                    {
                        continue;
                    }
                }

                result.Add(p);
            }

            for (int i = result.Count - 2; i >= 1; i--)
            {
                GraphPoint a = result[i - 1];
                GraphPoint b = result[i];
                GraphPoint c = result[i + 1];
                bool collinear = (Math.Abs(a.X - b.X) <= 1e-6 && Math.Abs(b.X - c.X) <= 1e-6)
                                 || (Math.Abs(a.Y - b.Y) <= 1e-6 && Math.Abs(b.Y - c.Y) <= 1e-6);
                if (collinear)
                {
                    result.RemoveAt(i);
                }
            }

            return result;
        }
    }
}
