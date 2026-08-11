using System;
using System.Collections.Generic;
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
                using JsonDocument document = JsonDocument.Parse(json,
                    new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"The template produced a JSON {document.RootElement.ValueKind}, not an object.");
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
