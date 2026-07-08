using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    /// <summary>
    /// One decoupled template part in <see cref="TenantTemplateMarkup.Extensions"/>: the handler's serialized
    /// <see cref="Payload"/> plus the <see cref="ApplyMode"/> that part should be applied with. The legacy string form
    /// (<c>"partKey": "payload"</c>) still deserializes (payload only, <see cref="TemplateApplyMode.Auto"/>).
    /// </summary>
    [JsonConverter(typeof(TemplateExtensionMarkupJsonConverter))]
    public class TemplateExtensionMarkup
    {
        public TemplateExtensionMarkup()
        {
        }

        public TemplateExtensionMarkup(string payload)
        {
            Payload = payload;
        }

        /// <summary>The part handler's serialized payload.</summary>
        public string Payload { get; set; }

        /// <summary>Apply mode for this part (<see cref="TemplateApplyMode.Auto"/> inherits the applying method's mode).</summary>
        public TemplateApplyMode ApplyMode { get; set; }
    }

    /// <summary>
    /// Reads both the current object form (<c>{ "Payload": "...", "ApplyMode": "Additive" }</c>) and the legacy string
    /// form (<c>"payload"</c>, mapped to payload-only with <see cref="TemplateApplyMode.Auto"/>), so tenant templates
    /// stored before the model change keep deserializing. Writes the object form.
    /// </summary>
    internal sealed class TemplateExtensionMarkupJsonConverter : JsonConverter<TemplateExtensionMarkup>
    {
        public override TemplateExtensionMarkup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String:
                    return new TemplateExtensionMarkup(reader.GetString());
                case JsonTokenType.StartObject:
                    string payload = null;
                    var mode = TemplateApplyMode.Auto;
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            break;
                        }

                        if (reader.TokenType != JsonTokenType.PropertyName)
                        {
                            continue;
                        }

                        var prop = reader.GetString();
                        reader.Read();
                        if (string.Equals(prop, nameof(TemplateExtensionMarkup.Payload), StringComparison.OrdinalIgnoreCase))
                        {
                            payload = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                        }
                        else if (string.Equals(prop, nameof(TemplateExtensionMarkup.ApplyMode), StringComparison.OrdinalIgnoreCase))
                        {
                            mode = ReadMode(ref reader);
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }

                    return new TemplateExtensionMarkup { Payload = payload, ApplyMode = mode };
                default:
                    throw new JsonException($"Unexpected token {reader.TokenType} while reading {nameof(TemplateExtensionMarkup)}.");
            }
        }

        private static TemplateApplyMode ReadMode(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var i) && Enum.IsDefined(typeof(TemplateApplyMode), i))
            {
                return (TemplateApplyMode)i;
            }

            if (reader.TokenType == JsonTokenType.String && Enum.TryParse<TemplateApplyMode>(reader.GetString(), true, out var m))
            {
                return m;
            }

            return TemplateApplyMode.Auto;
        }

        public override void Write(Utf8JsonWriter writer, TemplateExtensionMarkup value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(nameof(TemplateExtensionMarkup.Payload), value.Payload);
            writer.WriteString(nameof(TemplateExtensionMarkup.ApplyMode), value.ApplyMode.ToString());
            writer.WriteEndObject();
        }
    }
}
