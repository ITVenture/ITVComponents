using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ITVComponents.AgentServices.Abstractions;

namespace ITVComponents.AgentServices
{
    /// <summary>
    /// Liest Einstellungstypen (<see cref="IHasDeviceSettings{TSettings}"/>) per Reflexion: welche Felder es
    /// gibt, und wie aus JSON ein Einstellungsobjekt wird. Baut nie ein Gerät.
    /// </summary>
    public static class DeviceSettingsReflection
    {
        /// <summary>
        /// Die Optionen, mit denen Einstellungen auf der Leitung und in der Ablage stehen.
        /// </summary>
        /// <remarks>
        /// Zahlen auch als Text: eine Maske liefert Text, und an einem <c>"port": "50000"</c> soll ein Gerät
        /// nicht scheitern.
        /// </remarks>
        public static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Der Einstellungstyp eines Geräts, oder <c>null</c>, wenn es keinen deklariert.</summary>
        /// <param name="deviceType">der Typ des Geräts</param>
        public static Type SettingsTypeOf(Type deviceType)
        {
            return deviceType?.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHasDeviceSettings<>))
                ?.GetGenericArguments()[0];
        }

        /// <summary>Die Felder eines Einstellungstyps.</summary>
        /// <param name="settingsType">der Einstellungstyp</param>
        public static SettingDescriptor[] Describe(Type settingsType)
        {
            if (settingsType == null)
            {
                return Array.Empty<SettingDescriptor>();
            }

            object defaults = Activator.CreateInstance(settingsType);
            return Fields(settingsType).Select(f => ToDescriptor(f.Property, f.Attribute, defaults)).ToArray();
        }

        /// <summary>
        /// Liest abgelegte Einstellungen nachsichtig: was es nicht (mehr) gibt, wird übergangen, was fehlt,
        /// behält seine Vorbelegung.
        /// </summary>
        /// <param name="settingsType">der Einstellungstyp</param>
        /// <param name="json">das abgelegte JSON; leer = Vorbelegung</param>
        /// <remarks>
        /// Für den Bau des Geräts. Ein Feld, das nach einem Update weggefallen ist, darf ein Gerät nicht
        /// am Starten hindern.
        /// </remarks>
        public static object Read(Type settingsType, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Activator.CreateInstance(settingsType);
            }

            return JsonSerializer.Deserialize(json, settingsType, Json) ?? Activator.CreateInstance(settingsType);
        }

        /// <summary>
        /// Prüft Einstellungen streng gegen den Einstellungstyp und baut daraus das Objekt.
        /// </summary>
        /// <param name="settingsType">der Einstellungstyp</param>
        /// <param name="json">die Werte als JSON-Objekt</param>
        /// <param name="errors">nimmt die Fehler je Feld auf</param>
        /// <returns>das Einstellungsobjekt; nur gültig, wenn <paramref name="errors"/> leer geblieben ist</returns>
        /// <remarks>
        /// <b>Streng:</b> ein Schlüssel, der kein Feld ist, wird abgelehnt und nicht übergangen. Nur so ist
        /// sichergestellt, dass über die Einstellungen nichts hereinkommt, was das Gerät nicht als Feld
        /// deklariert hat.
        /// </remarks>
        public static object Validate(Type settingsType, string json, List<SettingError> errors)
        {
            object retVal = Activator.CreateInstance(settingsType);
            JsonObject values;
            try
            {
                values = string.IsNullOrWhiteSpace(json)
                    ? new JsonObject()
                    : JsonNode.Parse(json) as JsonObject;
            }
            catch (JsonException ex)
            {
                errors.Add(new SettingError { Message = $"The settings are not valid JSON: {ex.Message}" });
                return retVal;
            }

            if (values == null)
            {
                errors.Add(new SettingError { Message = "The settings must be a JSON object." });
                return retVal;
            }

            var fields = Fields(settingsType).ToDictionary(f => FieldName(f.Property), StringComparer.OrdinalIgnoreCase);
            foreach (var key in values.Select(n => n.Key))
            {
                if (!fields.ContainsKey(key))
                {
                    errors.Add(new SettingError { SettingName = key, Message = $"'{key}' is not a setting of this device." });
                }
            }

            foreach (var (name, field) in fields)
            {
                var node = values.FirstOrDefault(n => string.Equals(n.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
                bool empty = node == null
                             || (node is JsonValue v && v.TryGetValue(out string text) && string.IsNullOrWhiteSpace(text));
                if (empty)
                {
                    // Nicht mitgeschickt heisst: die Vorbelegung gilt. Pflicht verlangt einen WERT, nicht
                    // dass er auf der Leitung stand - eine Maske laesst unveraenderte Vorschlaege weg.
                    if (field.Attribute.Required && IsEmpty(field.Property.GetValue(retVal)))
                    {
                        errors.Add(new SettingError { SettingName = name, Message = $"'{field.Attribute.Label}' is required." });
                    }

                    continue;
                }

                try
                {
                    field.Property.SetValue(retVal, ValueOf(node, field.Property.PropertyType));
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or FormatException)
                {
                    errors.Add(new SettingError
                    {
                        SettingName = name,
                        Message = $"'{field.Attribute.Label}' has an invalid value: {ex.Message}"
                    });
                }
            }

            return retVal;
        }

        /// <summary>Schreibt Einstellungen so, wie sie abgelegt werden — nur die Felder.</summary>
        /// <param name="settings">das Einstellungsobjekt</param>
        public static string Write(object settings)
        {
            var result = new JsonObject();
            foreach (var field in Fields(settings.GetType()))
            {
                result[FieldName(field.Property)] = JsonSerializer.SerializeToNode(field.Property.GetValue(settings),
                    field.Property.PropertyType, Json);
            }

            return result.ToJsonString(Json);
        }

        /// <summary>Der Name eines Felds auf der Leitung.</summary>
        public static string FieldName(PropertyInfo property) => Json.PropertyNamingPolicy!.ConvertName(property.Name);

        private static IEnumerable<(PropertyInfo Property, DeviceSettingAttribute Attribute)> Fields(Type settingsType)
        {
            return settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Select(p => (Property: p, Attribute: p.GetCustomAttribute<DeviceSettingAttribute>()))
                .Where(f => f.Attribute != null)
                .OrderBy(f => f.Attribute.Order)
                .ThenBy(f => f.Property.MetadataToken);
        }

        private static SettingDescriptor ToDescriptor(PropertyInfo property, DeviceSettingAttribute attribute, object defaults)
        {
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var retVal = new SettingDescriptor
            {
                Name = FieldName(property),
                Label = attribute.Label,
                Required = attribute.Required,
                HelpText = attribute.HelpText,
                ChoiceSource = attribute.ChoiceSource,
                DependsOn = attribute.DependsOn,
                DefaultValue = DefaultOf(property.GetValue(defaults))
            };

            if (!string.IsNullOrEmpty(attribute.ChoiceSource))
            {
                retVal.Kind = SettingKind.Choice;
            }
            else if (Enum.IsDefined(typeof(SettingKind), attribute.Kind))
            {
                retVal.Kind = attribute.Kind;
            }
            else
            {
                retVal.Kind = KindOf(type);
            }

            if (type.IsEnum)
            {
                retVal.Kind = SettingKind.Choice;
                retVal.Choices = Enum.GetNames(type).Select(n => new SettingChoice { Value = n, Label = n }).ToArray();
            }

            return retVal;
        }

        private static SettingKind KindOf(Type type)
        {
            if (type == typeof(bool))
            {
                return SettingKind.Boolean;
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return SettingKind.DateTime;
            }

            if (type == typeof(DateOnly))
            {
                return SettingKind.Date;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return SettingKind.Number;
                default:
                    return SettingKind.Text;
            }
        }

        /// <summary>
        /// Liest einen Wert in den Typ der Property. Ein Schalter darf als Text kommen — eine Maske liefert
        /// alles als Text, und Zahlen liest der Serialisierer ohnehin auch aus Text.
        /// </summary>
        private static object ValueOf(JsonNode node, Type targetType)
        {
            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (type == typeof(bool) && node is JsonValue value && value.TryGetValue(out string text))
            {
                if (bool.TryParse(text, out var flag))
                {
                    return flag;
                }

                return text.Trim().ToLowerInvariant() switch
                {
                    "1" or "yes" or "ja" or "on" => true,
                    "0" or "no" or "nein" or "off" => false,
                    _ => throw new FormatException($"'{text}' is no yes/no value.")
                };
            }

            return node.Deserialize(targetType, Json);
        }

        private static bool IsEmpty(object value) => value == null || value is string s && string.IsNullOrWhiteSpace(s);

        private static string DefaultOf(object value)
        {
            return value switch
            {
                null => null,
                string s => string.IsNullOrEmpty(s) ? null : s,
                bool b => b ? "true" : "false",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }
    }
}
