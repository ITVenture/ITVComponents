using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets.Charts
{
    /// <summary>
    /// Reads a rendered JSON declaration into the same shape the CScript side arrives in.
    /// </summary>
    /// <remarks>
    /// Aus <c>JsonElement</c> wird ein gewoehnlicher Objekt-Baum (Woerterbuch, Liste, Zahl, Text,
    /// Wahrheitswert) - damit sieht der gemeinsame Teil beiden Herkuenften dasselbe an und muss nicht
    /// wissen, welcher Renderer ihn gerufen hat.
    /// </remarks>
    public static class ChartJson
    {
        /// <summary>
        /// Parses the rendered text into one entry per chart.
        /// </summary>
        /// <param name="json">what the template produced</param>
        /// <param name="errors">receives the parse error, with the position - JSON-Fehler sind sonst
        /// nicht zu finden</param>
        /// <returns>the entries, or null when the text is not readable at all</returns>
        /// <remarks>
        /// <para>
        /// Ein Objekt an der Wurzel ist EIN Diagramm, ein Array sind mehrere - so bleibt jede bisherige
        /// Konfiguration Wort fuer Wort gueltig, und aus derselben Abfrage lassen sich mehrere Grafiken
        /// bauen, ohne die Daten ein zweites Mal zu holen.
        /// </para>
        /// <para>
        /// Was ein einzelner Eintrag ist, prueft diese Klasse ABSICHTLICH nicht: das tut
        /// <see cref="ChartWidgetPanel.Build"/> - und zwar fuer beide Herkuenfte nach derselben Regel.
        /// Hier scheitert nur, was die ganze Kachel betrifft; ein einzelner unbrauchbarer Eintrag darf die
        /// uebrigen Diagramme nicht mitnehmen.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<object?>? ToEntries(string? json, List<string> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("The template produced no output.");
                return null;
            }

            try
            {
                // Ueber einen Reader und ParseValue statt ueber Parse(string): so darf hinter der
                // Deklaration noch etwas stehen. Genau das tut der Knopf "Parameter einfuegen" - er haengt
                // die Uebersicht als Kommentarblock an, und Parse(string) wuerde das als "zusaetzlicher
                // Inhalt" abweisen. Was WIRKLICH danach kommt, wird trotzdem geprueft (unten).
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json),
                    new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                using JsonDocument document = JsonDocument.ParseValue(ref reader);
                if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                {
                    errors.Add(
                        $"The template produced a JSON {document.RootElement.ValueKind}, not an object or an array of objects.");
                    return null;
                }

                // Kommentare hat der Reader uebersprungen; ein weiteres Token waere echter Inhalt - und
                // der ist ein Fehler, kein Beiwerk (zwei Deklarationen hintereinander etwa).
                if (reader.Read())
                {
                    errors.Add("There is more than the declaration in the configuration - a second value follows it.");
                    return null;
                }

                object? root = ToObject(document.RootElement);
                // Der Cast ist noetig: die beiden Zweige haben keinen gemeinsamen Typ, nur eine
                // gemeinsame Schnittstelle.
                return root is List<object?> entries
                    ? (IReadOnlyList<object?>)entries
                    : new object?[] { root };
            }
            catch (JsonException ex)
            {
                errors.Add($"The rendered configuration is not valid JSON: {ex.Message}");
                return null;
            }
        }

        private static object? ToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        map[property.Name] = ToObject(property.Value);
                    }

                    return map;
                }

                case JsonValueKind.Array:
                {
                    var list = new List<object?>();
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        list.Add(ToObject(item));
                    }

                    return list;
                }

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    return element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                default:
                    return null;
            }
        }
    }
}
