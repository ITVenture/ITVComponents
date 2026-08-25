using System.Globalization;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Graph
{
    /// <summary>
    /// Die Eckpunkte der Knoten-Formen, die im Graphen vorkommen - gemeinsam fuer die Ansicht und den
    /// Editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ansicht und Editor erzeugen <b>nicht</b> dasselbe SVG, und das ist richtig so: die Ansicht
    /// zeichnet in absoluten Koordinaten, der Editor am Ursprung innerhalb einer verschobenen Gruppe
    /// (<c>transform="translate(x,y)"</c>) und mit <c>data-*</c>-Merkmalen, weil an ihnen das Ziehen und
    /// das Interop haengen. Dieselbe Unterscheidung wie beim doppelten Kanten-Routing.
    /// </para>
    /// <para>
    /// Was beide teilen, ist die <b>Geometrie</b> - und genau die ist der Teil, dessen Auseinanderlaufen
    /// niemand bemerkt: eine Raute mit einer anderen Spitze faellt nicht als Fehler auf, sondern
    /// hoechstens als "sieht im Editor irgendwie anders aus". Der Versatz (<c>x</c>/<c>y</c>) ist dabei
    /// der einzige Unterschied zwischen beiden Aufrufern, also ist er ein Parameter.
    /// </para>
    /// <para>
    /// Die Einbuchtung des Sechsecks lag schon vorher gemeinsam in
    /// <see cref="GraphLayout.HexagonInset"/> - dieser Typ zieht nach, was daneben stehen geblieben war.
    /// </para></remarks>
    public static class SvgShapes
    {
        /// <summary>
        /// Die vier Eckpunkte einer Raute (Gateway): oben, rechts, unten, links.
        /// </summary>
        /// <param name="x">linke Kante des umschliessenden Rechtecks</param>
        /// <param name="y">obere Kante des umschliessenden Rechtecks</param>
        /// <param name="width">Breite</param>
        /// <param name="height">Hoehe</param>
        public static string Diamond(double x, double y, double width, double height)
        {
            double cx = x + (width / 2);
            double cy = y + (height / 2);
            return string.Join(" ",
                $"{N(cx)},{N(y)}",
                $"{N(x + width)},{N(cy)}",
                $"{N(cx)},{N(y + height)}",
                $"{N(x)},{N(cy)}");
        }

        /// <summary>
        /// Die sechs Eckpunkte eines Sechsecks (Subprozess): oben links und rechts der Einbuchtung, die
        /// rechte Spitze, unten rechts und links, die linke Spitze.
        /// </summary>
        /// <param name="x">linke Kante des umschliessenden Rechtecks</param>
        /// <param name="y">obere Kante des umschliessenden Rechtecks</param>
        /// <param name="width">Breite</param>
        /// <param name="height">Hoehe</param>
        public static string Hexagon(double x, double y, double width, double height)
        {
            double k = GraphLayout.HexagonInset(width, height);
            double cy = y + (height / 2);
            return string.Join(" ",
                $"{N(x + k)},{N(y)}",
                $"{N(x + width - k)},{N(y)}",
                $"{N(x + width)},{N(cy)}",
                $"{N(x + width - k)},{N(y + height)}",
                $"{N(x + k)},{N(y + height)}",
                $"{N(x)},{N(cy)}");
        }

        /// <summary>
        /// Zahlen fuer SVG immer mit Punkt als Trennzeichen - in einer Umgebung mit deutschem oder
        /// franzoesischem Gebietsschema waere ein Komma sonst ein zweiter Koordinaten-Trenner, und die
        /// Form zerfiele.
        /// </summary>
        private static string N(double value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
