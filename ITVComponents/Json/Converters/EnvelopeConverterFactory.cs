// The bridge for IManualSerializer is the supported way to keep existing implementations running; the interface is
// obsolete for new code only.
#pragma warning disable CS0618
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;

namespace ITVComponents.Json.Converters
{
    /// <summary>
    /// The converter behind <see cref="SerializationTypingMode.EnvelopePolymorphism"/>: wraps every value whose
    /// declared type is open (<c>object</c>, abstract classes, non-collection interfaces) in
    /// <c>{"$type": "&lt;name&gt;", "$value": …}</c>. As a converter factory it acts on every level - properties,
    /// collection elements, dictionary values and the root.
    /// </summary>
    /// <remarks>
    /// Not wrapped: types with native polymorphism (<see cref="JsonPolymorphicAttribute"/>, protocol bases extended through
    /// <c>JsonHelper.ExtendNativeProtocolType</c>), collection interfaces and System.Text.Json's own node types.
    /// Types implementing <see cref="IManualSerializer"/> are always wrapped, their entries are written as an object
    /// keyed by the entry names (the bridge for existing implementations).
    /// </remarks>
    internal sealed class EnvelopeConverterFactory : JsonConverterFactory
    {
        public const string TypeProperty = "$type";

        public const string ValueProperty = "$value";

        public const string LegacyTypeProperty = "$$Type";

        private readonly Func<Type, bool> hasNativePolymorphism;

        public EnvelopeConverterFactory(Func<Type, bool> hasNativePolymorphism)
        {
            this.hasNativePolymorphism = hasNativePolymorphism;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            if (typeToConvert == typeof(object) || typeof(IManualSerializer).IsAssignableFrom(typeToConvert))
            {
                return true;
            }

            if (!(typeToConvert.IsInterface || typeToConvert.IsAbstract))
            {
                return false;
            }

            return !typeof(IEnumerable).IsAssignableFrom(typeToConvert)
                   && !typeof(JsonNode).IsAssignableFrom(typeToConvert)
                   && !typeToConvert.IsDefined(typeof(JsonPolymorphicAttribute), false)
                   && !typeToConvert.IsDefined(typeof(JsonDerivedTypeAttribute), false)
                   && !hasNativePolymorphism(typeToConvert);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return (JsonConverter)Activator.CreateInstance(typeof(EnvelopeConverter<>).MakeGenericType(typeToConvert));
        }

        /// <summary>The shape of a document's root, inspected before reading it.</summary>
        public enum RootShape
        {
            Other,
            Envelope,
            Legacy
        }

        /// <summary>
        /// Looks at the root of a document: an envelope (exactly <c>$type</c> and <c>$value</c>), the legacy format of
        /// removed <c>AssistedPolymorphism</c> mode (<c>$$Type</c> with a <c>Data</c> array), or
        /// anything else.
        /// </summary>
        public static RootShape InspectRoot(ReadOnlySpan<byte> utf8Json)
        {
            try
            {
                var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                {
                    return RootShape.Other;
                }

                bool hasType = false, hasValue = false, hasLegacyType = false, hasLegacyData = false;
                int count = 0;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    count++;
                    string name = reader.GetString();
                    reader.Read();
                    switch (name)
                    {
                        case TypeProperty:
                            hasType = true;
                            break;
                        case ValueProperty:
                            hasValue = true;
                            break;
                        case LegacyTypeProperty:
                            hasLegacyType = true;
                            break;
                        case "Data":
                            hasLegacyData = reader.TokenType == JsonTokenType.StartArray;
                            break;
                    }

                    reader.Skip();
                }

                if (hasLegacyType && hasLegacyData)
                {
                    return RootShape.Legacy;
                }

                return hasType && hasValue && count == 2 ? RootShape.Envelope : RootShape.Other;
            }
            catch (JsonException ex)
            {
                // Malformed JSON: the actual deserialization reports it with the full context.
                LogEnvironment.LogDebugEvent($"Root inspection of an envelope document failed, deserialization will report the error: {ex.Message}", LogSeverity.Report);
                return RootShape.Other;
            }
        }

        /// <summary>The exception for data in the format of the removed assisted mode.</summary>
        public static JsonException LegacyFormat()
        {
            return new JsonException(
                "The data is in the format of the removed SerializationTypingMode.AssistedPolymorphism (\"$$Type\" with a \"Data\" list), " +
                "which SerializationTypingMode.EnvelopePolymorphism does not read. The stored data has to be migrated.");
        }
    }

    /// <summary>Envelope for one declared open type, see <see cref="EnvelopeConverterFactory"/>.</summary>
    internal sealed class EnvelopeConverter<TDeclared> : JsonConverter<TDeclared>
    {
        private const string TypeProperty = EnvelopeConverterFactory.TypeProperty;

        private const string ValueProperty = EnvelopeConverterFactory.ValueProperty;

        private static readonly System.Text.Encodings.Web.JavaScriptEncoder RelaxedEncoder =
            System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        public override void Write(Utf8JsonWriter writer, TDeclared value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            Type runtimeType = value.GetType();
            if (runtimeType == typeof(string) || runtimeType == typeof(bool))
            {
                // Unambiguous in JSON - no envelope.
                JsonSerializer.Serialize(writer, value, runtimeType, options);
                return;
            }

            writer.WriteStartObject();
            // Type names come from the registry, not from user data: written readable (list<x>, Outer+Inner, List`1)
            // instead of the default escaping (<, +, `), which only matters when JSON is embedded in HTML.
            writer.WriteString(TypeProperty, JsonEncodedText.Encode(ManualTypeRegistry.NameOf(runtimeType), RelaxedEncoder));
            writer.WritePropertyName(ValueProperty);
            if (value is IManualSerializer manual)
            {
                WriteManual(writer, manual, options);
            }
            else if (runtimeType == typeof(object))
            {
                // A plain object: serializing it by its runtime type would land here again.
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            else
            {
                JsonSerializer.Serialize(writer, value, runtimeType, options);
            }

            writer.WriteEndObject();
        }

        public override TDeclared Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return default;
                case JsonTokenType.String:
                    return Bare(reader.GetString());
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return Bare(reader.GetBoolean());
                case JsonTokenType.Number:
                    return ReadBareNumber(ref reader);
                case JsonTokenType.StartArray:
                    if (typeof(TDeclared) == typeof(object))
                    {
                        return (TDeclared)(object)JsonNode.Parse(ref reader);
                    }

                    throw new JsonException($"An array without envelope cannot be read into {typeof(TDeclared).FullName}.");
                case JsonTokenType.StartObject:
                    return ReadEnvelope(ref reader, options);
                default:
                    throw new JsonException($"Unexpected token {reader.TokenType} for {typeof(TDeclared).FullName}.");
            }
        }

        private static TDeclared Bare(object value)
        {
            if (value is TDeclared typed)
            {
                return typed;
            }

            throw new JsonException($"A {value.GetType().Name} cannot be read into {typeof(TDeclared).FullName}.");
        }

        /// <summary>A number without envelope (hand-written JSON): the narrowest of int, long, decimal, double.</summary>
        private static TDeclared ReadBareNumber(ref Utf8JsonReader reader)
        {
            if (reader.TryGetInt32(out int i) && typeof(TDeclared).IsAssignableFrom(typeof(int)))
            {
                return (TDeclared)(object)i;
            }

            if (reader.TryGetInt64(out long l) && typeof(TDeclared).IsAssignableFrom(typeof(long)))
            {
                return (TDeclared)(object)l;
            }

            if (reader.TryGetDecimal(out decimal m) && typeof(TDeclared).IsAssignableFrom(typeof(decimal)))
            {
                return (TDeclared)(object)m;
            }

            if (reader.TryGetDouble(out double d) && typeof(TDeclared).IsAssignableFrom(typeof(double)))
            {
                return (TDeclared)(object)d;
            }

            throw new JsonException($"A number without envelope cannot be read into {typeof(TDeclared).FullName}.");
        }

        private static TDeclared ReadEnvelope(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            // Pass 1 on a copy: find $type wherever it stands. System.Text.Json hands a converter the complete value.
            var scan = reader;
            string typeName = null;
            bool hasValue = false, hasLegacyType = false;
            int others = 0;
            while (scan.Read() && scan.TokenType == JsonTokenType.PropertyName)
            {
                string name = scan.GetString();
                scan.Read();
                if (name == TypeProperty && scan.TokenType == JsonTokenType.String)
                {
                    typeName = scan.GetString();
                    continue;
                }

                if (name == ValueProperty)
                {
                    hasValue = true;
                }
                else
                {
                    hasLegacyType |= name == EnvelopeConverterFactory.LegacyTypeProperty;
                    others++;
                }

                scan.Skip();
            }

            if (hasLegacyType)
            {
                throw EnvelopeConverterFactory.LegacyFormat();
            }

            if (typeName == null || !hasValue || others != 0)
            {
                if (typeof(TDeclared) == typeof(object))
                {
                    // A plain object in an object place (hand-written JSON) stays untyped.
                    return (TDeclared)(object)JsonNode.Parse(ref reader);
                }

                throw new JsonException(
                    $"Expected an envelope {{\"{TypeProperty}\": …, \"{ValueProperty}\": …}} for {typeof(TDeclared).FullName}.");
            }

            Type type = ManualTypeRegistry.Resolve(typeName, out bool refused);
            if (type == null)
            {
                return Unresolved(ref reader, typeName, refused);
            }

            if (!typeof(TDeclared).IsAssignableFrom(type))
            {
                throw new JsonException(
                    $"The type '{typeName}' ({type.FullName}) is not assignable to {typeof(TDeclared).FullName}.");
            }

            // Pass 2 on the real reader: read $value exactly once (a second read would register its $id twice).
            object result = null;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString();
                reader.Read();
                if (name == ValueProperty)
                {
                    result = ReadValue(ref reader, type, typeName, options);
                }
                else
                {
                    reader.Skip();
                }
            }

            if (result != null && !type.IsInstanceOfType(result))
            {
                throw new JsonException(
                    $"The value read for '{typeName}' is a {result.GetType().FullName}, not a {type.FullName}.");
            }

            return (TDeclared)result;
        }

        private static TDeclared Unresolved(ref Utf8JsonReader reader, string typeName, bool refused)
        {
            if (typeof(TDeclared) != typeof(object))
            {
                throw new JsonException(refused
                    ? $"The type '{typeName}' is not registered and may not be read into {typeof(TDeclared).FullName} under the restriction to registered types."
                    : $"The type '{typeName}' could not be resolved for {typeof(TDeclared).FullName}; it was most likely renamed or removed.");
            }

            // Two different situations, two levels (same as the assisted format):
            //   refused = intended fallback, the type is simply not registered and the value stays untyped.
            //   else    = the type could have been loaded and was not - a renamed or removed class.
            LogEnvironment.LogEvent(
                refused
                    ? $"Type '{typeName}' is not registered; the value is kept untyped (JsonNode). Register it to read it back typed."
                    : $"Could not resolve type '{typeName}'. The value is kept untyped (JsonNode); the type was most likely renamed or removed.",
                refused ? LogSeverity.Report : LogSeverity.Error);

            JsonNode payload = null;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString();
                reader.Read();
                if (name == ValueProperty)
                {
                    payload = JsonNode.Parse(ref reader);
                }
                else
                {
                    reader.Skip();
                }
            }

            return (TDeclared)(object)payload;
        }

        private static object ReadValue(ref Utf8JsonReader reader, Type type, string typeName, JsonSerializerOptions options)
        {
            if (typeof(IManualSerializer).IsAssignableFrom(type))
            {
                return ReadManual(ref reader, type, options);
            }

            if (type == typeof(object))
            {
                reader.Skip();
                return new object();
            }

            try
            {
                return JsonSerializer.Deserialize(ref reader, type, options);
            }
            catch (InvalidOperationException ex)
            {
                // System.Text.Json reports e.g. a $ref to an object of another type this way.
                throw new JsonException($"The value of type '{typeName}' could not be read: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Bridge for <see cref="IManualSerializer"/>: the entries of <see cref="IManualSerializer.GetObjectData"/> as an
        /// object keyed by their names, every value an open place of its own.
        /// </summary>
        private static void WriteManual(Utf8JsonWriter writer, IManualSerializer value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            var resolver = EnvelopeReferenceHandler.ResolverFor(options);
            if (resolver != null)
            {
                string id = resolver.GetReference(value, out bool alreadyExists);
                if (alreadyExists)
                {
                    writer.WriteString("$ref", id);
                    writer.WriteEndObject();
                    return;
                }

                writer.WriteString("$id", id);
            }

            value.Data = new List<ManualSerializationData>();
            try
            {
                value.GetObjectData();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (ManualSerializationData entry in value.Data)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.PropertyName) || entry.PropertyName.StartsWith("$", StringComparison.Ordinal))
                    {
                        throw new JsonException(
                            $"{value.GetType().FullName} writes an entry named '{entry.PropertyName}'; entry names must not be empty or start with '$'.");
                    }

                    if (!names.Add(entry.PropertyName))
                    {
                        throw new JsonException(
                            $"{value.GetType().FullName} writes the entry '{entry.PropertyName}' more than once.");
                    }

                    writer.WritePropertyName(entry.PropertyName);
                    JsonSerializer.Serialize<object>(writer, entry.Data, options);
                }
            }
            finally
            {
                value.Data = null;
            }

            writer.WriteEndObject();
        }

        private static object ReadManual(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected an object for {type.FullName}, found {reader.TokenType}.");
            }

            IManualSerializer instance;
            try
            {
                instance = (IManualSerializer)Activator.CreateInstance(type, nonPublic: true);
            }
            catch (MissingMethodException ex)
            {
                throw new JsonException($"{type.FullName} has no parameterless constructor and cannot be read.", ex);
            }

            var resolver = EnvelopeReferenceHandler.ResolverFor(options);
            object referenced = null;
            bool isReference = false;
            var data = new List<ManualSerializationData>();
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString();
                reader.Read();
                if (name == "$id" || name == "$ref")
                {
                    if (resolver == null)
                    {
                        throw new JsonException($"'{name}' in {type.FullName}, but references are not preserved.");
                    }

                    if (name == "$id")
                    {
                        // Registered before the entries are read, so an entry may point back (cycle).
                        resolver.AddReference(reader.GetString(), instance);
                    }
                    else
                    {
                        referenced = resolver.ResolveReference(reader.GetString());
                        isReference = true;
                    }

                    continue;
                }

                data.Add(ManualSerializationData.FromValue(name, JsonSerializer.Deserialize<object>(ref reader, options)));
            }

            if (isReference)
            {
                if (data.Count != 0)
                {
                    throw new JsonException($"A reference to a {type.FullName} must not carry further entries.");
                }

                return referenced;
            }

            instance.Data = data;
            try
            {
                instance.ApplyObjectData();
            }
            finally
            {
                instance.Data = null;
            }

            return instance;
        }
    }
}
