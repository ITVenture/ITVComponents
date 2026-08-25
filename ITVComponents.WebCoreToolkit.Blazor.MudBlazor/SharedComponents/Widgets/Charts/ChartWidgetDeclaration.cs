using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MudBlazor;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets.Charts
{
    /// <summary>One category of a chart - its caption and, optionally, where a click on it leads.</summary>
    public sealed class ChartWidgetLabel
    {
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Where a click on this category navigates. Relative targets are resolved against the application's
        /// base - das ist die Falle aus BUG-PRE141: ein root-absolutes Ziel geht in einer
        /// mandanten-praefixierten Anwendung am Praefix vorbei.
        /// </summary>
        public string? NavigateTo { get; init; }
    }

    /// <summary>
    /// Ein Text, der IM Diagramm steht - fuer die Mitte eines Rings die uebliche Verwendung.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bewusst eine Liste positionierter Eintraege und kein festes "gross oben, klein unten": drei oder
    /// vier Zeilen sind genauso plausibel, und ein festes Schema muesste beim ersten solchen Wunsch
    /// aufgebrochen werden.
    /// </para>
    /// <para>
    /// <b>Und bewusst kein freies SVG:</b> die Deklaration kommt aus der Datenbank, und Markup von dort
    /// ungeprueft in die Seite zu geben ist das Muster, das einem spaeter auf die Fuesse faellt. Der Text
    /// wird kodiert in ein <c>&lt;text&gt;</c>-Element geschrieben - deshalb darf er von dort kommen.
    /// </para></remarks>
    public sealed class ChartWidgetOverlayText
    {
        /// <summary>Was dasteht.</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Die CSS-Klasse(n) - hier gehoeren die Typografie-Klassen hin. <b>Keine freien Stil-Angaben:</b>
        /// eine Klasse ist eine Auswahl aus dem, was das Stylesheet anbietet, ein <c>style</c> waere ein
        /// zweiter Weg, Fremdes einzuschleusen.
        /// </summary>
        public string? Class { get; init; }

        /// <summary>Waagrechte Lage, als SVG-Koordinate oder Prozentwert. Vorgabe: Mitte.</summary>
        public string PosX { get; init; } = "50%";

        /// <summary>Senkrechte Lage, als SVG-Koordinate oder Prozentwert. Vorgabe: Mitte.</summary>
        public string PosY { get; init; } = "50%";

        /// <summary>
        /// Woran die Position den Text ausrichtet: <c>start</c>, <c>middle</c> oder <c>end</c>.
        /// </summary>
        /// <remarks>
        /// Vorgabe <c>middle</c>, und das ist keine Kosmetik: in SVG ist <c>x</c> der ANFANG des Textes.
        /// Ohne diese Ausrichtung staende ein Text bei <c>PosX = "50%"</c> rechts neben der Mitte - der
        /// Fehler, den man beim ersten Ausprobieren macht.
        /// </remarks>
        public string Anchor { get; init; } = DefaultAnchor;

        /// <summary>Die Vorgabe-Ausrichtung.</summary>
        public const string DefaultAnchor = "middle";

        /// <summary>Die zulaessigen Ausrichtungen.</summary>
        public static IReadOnlyList<string> Anchors { get; } = new[] { "start", "middle", "end" };
    }

    /// <summary>
    /// What both chart renderers produce and the chart view consumes: the prepared parts plus everything
    /// else, still untouched, for the pass-through binding.
    /// </summary>
    /// <remarks>
    /// Aufbereitet werden nur die drei Felder, die aus den Abfrage-Daten gebaut werden muessen. Alles
    /// Weitere geht als Name/Wert weiter und wird erst gegen die Parameter der Diagramm-Komponente
    /// geprueft - so muss diese Klasse nicht wissen, was MudBlazor alles kann.
    /// </remarks>
    public sealed class ChartWidgetDeclaration
    {
        /// <summary>Ab dieser Breite passt ein Diagramm noch neben ein anderes.</summary>
        public const int DefaultMinWidth = 280;

        /// <summary>Der Name der Aktion, die ein Klick ohne Navigationsziel ausloest.</summary>
        public const string DefaultAction = "select";

        public ChartType Type { get; init; }

        public IReadOnlyList<ChartWidgetLabel> Labels { get; init; } = Array.Empty<ChartWidgetLabel>();

        public List<ChartSeries<double>> Series { get; init; } = new();

        /// <summary>
        /// Texte, die IM Diagramm stehen (leer = keine). Beim Ring die Mitte - dort steht sonst nichts.
        /// </summary>
        public IReadOnlyList<ChartWidgetOverlayText> Overlay { get; init; } = Array.Empty<ChartWidgetOverlayText>();

        /// <summary>
        /// The caption above this chart. Null hides it.
        /// </summary>
        /// <remarks>
        /// Bei mehreren Diagrammen in einer Kachel reicht die Kachel-Beschriftung nicht mehr aus. Der Wert
        /// darf ein Kultur-Datensatz sein; uebersetzt wird beim ANZEIGEN, nicht hier - so bleibt diese
        /// Klasse ohne Umgebung pruefbar.
        /// </remarks>
        public string? Title { get; init; }

        /// <summary>
        /// The width below which this chart wraps to its own line, in pixels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Bewusst eine Mindestbreite und keine Spaltenzahl: wie breit die Kachel wirklich ist, haengt an
        /// ihrem ColSpan UND am Fenster - das weiss nur der Browser. Mit einer Mindestbreite ordnen sich
        /// die Diagramme von selbst nebeneinander, solange der Platz reicht, und untereinander, sobald er
        /// nicht mehr reicht. 0 heisst "immer nebeneinander, Platz zu gleichen Teilen".
        /// </para>
        /// <para>
        /// Sie gilt fuer ein Diagramm, dessen Breite sich nach dem Platz richtet. Traegt die Deklaration
        /// eine absolute <c>width</c>, steht die Breite schon fest und bestimmt den Platz - dieser Wert hat
        /// dann keine Wirkung mehr (siehe <c>ChartWidgetView.SizeStyle</c>).
        /// </para>
        /// </remarks>
        public int MinWidth { get; init; } = DefaultMinWidth;

        /// <summary>
        /// The action name a click without a navigation target raises.
        /// </summary>
        /// <remarks>
        /// Er ist einstellbar, weil eine Anwendung sonst nicht unterscheiden koennte, WELCHES Diagramm
        /// einer Kachel geklickt wurde - <c>WidgetAction</c> traegt nur Name und Argument.
        /// </remarks>
        public string Action { get; init; } = DefaultAction;

        /// <summary>Every other field of the declaration, unconverted.</summary>
        public IReadOnlyDictionary<string, object?> Extra { get; init; }
            = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reads a declaration. Both renderers arrive here: the CScript one with an <c>ObjectLiteral</c>
        /// (which is an <c>IDictionary&lt;string, object&gt;</c>), the Scriban one with the map its JSON was
        /// parsed into.
        /// </summary>
        /// <param name="map">the declaration</param>
        /// <param name="errors">receives every complaint - nothing is dropped silently</param>
        /// <returns>the declaration, or null when it cannot be read at all</returns>
        public static ChartWidgetDeclaration? FromMap(IDictionary<string, object?>? map, List<string> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (map == null || map.Count == 0)
            {
                errors.Add("The configuration produced no chart declaration.");
                return null;
            }

            // Gross-/Kleinschreibung darf nicht entscheiden: 'Type' und 'type' sind sonst genau die Sorte
            // Unterschied, die eine leere Kachel erzeugt.
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object?> entry in map)
            {
                values[entry.Key] = entry.Value;
            }

            ChartType type = ReadType(values, errors);
            IReadOnlyList<ChartWidgetLabel> labels = ReadLabels(values, errors);
            List<ChartSeries<double>> series = ReadSeries(values, errors);

            foreach (ChartSeries<double> s in series)
            {
                int count = s.Data?.Count() ?? 0;
                if (labels.Count != 0 && count != labels.Count)
                {
                    // Kein stilles Abschneiden: eine Kategorie lautlos wegzulassen waere die schlechteste
                    // aller Auskuenfte - die Zahlen sehen dann richtig aus und sind es nicht.
                    errors.Add(
                        $"Series '{s.Name}' has {count} value(s) but there are {labels.Count} label(s).");
                }
            }

            var extra = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object?> entry in values)
            {
                if (!IsPreparedField(entry.Key))
                {
                    extra[entry.Key] = entry.Value;
                }
            }

            return new ChartWidgetDeclaration
            {
                Type = type,
                Labels = labels,
                Series = series,
                Overlay = ReadOverlay(values, errors),
                Title = ReadTitle(values),
                MinWidth = ReadMinWidth(values, errors),
                Action = ReadAction(values),
                Extra = extra
            };
        }

        /// <summary>
        /// The fields this class prepares itself - everything else is a parameter of the chart component.
        /// </summary>
        /// <remarks>
        /// Oeffentlich, weil die Uebersicht des Knopfes "Parameter einfuegen" sie nennen muss. Stuende die
        /// Liste dort ein zweites Mal, waere sie beim naechsten neuen Feld sofort falsch - und ein Feld,
        /// das die Uebersicht als Diagramm-Parameter ausgibt, obwohl es hier abgefangen wird, ist eine
        /// Einladung zum Fehler.
        /// </remarks>
        public static IReadOnlyList<string> PreparedFields { get; } = new[]
        {
            "type", "labels", "series", "title", "minWidth", "action", "overlay"
        };

        private static bool IsPreparedField(string name)
            => PreparedFields.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));

        private static string? ReadTitle(IDictionary<string, object?> values)
        {
            if (!values.TryGetValue("title", out object? raw) || raw == null)
            {
                return null;
            }

            string text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static string ReadAction(IDictionary<string, object?> values)
        {
            if (!values.TryGetValue("action", out object? raw) || raw == null)
            {
                return DefaultAction;
            }

            string text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? DefaultAction : text;
        }

        private static int ReadMinWidth(IDictionary<string, object?> values, List<string> errors)
        {
            if (!values.TryGetValue("minWidth", out object? raw) || raw == null)
            {
                return DefaultMinWidth;
            }

            // Eine Zahl, keine CSS-Laenge: aus "300px" liesse sich zwar rechnen, aber dann muesste diese
            // Klasse Einheiten kennen - und '50%' waere eine Angabe, mit der der Umbruch nichts anfangen
            // kann. Wer die Groesse des Diagramms selbst meint, setzt weiterhin width/height durch.
            if (raw is IConvertible)
            {
                try
                {
                    int width = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                    if (width >= 0)
                    {
                        return width;
                    }

                    errors.Add($"'minWidth' cannot be negative ({width}).");
                    return DefaultMinWidth;
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    // Faellt unten in dieselbe Meldung.
                }
            }

            errors.Add($"'minWidth' must be a number of pixels, not '{raw}'.");
            return DefaultMinWidth;
        }

        private static ChartType ReadType(IDictionary<string, object?> values, List<string> errors)
        {
            if (!values.TryGetValue("type", out object? raw) || raw == null)
            {
                errors.Add("The declaration has no 'type' (pie, donut, bar, line, …).");
                return ChartType.Pie;
            }

            // Ein CScript-Ausdruck darf ChartType.Pie schreiben, ein JSON-Template nur "pie" - beides
            // kommt hier an.
            if (raw is ChartType chartType)
            {
                return chartType;
            }

            string text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            if (Enum.TryParse(text, ignoreCase: true, out ChartType parsed))
            {
                return parsed;
            }

            errors.Add($"'{text}' is not a chart type. Known: {string.Join(", ", Enum.GetNames<ChartType>())}.");
            return ChartType.Pie;
        }

        private static IReadOnlyList<ChartWidgetLabel> ReadLabels(IDictionary<string, object?> values,
            List<string> errors)
        {
            if (!values.TryGetValue("labels", out object? raw) || raw == null)
            {
                return Array.Empty<ChartWidgetLabel>();
            }

            var labels = new List<ChartWidgetLabel>();
            foreach (object? item in AsList(raw, errors, "labels"))
            {
                if (AsMap(item) is IDictionary<string, object?> map)
                {
                    var entry = new Dictionary<string, object?>(map, StringComparer.OrdinalIgnoreCase);
                    entry.TryGetValue("text", out object? text);
                    entry.TryGetValue("navigateTo", out object? target);
                    labels.Add(new ChartWidgetLabel
                    {
                        Text = Convert.ToString(text, CultureInfo.InvariantCulture) ?? string.Empty,
                        NavigateTo = Convert.ToString(target, CultureInfo.InvariantCulture)
                    });
                }
                else
                {
                    labels.Add(new ChartWidgetLabel
                    {
                        Text = Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty
                    });
                }
            }

            return labels;
        }

        /// <summary>
        /// Liest die Texte, die im Diagramm stehen sollen. Fehlt der Eintrag, gibt es keine - das ist der
        /// Normalfall und keine Meldung wert.
        /// </summary>
        private static IReadOnlyList<ChartWidgetOverlayText> ReadOverlay(IDictionary<string, object?> values,
            List<string> errors)
        {
            if (!values.TryGetValue("overlay", out object? raw) || raw == null)
            {
                return Array.Empty<ChartWidgetOverlayText>();
            }

            var texts = new List<ChartWidgetOverlayText>();
            foreach (object? item in AsList(raw, errors, "overlay"))
            {
                if (AsMap(item) is not IDictionary<string, object?> map)
                {
                    // Anders als bei 'labels' gibt es hier keine Kurzform: ein blosser Text haette keine
                    // Position, und die zu raten hiesse, ihn irgendwo hinzuschreiben.
                    errors.Add("Each entry of 'overlay' must be an object with at least 'text'.");
                    continue;
                }

                var entry = new Dictionary<string, object?>(map, StringComparer.OrdinalIgnoreCase);
                entry.TryGetValue("text", out object? text);
                entry.TryGetValue("class", out object? cls);
                entry.TryGetValue("posX", out object? posX);
                entry.TryGetValue("posY", out object? posY);
                entry.TryGetValue("anchor", out object? anchor);

                string resolvedAnchor = Convert.ToString(anchor, CultureInfo.InvariantCulture) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(resolvedAnchor))
                {
                    resolvedAnchor = ChartWidgetOverlayText.DefaultAnchor;
                }
                else if (!ChartWidgetOverlayText.Anchors.Contains(resolvedAnchor, StringComparer.OrdinalIgnoreCase))
                {
                    // Melden statt still auf die Vorgabe zu fallen: ein Tippfehler in der Ausrichtung
                    // verschiebt den Text sichtbar, und dann sucht man ihn im Diagramm statt im Text.
                    errors.Add($"'{resolvedAnchor}' is not a valid overlay anchor - use "
                               + $"{string.Join(", ", ChartWidgetOverlayText.Anchors)}.");
                    resolvedAnchor = ChartWidgetOverlayText.DefaultAnchor;
                }

                texts.Add(new ChartWidgetOverlayText
                {
                    Text = Convert.ToString(text, CultureInfo.InvariantCulture) ?? string.Empty,
                    Class = Convert.ToString(cls, CultureInfo.InvariantCulture),
                    PosX = Coordinate(posX) ?? "50%",
                    PosY = Coordinate(posY) ?? "50%",
                    Anchor = resolvedAnchor
                });
            }

            return texts;
        }

        /// <summary>
        /// Eine SVG-Koordinate aus dem Skript: Zahl oder Prozentwert. Immer mit Punkt als Trennzeichen -
        /// unter deutschem Gebietsschema waere ein Komma in einem SVG-Attribut ein zweiter Wert.
        /// </summary>
        private static string? Coordinate(object? raw)
        {
            if (raw == null)
            {
                return null;
            }

            string text = raw is IFormattable f
                ? f.ToString(null, CultureInfo.InvariantCulture)
                : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static List<ChartSeries<double>> ReadSeries(IDictionary<string, object?> values,
            List<string> errors)
        {
            var series = new List<ChartSeries<double>>();
            if (!values.TryGetValue("series", out object? raw) || raw == null)
            {
                errors.Add("The declaration has no 'series'.");
                return series;
            }

            foreach (object? item in AsList(raw, errors, "series"))
            {
                if (AsMap(item) is not IDictionary<string, object?> map)
                {
                    errors.Add("Each entry of 'series' must be an object with 'name' and 'data'.");
                    continue;
                }

                var entry = new Dictionary<string, object?>(map, StringComparer.OrdinalIgnoreCase);
                entry.TryGetValue("name", out object? name);
                entry.TryGetValue("data", out object? data);

                double[] numbers = AsList(data, errors, "series data")
                    .Select(v => ToDouble(v, errors))
                    .ToArray();

                series.Add(new ChartSeries<double>
                {
                    Name = Convert.ToString(name, CultureInfo.InvariantCulture) ?? string.Empty,
                    Data = numbers
                });
            }

            if (series.Count == 0)
            {
                errors.Add("'series' is empty - there is nothing to draw.");
            }

            return series;
        }

        /// <summary>Reads a value as a list. A single value counts as a list of one.</summary>
        private static IEnumerable<object?> AsList(object? raw, List<string> errors, string what)
        {
            switch (raw)
            {
                case null:
                    errors.Add($"'{what}' is empty.");
                    return Array.Empty<object?>();

                // Zeichenketten sind aufzaehlbar (Zeichen fuer Zeichen) - ohne diesen Fall waere "abc"
                // eine Liste aus drei Buchstaben.
                case string text:
                    return new object?[] { text };

                case IEnumerable list:
                    return list.Cast<object?>();

                default:
                    return new[] { raw };
            }
        }

        /// <summary>
        /// Reads a value as a map. Deckt beide Herkuenfte ab: das <c>ObjectLiteral</c> aus CScript ist ein
        /// <c>IDictionary&lt;string, object&gt;</c>, die JSON-Seite liefert eines mit object?-Werten.
        /// </summary>
        /// <remarks>
        /// Ein einziger Fall genuegt: <c>IDictionary&lt;string, object&gt;</c> und
        /// <c>IDictionary&lt;string, object?&gt;</c> sind DERSELBE Typ - die Nullable-Angabe gehoert nicht
        /// zur Typidentitaet. Damit faengt dieser Zweig auch das <c>ObjectLiteral</c> aus CScript.
        /// </remarks>
        private static IDictionary<string, object?>? AsMap(object? raw)
            => raw as IDictionary<string, object?>;

        private static double ToDouble(object? value, List<string> errors)
        {
            switch (value)
            {
                case null:
                    return 0d;

                case double d:
                    return d;

                case IConvertible convertible:
                    try
                    {
                        return convertible.ToDouble(CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                    {
                        errors.Add($"'{value}' is not a number.");
                        return 0d;
                    }

                default:
                    errors.Add($"'{value}' is not a number.");
                    return 0d;
            }
        }
    }
}
