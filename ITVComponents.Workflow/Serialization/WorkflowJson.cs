using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow.Serialization
{
    /// <summary>
    /// Die kanonische JSON-Serialisierung der Workflow-Bibliothek (System.Text.Json). Dasselbe Format,
    /// das der Store persistiert, ist auch das <b>portable Export-/Import-Format</b>: der Knotengraph wird
    /// ueber stabile, selbstgewaehlte Diskriminatoren (<c>"kind"</c>) abgebildet - typnamen-unabhaengig,
    /// sodass eine exportierte Definition beim Import (auch in einem anderen System/einer anderen Version)
    /// wieder aufloest.
    /// </summary>
    /// <remarks>
    /// Der Knackpunkt ist <c>Dictionary&lt;string,object&gt;</c> (Instanz-Variablen und
    /// Knoten-Konfiguration): System.Text.Json liest <c>object</c>-Werte sonst als <see cref="JsonElement"/>
    /// zurueck, wodurch aus einem <c>int</c> ein JSON-Knoten wuerde. Der <see cref="ObjectValueConverter"/>
    /// gibt Primitive typerhaltend zurueck. Bekannte Grenze: verschachtelte Objekte/Arrays als
    /// Variablenwert kommen als <see cref="JsonElement"/> zurueck (Workflow-Variablen sind ueblicherweise
    /// Primitive).
    /// </remarks>
    public static class WorkflowJson
    {
        private static readonly JsonSerializerOptions Compact = Build(false);
        private static readonly JsonSerializerOptions Indented = Build(true);

        /// <summary>Serialisiert einen Wert. <paramref name="indented"/> = eingerueckt (fuer Export/Dateien).</summary>
        public static string Serialize<T>(T value, bool indented = false)
        {
            return JsonSerializer.Serialize(value, indented ? Indented : Compact);
        }

        /// <summary>Deserialisiert einen Wert; liefert <c>default</c> bei leerem Text.</summary>
        public static T Deserialize<T>(string json)
        {
            return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, Compact);
        }

        /// <summary>
        /// Exportiert eine Definition als (eingerueckten) JSON-Text - das portable Austauschformat.
        /// </summary>
        public static string ExportDefinition(WorkflowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return Serialize(definition, indented: true);
        }

        /// <summary>
        /// Importiert eine Definition aus JSON-Text. Wirft <see cref="System.Text.Json.JsonException"/> bei
        /// ungueltigem JSON. Die fachliche Pruefung (Struktur/Verweise) uebernimmt der
        /// <c>WorkflowDefinitionValidator</c>.
        /// </summary>
        public static WorkflowDefinition ImportDefinition(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("The import text is empty.", nameof(json));
            }

            return Deserialize<WorkflowDefinition>(json);
        }

        private static JsonSerializerOptions Build(bool indented)
        {
            var options = new JsonSerializerOptions { WriteIndented = indented };
            options.Converters.Add(new ObjectValueConverter());
            return options;
        }

        /// <summary>Haelt Primitive typerhaltend, wenn sie als <c>object</c> serialisiert/gelesen werden.</summary>
        private sealed class ObjectValueConverter : JsonConverter<object>
        {
            public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.True:
                        return true;
                    case JsonTokenType.False:
                        return false;
                    case JsonTokenType.String:
                        return reader.GetString();
                    case JsonTokenType.Number:
                        if (reader.TryGetInt32(out int i))
                        {
                            return i;
                        }

                        if (reader.TryGetInt64(out long l))
                        {
                            return l;
                        }

                        if (reader.TryGetDecimal(out decimal m))
                        {
                            return m;
                        }

                        return reader.GetDouble();
                    case JsonTokenType.Null:
                        return null;
                    default:
                        // Objekt/Array: als JsonElement erhalten (seltener Fall bei Variablen).
                        using (JsonDocument document = JsonDocument.ParseValue(ref reader))
                        {
                            return document.RootElement.Clone();
                        }
                }
            }

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                // Ueber den konkreten Typ serialisieren - fuer int/string/bool greifen deren eigene
                // Konverter, nicht erneut dieser hier (keine Rekursion).
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }
    }
}
