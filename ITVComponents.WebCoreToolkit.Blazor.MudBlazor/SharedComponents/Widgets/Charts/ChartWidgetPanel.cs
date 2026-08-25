using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using MudBlazor;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets.Charts
{
    /// <summary>
    /// One chart of a tile: its declaration, its bound parameters and what was wrong with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eine Konfiguration darf ein Diagramm beschreiben oder mehrere. Der Unterschied endet hier: beide
    /// Renderer bauen aus ihren Eintraegen dieselbe Liste, und die Ansicht zeichnet sie.
    /// </para>
    /// <para>
    /// Die Fehler haengen am EINZELNEN Diagramm und nicht an der Kachel. Das ist der Punkt, um den es
    /// geht: ein Tippfehler im dritten Diagramm soll die beiden ersten nicht mit ausloeschen - sonst
    /// nimmt eine kleine Unachtsamkeit die ganze Kachel mit, und was noch richtig ist, sieht man nicht
    /// mehr. Was die ganze Kachel betrifft (unlesbares JSON, ein Skript, das wirft), bleibt Sache des
    /// Renderers und steht weiterhin oben.
    /// </para>
    /// </remarks>
    public sealed class ChartWidgetPanel
    {
        /// <summary>What to draw. Null means: this entry could not be read - see <see cref="Errors"/>.</summary>
        public ChartWidgetDeclaration? Declaration { get; init; }

        /// <summary>The pass-through parameters, already checked and converted.</summary>
        public Dictionary<string, object?> Parameters { get; init; } = new();

        /// <summary>Everything that was wrong with THIS entry. Empty = nothing.</summary>
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

        /// <summary>Die Beschriftungen als Zeichenketten - was die Diagramm-Komponente entgegennimmt.</summary>
        public string[] LabelTexts { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Die Texte im Diagramm als fertiges SVG-Markup - leer, wenn es keine gibt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Warum als Text und nicht als Markup in der Ansicht:</b> Razor deutet <c>&lt;text&gt;</c> als
        /// eigenes STEUER-Tag (es dient dort dazu, blossen Inhalt ohne umschliessendes Element
        /// auszugeben) und lehnt Attribute daran ab - <c>RZ1023</c>. Dieselbe Stelle steht schon in
        /// <c>WorkflowGraph.razor</c>, aus genau diesem Grund. Ein SVG-<c>&lt;text&gt;</c> muss also als
        /// Zeichenkette entstehen.
        /// </para>
        /// <para>
        /// <b>Damit liegt das Kodieren hier</b> - und das ist der Preis dafuer, dass die Deklaration aus
        /// der Datenbank kommen darf. Text UND Attributwerte gehen durch
        /// <see cref="WebUtility.HtmlEncode(string)"/>; nichts davon wird durchgereicht.
        /// </para></remarks>
        public string OverlayMarkup { get; init; } = string.Empty;

        /// <summary>
        /// The width this chart was declared with, if it is an absolute length - otherwise null.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Wer <c>width: "150px"</c> schreibt, meint das Diagramm UND den Platz, den es einnimmt. Ohne diese
        /// Auskunft nahm der umgebende Platz weiter seine Mindestbreite plus allen Restplatz: der Mantel war
        /// viel breiter als das Diagramm, und die mittig gesetzte Ueberschrift stand irgendwo, nur nicht
        /// darueber.
        /// </para>
        /// <para>
        /// Nur die Breite und nicht auch die Hoehe: wie hoch ein Platz ist, ergibt sich aus seiner ZEILE -
        /// die Diagramme einer Zeile sind gleich hoch, damit sie auf einer Linie stehen (siehe
        /// <c>ChartWidgetView.CaptionStyle</c>). Eine deklarierte Hoehe gehoert deshalb dem Diagramm allein.
        /// </para>
        /// </remarks>
        public string? DeclaredWidth => AbsoluteLength(nameof(MudChart<double>.Width));

        /// <summary>
        /// Reads a passed-through size as an absolute CSS length.
        /// </summary>
        /// <param name="parameter">the parameter name of the chart component</param>
        /// <returns>the length, or null when there is none or it is relative</returns>
        /// <remarks>
        /// <para>
        /// Nur ABSOLUTE Laengen: eine relative Angabe (<c>80%</c> - und das ist die Vorgabe der
        /// Diagramm-Komponente) rechnet gegen den Elternteil. Wuerde sich der Elternteil dann nach ihr
        /// richten, waere die Rechnung zirkulaer, und was dabei herauskommt, entscheidet der Browser. Ohne
        /// absolute Angabe bleibt es deshalb beim bisherigen Verhalten.
        /// </para>
        /// <para>
        /// Eine nackte Zahl ist als Pixel gemeint - so liest sie auch das <c>svg</c>-Attribut, an dem die
        /// Angabe landet.
        /// </para>
        /// </remarks>
        private string? AbsoluteLength(string parameter)
        {
            if (!Parameters.TryGetValue(parameter, out object? raw) || raw == null)
            {
                return null;
            }

            string text = (Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
            if (text.Length == 0 || text.Contains('%', StringComparison.Ordinal))
            {
                return null;
            }

            // Schluesselwoerter beschreiben keine Groesse, sondern ein Verhalten - daraus laesst sich kein
            // Platz ableiten.
            if (RelativeKeywords.Contains(text))
            {
                return null;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? FormattableString.Invariant($"{number}px")
                : text;
        }

        /// <summary>
        /// Baut die <c>&lt;text&gt;</c>-Elemente der Diagramm-Texte. Siehe <see cref="OverlayMarkup"/>,
        /// warum das hier und nicht in der Ansicht geschieht.
        /// </summary>
        internal static string BuildOverlay(IReadOnlyList<ChartWidgetOverlayText>? overlay)
        {
            if (overlay == null || overlay.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (ChartWidgetOverlayText item in overlay)
            {
                sb.Append("<text x=\"").Append(Enc(item.PosX))
                  .Append("\" y=\"").Append(Enc(item.PosY))
                  .Append("\" text-anchor=\"").Append(Enc(item.Anchor))
                  .Append('"');
                if (!string.IsNullOrWhiteSpace(item.Class))
                {
                    sb.Append(" class=\"").Append(Enc(item.Class)).Append('"');
                }

                sb.Append('>').Append(Enc(item.Text)).Append("</text>");
            }

            return sb.ToString();
        }

        private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        private static readonly HashSet<string> RelativeKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "auto", "inherit", "initial", "unset", "revert", "fit-content", "max-content", "min-content", "none"
        };

        /// <summary>
        /// Turns the entries of a configuration into one panel each.
        /// </summary>
        /// <param name="entries">the entries - a map per chart, whatever the renderer's language</param>
        /// <returns>one panel per entry, in the order they were declared</returns>
        /// <remarks>
        /// Kein Eintrag faellt weg: was sich nicht lesen laesst, wird zu einem Platz mit Meldung. Ein
        /// stillschweigend uebersprungener Eintrag waere sonst genau die Sorte Fehler, bei der man die
        /// Konfiguration fuer richtig haelt und das fehlende Diagramm fuer ein Darstellungsproblem.
        /// </remarks>
        public static IReadOnlyList<ChartWidgetPanel> Build(IEnumerable<object?> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var panels = new List<ChartWidgetPanel>();
            foreach (object? entry in entries)
            {
                panels.Add(Build(entry, panels.Count));
            }

            if (panels.Count == 0)
            {
                panels.Add(new ChartWidgetPanel
                {
                    Errors = new[] { "The configuration produced no chart declaration." }
                });
            }

            return panels;
        }

        private static ChartWidgetPanel Build(object? entry, int index)
        {
            var errors = new List<string>();

            // Ein Fall genuegt - siehe die Anmerkung an ChartWidgetDeclaration.AsMap: das ObjectLiteral aus
            // CScript IST ein IDictionary<string, object>.
            if (entry is not IDictionary<string, object?> map)
            {
                string what = entry == null ? "nothing" : entry.GetType().Name;
                return new ChartWidgetPanel
                {
                    Errors = new[] { $"Chart {index + 1} of the configuration is a {what}, not a declaration." }
                };
            }

            ChartWidgetDeclaration? declaration = ChartWidgetDeclaration.FromMap(map, errors);
            if (declaration == null)
            {
                return new ChartWidgetPanel { Errors = errors };
            }

            Dictionary<string, object?> parameters =
                ChartParameterBinder.Bind(declaration.Extra, typeof(MudChart<double>), errors);

            return new ChartWidgetPanel
            {
                Declaration = declaration,
                Parameters = parameters,
                Errors = errors,
                LabelTexts = declaration.Labels.Select(l => l.Text).ToArray(),
                OverlayMarkup = BuildOverlay(declaration.Overlay)
            };
        }
    }
}
