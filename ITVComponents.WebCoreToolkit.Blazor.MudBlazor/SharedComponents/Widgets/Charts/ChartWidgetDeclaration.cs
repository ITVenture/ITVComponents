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
        public ChartType Type { get; init; }

        public IReadOnlyList<ChartWidgetLabel> Labels { get; init; } = Array.Empty<ChartWidgetLabel>();

        public List<ChartSeries<double>> Series { get; init; } = new();

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
                Extra = extra
            };
        }

        private static bool IsPreparedField(string name)
            => string.Equals(name, "type", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "labels", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "series", StringComparison.OrdinalIgnoreCase);

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
