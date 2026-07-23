using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Die JSON-Serialisierung des Stores (System.Text.Json).
    /// </summary>
    /// <remarks>
    /// Der Knackpunkt ist <c>Dictionary&lt;string,object&gt;</c> (Instanz-Variablen und
    /// Knoten-Konfiguration): System.Text.Json liest <c>object</c>-Werte sonst als
    /// <see cref="JsonElement"/> zurueck, wodurch aus einem <c>int</c> ein JSON-Knoten wuerde und
    /// die Engine an <c>(int)Variables[...]</c> scheiterte. Der <see cref="ObjectValueConverter"/>
    /// gibt Primitive typerhaltend zurueck. Der polymorphe Knotengraph wird ueber die
    /// Diskriminator-Attribute auf <see cref="Model.WorkflowNode"/> aufgeloest.
    ///
    /// Bekannte Grenze: Verschachtelte Objekte/Arrays als Variablenwert kommen als
    /// <see cref="JsonElement"/> zurueck, und ein als Variable abgelegter DateTime kommt als String
    /// zurueck - Workflow-Variablen sind ueblicherweise Primitive. Voller Typerhalt fuer beliebige
    /// Objektwerte waere ueber eine explizite Typmarkierung je Variable nachruestbar.
    /// </remarks>
    internal static class WorkflowJson
    {
        private static readonly JsonSerializerOptions Options = Build();

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        public static T Deserialize<T>(string json)
        {
            return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, Options);
        }

        private static JsonSerializerOptions Build()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ObjectValueConverter());
            return options;
        }

        /// <summary>
        /// Haelt Primitive typerhaltend, wenn sie als <c>object</c> serialisiert/gelesen werden.
        /// </summary>
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
