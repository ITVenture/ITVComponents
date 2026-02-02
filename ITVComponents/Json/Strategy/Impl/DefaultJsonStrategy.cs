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

        private readonly DefaultJsonTypeInfoResolver defaultContract = new DefaultJsonTypeInfoResolver();

        private readonly List<Action<JsonTypeInfo>> protocolTypeExtensions = new List<Action<JsonTypeInfo>>();

        private readonly JsonSerializerOptions strongTypedSerializerSettingsWithReferences;

        private readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Serializer-settings configuring newtonsoft to type-full-qualify each serialized object
        /// </summary>
        private readonly JsonSerializerOptions strongTypedSerializerSettings;

        public DefaultJsonStrategy()
        {
            DynamicContractResolver.ConfigureType(typeof(IManualSerializer), typeof(SimpleContract), "SimpleContract");
            defaultContract.Modifiers.Add(ProcessTypeExtensions);
            strongTypedSerializerSettings = BuildSerializerOptions(false);
            strongTypedSerializerSettingsWithReferences = BuildSerializerOptions(true);
        }

        public void ExtendNativeProtocolType<TProto, TExt>(string discriminator) where TExt : TProto
        {
            lock (protocolTypeExtensions)
            {
                protocolTypeExtensions.Add(t =>
                {
                    if (t.Type == typeof(TProto))
                    {
                        t.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(TExt), discriminator));
                    }
                });
            }
        }

        public string EncryptJsonValues(string jsonString, string password)
        {
            var setng = GetSerializer(SerializationTypingMode.StaticTyping, true, false);
            //var settings = simpleSerializerSettingsWithReferences.Copy();
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
            setng.Converters.Add(new JsonStringEncryptConverter(password));
            var tmp = DeserializeObject(jsonString, setng);
            return Serialize(tmp, setng);
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
            if (typingMode == SerializationTypingMode.AssistedPolymorphism && value is not IManualSerializer)
            {
                return ToJson<IManualSerializer>(new SimpleContract { Value = value }, basicSettings, useCamelCase);
            }

            return ToJson(value, basicSettings, useCamelCase);
        }

        public string ToJson(object value, SerializationTypingMode typingMode, Type? type,
            bool preserveReferences, bool useCamelCase)
        {
            if (typingMode == SerializationTypingMode.AssistedPolymorphism && value is IManualSerializer)
            {
                type ??= typeof(IManualSerializer);
            }
            else if (typingMode == SerializationTypingMode.AssistedPolymorphism && value is not IManualSerializer)
            {
                type = typeof(IManualSerializer);
                value = new SimpleContract() { Value = value };
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
            }

            throw new NotSupportedException("DefaultJsonStrategy only supports Options of Type JsonSerializerOptions");
        }

        public void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options,
            Stream targetStream) where TSerializerOptions : class
        {
            if (options is JsonSerializerOptions jsop)
            {
                Serialize(value, jsop, targetStream);
            }

            throw new NotSupportedException("DefaultJsonStrategy only supports Options of Type JsonSerializerOptions");
        }

        public T ReadObject<T>(Stream sourceStream, SerializationTypingMode typingMode,
            bool preserveReferences, bool useCamelCase)
        {
            var serializer = GetSerializer(typingMode, preserveReferences, useCamelCase);
            if (typingMode == SerializationTypingMode.AssistedPolymorphism && typeof(T) != typeof(IManualSerializer) &&
                typeof(T).GetInterfaces().Contains(typeof(IManualSerializer)))
            {
                return (T)DeserializeObject<IManualSerializer>(serializer, sourceStream);
            }

            if (typingMode == SerializationTypingMode.AssistedPolymorphism &&
                typeof(T) != typeof(IManualSerializer))
            {
                if (DeserializeObject<IManualSerializer>(serializer, sourceStream) is SimpleContract tmp)
                {
                    return (T)tmp.Value;
                }
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
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                if (typingMode == SerializationTypingMode.AssistedPolymorphism && typeof(T) != typeof(IManualSerializer) &&
                    typeof(T).GetInterfaces().Contains(typeof(IManualSerializer)))
                {
                    return (T)DeserializeObject<IManualSerializer>(options, sourceStream);
                }

                if (typingMode == SerializationTypingMode.AssistedPolymorphism &&
                    typeof(T) != typeof(IManualSerializer))
                {
                    if (DeserializeObject<IManualSerializer>(options, sourceStream) is SimpleContract tmp)
                    {
                        return (T)tmp.Value;
                    }
                }

                return DeserializeObject<T>(options, sourceStream);
            }
        }

        public T FromJsonString<T>(string json, SerializationTypingMode typingMode,
            bool preserveReferences, bool useCamelCase)
        {
            var basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            if (typingMode == SerializationTypingMode.AssistedPolymorphism && typeof(T) != typeof(IManualSerializer) &&
                typeof(T).GetInterfaces().Contains(typeof(IManualSerializer)))
            {
                return (T)DeserializeObject<IManualSerializer>(basicSettings, json);
            }

            if (typingMode == SerializationTypingMode.AssistedPolymorphism &&
                typeof(T) != typeof(IManualSerializer))
            {
                if (DeserializeObject<IManualSerializer>(basicSettings, json) is SimpleContract tmp)
                {
                    return (T)tmp.Value;
                }
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
            if (t == null && typingMode == SerializationTypingMode.AssistedPolymorphism)
            {
                t = typeof(IManualSerializer);
            }

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
            return System.Text.Json.JsonSerializer.Deserialize(r.ReadToEnd(), t, settings);
            //return DeserializeObject(s, r, t);
        }

        private T DeserializeObject<T>(JsonSerializerOptions serializer, Stream r)
        {
            return
                System.Text.Json.JsonSerializer
                    .Deserialize<T>(r, serializer);
        }

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

            JsonSerializer.Serialize(writer, value, settings);
        }

        private JsonSerializerOptions GetSerializer(SerializationTypingMode strongTypeMode, bool preserveReferences, bool useCamelCase)
        {
            JsonSerializerOptions tmp = preserveReferences
                ? strongTypedSerializerSettingsWithReferences
                : strongTypedSerializerSettings; ;

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
