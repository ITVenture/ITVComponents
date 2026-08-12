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
        /// <summary>Parses the rendered text.</summary>
        /// <param name="json">what the template produced</param>
        /// <param name="errors">receives the parse error, with the position - JSON-Fehler sind sonst
        /// nicht zu finden</param>
        /// <returns>the declaration, or null when the text is not a JSON object</returns>
        public static IDictionary<string, object?>? ToMap(string? json, List<string> errors)
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
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"The template produced a JSON {document.RootElement.ValueKind}, not an object.");
                    return null;
                }

                // Kommentare hat der Reader uebersprungen; ein weiteres Token waere echter Inhalt - und
                // der ist ein Fehler, kein Beiwerk (zwei Deklarationen hintereinander etwa).
                if (reader.Read())
                {
                    errors.Add("There is more than the declaration in the configuration - a second value follows it.");
                    return null;
                }

                return (IDictionary<string, object?>?)ToObject(document.RootElement);
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
