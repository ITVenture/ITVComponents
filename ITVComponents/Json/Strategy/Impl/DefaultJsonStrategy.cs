using ITVComponents.Helpers;
using ITVComponents.Json.Contracts;
using ITVComponents.Json.Converters;
using ITVComponents.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace ITVComponents.Json.Strategy.Impl
{
    internal class DefaultJsonStrategy:IJsonStrategy
    {
        private readonly DynamicContractResolver strongContract = new DynamicContractResolver();

        private volatile DefaultJsonTypeInfoResolver defaultContract;

        private readonly List<Action<JsonTypeInfo>> protocolTypeExtensions = new List<Action<JsonTypeInfo>>();

        private volatile JsonSerializerOptions strongTypedSerializerSettingsWithReferences;

        private readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Serializer-settings configuring newtonsoft to type-full-qualify each serialized object
        /// </summary>
        private volatile JsonSerializerOptions strongTypedSerializerSettings;

        /// <summary>Options for <see cref="SerializationTypingMode.EnvelopePolymorphism"/>, without and with references.</summary>
        private volatile JsonSerializerOptions envelopeSerializerSettings;

        private volatile JsonSerializerOptions envelopeSerializerSettingsWithReferences;

        /// <summary>
        /// Protocol bases that got native polymorphism through <see cref="ExtendNativeProtocolType{TProto,TExt}"/> - the
        /// envelope leaves them alone.
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, byte> nativeProtocolBases =
            new System.Collections.Concurrent.ConcurrentDictionary<Type, byte>();

        public DefaultJsonStrategy()
        {
            RebuildSerializerOptions();
        }

        /// <summary>
        /// Builds the options with a new resolver. System.Text.Json shares the cached type infos between all equal
        /// options; a new resolver makes them unequal, so a protocol base that was serialized before an extension was
        /// registered (e.g. InMemory before NamedPipe in the same process) picks up its new derived types.
        /// </summary>
        private void RebuildSerializerOptions()
        {
            var contract = new DefaultJsonTypeInfoResolver();
            contract.Modifiers.Add(ProcessTypeExtensions);
            contract.Modifiers.Add(ApplyEncryptAttributes);
            defaultContract = contract;
            strongTypedSerializerSettings = BuildSerializerOptions(false);
            strongTypedSerializerSettingsWithReferences = BuildSerializerOptions(true);
            envelopeSerializerSettings = BuildEnvelopeSerializerOptions(false);
            envelopeSerializerSettingsWithReferences = BuildEnvelopeSerializerOptions(true);
        }

        public void ExtendNativeProtocolType<TProto, TExt>(string discriminator) where TExt : TProto
        {
            lock (protocolTypeExtensions)
            {
                nativeProtocolBases.TryAdd(typeof(TProto), 0);
                protocolTypeExtensions.Add(t =>
                {
                    if (t.Type == typeof(TProto))
                    {
                        // Polymorphism for these protocol bases is built entirely here rather than via a static
                        // [JsonPolymorphic] attribute. That way a base type whose derived types are all contributed
                        // by feature libraries that are not installed on this system stays non-polymorphic and
                        // serializes as the plain base, instead of System.Text.Json throwing
                        // "should specify at least one derived type" for an attribute with an empty derived-type set.
                        t.PolymorphismOptions ??= new JsonPolymorphismOptions
                        {
                            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType
                        };
                        t.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(TExt), discriminator));
                    }
                });

                RebuildSerializerOptions();
            }
        }

        public string EncryptJsonValues(string jsonString, string password)
        {
            // System.Text.Json does NOT invoke a JsonConverter<string> for the string values held
            // inside a parsed JsonNode DOM — the tree is written verbatim. So we cannot rely on the
            // converter here (as the typed-object overloads do); instead we walk the DOM and apply
            // the exact same transform to every string value.
            var converter = new JsonStringEncryptConverter(password);
            var node = JsonNode.Parse(jsonString);
            node = EncryptNodeStrings(node, converter);
            var setng = GetSerializer(SerializationTypingMode.StaticTyping, true, false);
            return Serialize(node, setng);
        }

        private static JsonNode EncryptNodeStrings(JsonNode node, JsonStringEncryptConverter converter)
        {
            switch (node)
            {
                case JsonValue val when val.TryGetValue<string>(out var s):
                    var transformed = converter.Transform(s);
                    return transformed == null ? null : JsonValue.Create(transformed);

                case JsonObject obj:
                    foreach (var key in obj.Select(p => p.Key).ToList())
                    {
                        var child = obj[key];
                        var replaced = EncryptNodeStrings(child, converter);
                        // Containers come back as the same reference (mutated in place); only leaf
                        // string values are swapped for a fresh, parent-less node — reassign just those
                        // to preserve property order and avoid "node already has a parent".
                        if (!ReferenceEquals(replaced, child))
                            obj[key] = replaced;
                    }
                    return obj;

                case JsonArray arr:
                    for (var i = 0; i < arr.Count; i++)
                    {
                        var child = arr[i];
                        var replaced = EncryptNodeStrings(child, converter);
                        if (!ReferenceEquals(replaced, child))
                            arr[i] = replaced;
                    }
                    return arr;

                default:
                    return node;
            }
        }

        public string EncryptJsonValues(object rawObject, string password)
        {
            var settings = GetSerializer(SerializationTypingMode.StaticTyping, true, false);/*new JsonSerializerSettings
            {
                CheckAdditionalContent = true,
                ConstructorHandling = ConstructorHandling.Default,
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                NullValueHandling = NullValueHandling.Include,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            }*/
            ;

            settings.Converters.Add(new JsonStringEncryptConverter(password));
            return Serialize(rawObject, settings);
        }

        public string EncryptJsonValues(object rawObject, byte[] encryptionKey)
        {
            var settings = GetSerializer(SerializationTypingMode.StaticTyping, true, false);
            /*new JsonSerializerSettings
            {
                CheckAdditionalContent = true,
                ConstructorHandling = ConstructorHandling.Default,
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                NullValueHandling = NullValueHandling.Include,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            }*/
            ;

            settings.Converters.Add(new JsonStringEncryptConverter(encryptionKey));
            return Serialize(rawObject, settings);
        }

        public string ToJson<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options) where TSerializerOptions:class
        {
            if (options is JsonSerializerOptions jsop)
                return Serialize(value, jsop);

            throw new NotSupportedException("DefaultJsonStrategy only supports Options of Type JsonSerializerOptions");
        }

        public string ToJson<TProtocol>(TProtocol value, SerializationTypingMode typingMode,
            bool preserveReferences, bool useCamelCase)
        {
            JsonSerializerOptions basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            return ToJson(value, basicSettings, useCamelCase);
        }

        public string ToJson(object value, SerializationTypingMode typingMode, Type? type,
            bool preserveReferences, bool useCamelCase)
        {
            if (typingMode == SerializationTypingMode.EnvelopePolymorphism)
            {
                // Without a given type the root keeps its runtime type: it is enveloped.
                type ??= typeof(object);
            }
            else
            {
                type ??= value?.GetType() ?? typeof(object);
            }

            JsonSerializerOptions basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            var meth = LambdaHelper.GetMethodInfo(() => ToJson(value, basicSettings, useCamelCase)).GetGenericMethodDefinition()
                .MakeGenericMethod(type);
            return (string)meth.Invoke(this, new[] { value, basicSettings, useCamelCase });
        }

        public void WriteObject<TProto>(TProto value, SerializationTypingMode typingMode, Stream targetStream, bool preserveReferences, bool useCamelCase)
        {
            var basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            //serializer.Encoder = JavaScriptEncoder.;
            Serialize(value, basicSettings, targetStream);
        }

        public void WriteObject<TProtocol>(TProtocol value, SerializationTypingMode typingMode, string fileName,
            bool preserveReferences, bool useCamelCase)
        {
            var basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            using var fs = File.OpenWrite(fileName);
            Serialize(value, basicSettings, fs);
        }

        public void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options, string fileName) where TSerializerOptions:class
        {
            if (options is JsonSerializerOptions jsop)
            {
                using var fs = File.OpenWrite(fileName);
                Serialize(value, jsop, fs);
                return;
            }

            throw new NotSupportedException("DefaultJsonStrategy only supports Options of Type JsonSerializerOptions");
        }

        public void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options,
            Stream targetStream) where TSerializerOptions : class
        {
            if (options is JsonSerializerOptions jsop)
            {
                Serialize(value, jsop, targetStream);
                return;
            }

            throw new NotSupportedException("DefaultJsonStrategy only supports Options of Type JsonSerializerOptions");
        }

        public T ReadObject<T>(Stream sourceStream, SerializationTypingMode typingMode,
            bool preserveReferences, bool useCamelCase)
        {
            var serializer = GetSerializer(typingMode, preserveReferences, useCamelCase);
            if (typingMode == SerializationTypingMode.EnvelopePolymorphism)
            {
                using var buffer = new MemoryStream();
                sourceStream.CopyTo(buffer);
                return ReadEnvelopeRoot<T>(buffer.ToArray(), serializer);
            }

            return DeserializeObject<T>(serializer, sourceStream);
        }

        public T ReadObject<T, TSerializerOptions>(Stream sourceStream, TSerializerOptions options)
            where TSerializerOptions : class
        {
            if (options is JsonSerializerOptions jsop)
            {
                return DeserializeObject<T>(jsop, sourceStream);
            }

            throw new NotSupportedException("DefaultJsonStrategy only supports Options of Type JsonSerializerOptions");
        }

        public T ReadObject<T>(string fileName, SerializationTypingMode typingMode, bool preserveReferences, bool useCamelCase)
        {
            var options = GetSerializer(typingMode, preserveReferences, useCamelCase);
            if (typingMode == SerializationTypingMode.EnvelopePolymorphism)
            {
                return ReadEnvelopeRoot<T>(File.ReadAllBytes(fileName), options);
            }

            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return DeserializeObject<T>(options, sourceStream);
            }
        }

        public T FromJsonString<T>(string json, SerializationTypingMode typingMode,
            bool preserveReferences, bool useCamelCase)
        {
            var basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            if (typingMode == SerializationTypingMode.EnvelopePolymorphism)
            {
                return ReadEnvelopeRoot<T>(Utf8NoBom.GetBytes(json), basicSettings);
            }

            return DeserializeObject<T>(basicSettings, json);
        }

        public T FromJsonString<T, TSerializerOptions>(string json, TSerializerOptions options)
            where TSerializerOptions : class
        {
            if (options is JsonSerializerOptions jsop)
            {
                return DeserializeObject<T>(jsop, json);
            }

            throw new NotSupportedException("DefaultJsonStrategy only supports Options of Type JsonSerializerOptions");
        }

        public object FromJsonString(string json, Type t, SerializationTypingMode typingMode,
            bool preserveReferences,
            bool useCamelCase)
        {
            // Without a type: object - in EnvelopePolymorphism an enveloped root carries its type itself.
            t ??= typeof(object);
            var mth = LambdaHelper
                .GetMethodInfo(() => FromJsonString<object>(json, typingMode, preserveReferences, useCamelCase))
                .GetGenericMethodDefinition();
            var impl = mth.MakeGenericMethod(t);
            return impl.Invoke(this, new object[] { json, typingMode, preserveReferences, useCamelCase });
        }

        public object FromJsonString<TSerializerOptions>(string json, Type t, TSerializerOptions options) where TSerializerOptions : class
        {
            t ??= typeof(object);
            var mth = LambdaHelper
                .GetMethodInfo(() => FromJsonString<object, TSerializerOptions>(json, options))
                .GetGenericMethodDefinition();
            var impl = mth.MakeGenericMethod(t, typeof(TSerializerOptions));
            return impl.Invoke(this, new object[] { json, options });
        }

        public TSerializerOptions WithStrongContract<TSerializerOptions>(TSerializerOptions options) where TSerializerOptions : class
        {
            if (options is JsonSerializerOptions jsop)
            {
                var retVal = jsop;
                retVal.TypeInfoResolverChain.Clear();
                retVal.TypeInfoResolverChain.Add(strongContract);
                return retVal as TSerializerOptions;
            }

            throw new NotSupportedException("DefaultJsonStrategy only supports Options of Type JsonSerializerOptions");
        }

        private string ToJson<T>(T value, JsonSerializerOptions basicSettings, bool useCamelCase)
        {
            if (useCamelCase)
            {
                basicSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            }

            return Serialize(value, basicSettings);
        }

        private T DeserializeObject<T>(JsonSerializerOptions settings, string json)
        {
            var data = Utf8NoBom.GetBytes(json);
            using MemoryStream mst = new MemoryStream(data);
            return DeserializeObject<T>(settings, mst);
        }

        private object DeserializeObject(string json, JsonSerializerOptions settings, Type t = null)
        {
            var data = Utf8NoBom.GetBytes(json);
            using MemoryStream mst = new MemoryStream(data);
            using (TextReader r = new StreamReader(mst, Utf8NoBom))
            {
                return DeserializeObject(r, settings, t);
            }
        }

        private T DeserializeObject<T>(Stream r, JsonSerializerOptions settings, bool strongTyped)
        {
            using var referenceScope = EnvelopeReferenceHandler.ScopeFor(settings);
            return
                System.Text.Json.JsonSerializer
                    .Deserialize<T>(r, settings);
        }

        private object DeserializeObject(TextReader r, JsonSerializerOptions settings, Type t = null)
        {
            /*JsonTypeInfo ty = null;
            if (t != null)
            {
                ty = settings.GetTypeInfo(t);
            }*/

            if (t == null)
            {
                var nd = JsonNode.Parse(r.ReadToEnd());
                var k = nd.GetValueKind();
                if (k == JsonValueKind.Array)
                {
                    return nd.AsArray();
                }

                if (k == JsonValueKind.Object)
                {
                    return nd.AsObject();
                }

                if (k == JsonValueKind.String || k == JsonValueKind.Number || k == JsonValueKind.False || k == JsonValueKind.True || k == JsonValueKind.Null)
                {
                    return nd.AsValue();
                }

                return null;
            }
            //System.Text.Json.JsonDocument.Parse(r.ReadToEnd(), t, settings);
            //JsonSerializer s = JsonSerializer.Create(settings);
            using var referenceScope = EnvelopeReferenceHandler.ScopeFor(settings);
            return System.Text.Json.JsonSerializer.Deserialize(r.ReadToEnd(), t, settings);
            //return DeserializeObject(s, r, t);
        }

        private T DeserializeObject<T>(JsonSerializerOptions serializer, Stream r)
        {
            using var referenceScope = EnvelopeReferenceHandler.ScopeFor(serializer);
            return
                System.Text.Json.JsonSerializer
                    .Deserialize<T>(r, serializer);
        }

        /// <summary>
        /// Reads a document of <see cref="SerializationTypingMode.EnvelopePolymorphism"/>. An enveloped root read into a
        /// concrete type is unpacked and checked (System.Text.Json itself would read it into an empty object); data in
        /// the format of the assisted mode is rejected.
        /// </summary>
        private T ReadEnvelopeRoot<T>(byte[] utf8Json, JsonSerializerOptions settings)
        {
            ReadOnlySpan<byte> content = utf8Json;
            if (content.StartsWith(Utf8Bom))
            {
                content = content.Slice(Utf8Bom.Length);
            }

            var shape = EnvelopeConverterFactory.InspectRoot(content);
            if (shape == EnvelopeConverterFactory.RootShape.Legacy)
            {
                throw EnvelopeConverterFactory.LegacyFormat();
            }

            using var referenceScope = EnvelopeReferenceHandler.ScopeFor(settings);
            var handledByEnvelope = settings.Converters.OfType<EnvelopeConverterFactory>().Any(f => f.CanConvert(typeof(T)));
            if (shape == EnvelopeConverterFactory.RootShape.Envelope && !handledByEnvelope)
            {
                var value = JsonSerializer.Deserialize<object>(content, settings);
                if (value is null)
                {
                    return default;
                }

                if (value is T typed)
                {
                    return typed;
                }

                throw new JsonException(
                    $"The enveloped root holds a {value.GetType().FullName}, which is not a {typeof(T).FullName}.");
            }

            return JsonSerializer.Deserialize<T>(content, settings);
        }

        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        private string Serialize<T>(T value, JsonSerializerOptions settings)
        {
            using MemoryStream mst = new MemoryStream();
            Serialize(value, settings, mst);
            var data = mst.ToArray();
            return Utf8NoBom.GetString(data);
        }

        private void Serialize<T>(T value, JsonSerializerOptions settings, Stream writer)
        {
            if (writer.CanSeek && writer.CanWrite)
            {
                try
                {
                    writer.SetLength(0);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent($"Failed to truncate existing Stream: {ex.Message}", LogSeverity.Error);
                }
            }

            using var referenceScope = EnvelopeReferenceHandler.ScopeFor(settings);
            JsonSerializer.Serialize(writer, value, settings);
        }

        private JsonSerializerOptions GetSerializer(SerializationTypingMode strongTypeMode, bool preserveReferences, bool useCamelCase)
        {
            JsonSerializerOptions tmp = strongTypeMode == SerializationTypingMode.EnvelopePolymorphism
                ? (preserveReferences ? envelopeSerializerSettingsWithReferences : envelopeSerializerSettings)
                : (preserveReferences ? strongTypedSerializerSettingsWithReferences : strongTypedSerializerSettings);

            tmp = new JsonSerializerOptions(tmp);
            if (useCamelCase)
            {
                tmp.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            }

            if (strongTypeMode == SerializationTypingMode.StaticTyping)
            {
                tmp.DefaultIgnoreCondition = JsonIgnoreCondition.Never;

            }

            return tmp;
        }

        private JsonSerializerOptions BuildSerializerOptions(bool preserveReferences)
        {
            var retVal = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                //ReferenceHandler = 
                UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
                WriteIndented = true,
                // Discriminators and $id/$ref need not be the first properties (hand-written JSON, other writers).
                AllowOutOfOrderMetadataProperties = true,
            };

            retVal.TypeInfoResolverChain.Clear();
            retVal.TypeInfoResolverChain.Add(strongContract);
            retVal.TypeInfoResolverChain.Add(defaultContract);
            if (preserveReferences)
            {
                retVal.ReferenceHandler = ReferenceHandler.Preserve;
            }

            return retVal;
        }

        /// <summary>The envelope converter, aware of the protocol bases with native polymorphism.</summary>
        internal JsonConverter CreateEnvelopeConverter()
        {
            return new EnvelopeConverterFactory(t => nativeProtocolBases.ContainsKey(t));
        }

        /// <summary>
        /// Options of <see cref="SerializationTypingMode.EnvelopePolymorphism"/>: the envelope converter instead of the
        /// manual-serializer contract, and - with references - a handler that shares one resolver across envelopes.
        /// </summary>
        private JsonSerializerOptions BuildEnvelopeSerializerOptions(bool preserveReferences)
        {
            var retVal = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
                WriteIndented = true,
                AllowOutOfOrderMetadataProperties = true,
            };

            retVal.TypeInfoResolverChain.Clear();
            retVal.TypeInfoResolverChain.Add(defaultContract);
            retVal.Converters.Add(CreateEnvelopeConverter());
            if (preserveReferences)
            {
                retVal.ReferenceHandler = EnvelopeReferenceHandler.Instance;
            }

            return retVal;
        }

        private static void ApplyEncryptAttributes(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            foreach (var property in typeInfo.Properties)
            {
                // Don't clobber an explicit [JsonConverter]; only act on plain string members that
                // carry the strategy-neutral [EncryptJsonValue] marker.
                if (property.PropertyType != typeof(string) || property.CustomConverter != null)
                {
                    continue;
                }

                if (property.AttributeProvider?.GetCustomAttributes(typeof(EncryptJsonValueAttribute), true) is { Length: > 0 } attrs
                    && attrs[0] is EncryptJsonValueAttribute enc)
                {
                    property.CustomConverter = string.IsNullOrEmpty(enc.Entropy)
                        ? new JsonStringEncryptConverter()
                        : new JsonStringEncryptConverter(enc.Entropy);
                }
            }
        }

        private void ProcessTypeExtensions(JsonTypeInfo obj)
        {
            var extensions = Array.Empty<Action<JsonTypeInfo>>();
            lock (protocolTypeExtensions)
            {
                if (protocolTypeExtensions.Count != 0)
                {
                    extensions = protocolTypeExtensions.ToArray();
                }
            }

            foreach (var ext in extensions)
            {
                ext(obj);
            }
        }
    }
}
