using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Xml;
using ITVComponents.Cloning;
using ITVComponents.Helpers;
using ITVComponents.Json.Contracts;
using ITVComponents.Json.Converters;
using ITVComponents.Logging;
using ITVComponents.Settings;
using Microsoft.Extensions.FileProviders;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;

namespace ITVComponents.Json
{
    public static class JsonHelper
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly DynamicContractResolver strongContract = new DynamicContractResolver();

        private static readonly DefaultJsonTypeInfoResolver defaultContract = new DefaultJsonTypeInfoResolver();

        private static readonly List<Action<JsonTypeInfo>> protocolTypeExtensions = new List<Action<JsonTypeInfo>>();

        static JsonHelper()
        {
            DynamicContractResolver.ConfigureType(typeof(IManualSerializer), typeof(SimpleContract), "SimpleContract");
            defaultContract.Modifiers.Add(ProcessTypeExtensions);
        }

        /// <summary>
        /// Serializer-settings configuring newtonsoft to type-full-qualify each serialized object
        /// </summary>
        private static readonly JsonSerializerOptions strongTypedSerializerSettings = BuildSerializerOptions(false);

        /*private static readonly JsonSerializerOptions defaultPolyTypeSettings = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            //ReferenceHandler = 
            TypeInfoResolver = defaultContract,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            WriteIndented = true,
        };*/
        /*{
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            TypeNameHandling = TypeNameHandling.All
        };*/

        /// <summary>
        /// Serializer settings configuring newtonsoft to serialize with the default-settings
        /// </summary>
        /*private static readonly JsonSerializerOptions simpleSerializerSettings = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            //ReferenceHandler = 
            //TypeInfoResolver = strongContract,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            WriteIndented = true
        };*/
        /*{
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
        };*/

        /// <summary>
        /// Serializer-settings configuring newtonsoft to type-full-qualify each serialized object
        /// </summary>
        private static readonly JsonSerializerOptions strongTypedSerializerSettingsWithReferences =
            BuildSerializerOptions(true);

        /*private static readonly JsonSerializerOptions defaultPolyTypeSettingsWithReferences =
            new JsonSerializerOptions()
            {
                AllowTrailingCommas = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
                ReferenceHandler = ReferenceHandler.Preserve,
                TypeInfoResolver = defaultContract,
                UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
                WriteIndented = true,
            };*/
        /*{
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            TypeNameHandling = TypeNameHandling.All,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
        };*/

        /// <summary>
        /// Serializer settings configuring newtonsoft to serialize with the default-settings
        /// </summary>
        /*private static readonly JsonSerializerOptions simpleSerializerSettingsWithReferences = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            ReferenceHandler = ReferenceHandler.Preserve,
            //TypeInfoResolver = strongContract,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            WriteIndented = true,
        };*/
        /*{
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects
        };*/

        public static void ExtendNativeProtocolType<TProto, TExt>(string discriminator) where TExt:TProto
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

        public static string EncryptJsonValues(this string jsonString, string password = null)
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
            }*/;
            setng.Converters.Add(new JsonStringEncryptConverter(password));
            var tmp = DeserializeObject(jsonString, setng);
            return Serialize(tmp, setng);
        }

        public static string EncryptJsonValues(this object rawObject, string password = null)
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

        public static string EncryptJsonValues(this object rawObject, byte[] encryptionKey = null)
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

        /// <summary>
        /// Serializes an instance of Type TProtocol to string using the provided SerializerOptions
        /// </summary>
        /// <typeparam name="TProtocol">the Type from which to use the type-settings for the serializer</typeparam>
        /// <param name="value">the value to serialize</param>
        /// <param name="options">the options used for serialization</param>
        /// <returns>the serialized string</returns>
        public static string ToJson<TProtocol>(TProtocol value, JsonSerializerOptions options)
        {
            return Serialize(value, options);
        }

        /// <summary>
        /// Serializes an instance of the Type TProtocol to string using the provided parameters
        /// </summary>
        /// <typeparam name="TProtocol">the Type from which to use the Type-settings for the serializer</typeparam>
        /// <param name="value">the value to serialize</param>
        /// <param name="typingMode">the typing-mode to use for serialization</param>
        /// <param name="preserveReferences">indicates whether to use reference-reserving</param>
        /// <param name="useCamelCase">indicates whether to use javaScript conforming camelCase style</param>
        /// <returns>the serialized value of the provided value</returns>
        public static string ToJson<TProtocol>(TProtocol value, SerializationTypingMode typingMode,
            bool preserveReferences = false, bool useCamelCase = false)
        {
            JsonSerializerOptions basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            if (typingMode == SerializationTypingMode.AssistedPolymorphism && value is not IManualSerializer)
            {
                return ToJson<IManualSerializer>(new SimpleContract { Value = value }, basicSettings, useCamelCase);
            }

            return ToJson(value, basicSettings, useCamelCase);
        }

        public static string ToJson(object value, SerializationTypingMode typingMode, Type? type,
            bool preserveReferences = false, bool useCamelCase = false)
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
            return (string)meth.Invoke(null, new[] { value, basicSettings, useCamelCase });
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObject<TProto>(TProto value, SerializationTypingMode typingMode, Stream targetStream, bool preserveReferences = false, bool useCamelCase = false)
        {
            var basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            //serializer.Encoder = JavaScriptEncoder.;
            Serialize(value, basicSettings, targetStream);
        }

        public static void WriteObject<TProtocol>(TProtocol value, SerializationTypingMode typingMode, string fileName,
            bool preserveReferences = false, bool useCamelCase = false)
        {
            var basicSettings = GetSerializer(typingMode, preserveReferences, useCamelCase);
            using var fs = File.OpenWrite(fileName);
            Serialize(value, basicSettings, fs);
        }

        public static void WriteObject<TProtocol>(TProtocol value, JsonSerializerOptions options, string fileName)
        {
            using var fs = File.OpenWrite(fileName);
            Serialize(value, options, fs);
        }

        public static void WriteObject<TProtocol>(TProtocol value, JsonSerializerOptions options, Stream targetStream)
        {
            Serialize(value, options, targetStream);
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="sourceStream">the source-stream from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(Stream sourceStream, SerializationTypingMode typingMode,
            bool preserveReferences = false, bool useCamelCase = false)
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

        public static T ReadObject<T>(Stream sourceStream, JsonSerializerOptions options)
        {
            return DeserializeObject<T>(options, sourceStream);
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(string fileName, SerializationTypingMode typingMode, bool preserveReferences = false, bool useCamelCase = false)
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

        public static T FromJsonString<T>(string json, SerializationTypingMode typingMode,
            bool preserveReferences = false, bool useCamelCase = false)
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

        public static object FromJsonString(string json, Type t, SerializationTypingMode typingMode,
            bool preserveReferences = false,
            bool useCamelCase = false)
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
            return impl.Invoke(null, new object[] { json, typingMode, preserveReferences, useCamelCase });
        }

        public static JsonSerializerOptions WithStrongContract(JsonSerializerOptions options)
        {
            var retVal = options;
            options.TypeInfoResolverChain.Clear();
            options.TypeInfoResolverChain.Add(strongContract);
            return retVal;
        }

        /// <summary>
        /// Basic implementation for json-serialization
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="basicSettings">the estimated basic-settings</param>
        /// <param name="useCamelCase">indicates whether to use camelCase-naming convention</param>
        /// <returns>a string representing the json-notation of the given object</returns>
        private static string ToJson<T>(T value, JsonSerializerOptions basicSettings, bool useCamelCase)
        {
            if (useCamelCase)
            {
                basicSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            }

            return Serialize(value, basicSettings);
        }

        private static T DeserializeObject<T>(JsonSerializerOptions settings, string json)
        {
            var data = Utf8NoBom.GetBytes(json);
            using MemoryStream mst = new MemoryStream(data);
            return DeserializeObject<T>(settings, mst);
        }

        private static object DeserializeObject(string json, JsonSerializerOptions settings, Type t = null)
        {
            var data = Utf8NoBom.GetBytes(json);
            using MemoryStream mst = new MemoryStream(data);
            using (TextReader r = new StreamReader(mst, Utf8NoBom))
            {
                return DeserializeObject(r, settings, t);
            }
        }

        private static T DeserializeObject<T>(Stream r, JsonSerializerOptions settings, bool strongTyped)
        {
            return
                System.Text.Json.JsonSerializer
                    .Deserialize<T>(r, settings);
        }

        private static object DeserializeObject(TextReader r, JsonSerializerOptions settings, Type t = null)
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

        private static T DeserializeObject<T>(JsonSerializerOptions serializer, Stream r)
        {
            return
                System.Text.Json.JsonSerializer
                    .Deserialize<T>(r, serializer);
            //if (strongTyped)
            {
                var tmp = System.Text.Json.JsonSerializer
                    .Deserialize<IManualSerializer>(r, serializer);
                //tmp.OnDeserialized(serializer);
                object ret = tmp;
                if (tmp is SimpleContract sc)
                {
                    ret = sc.Value;
                }

                if (ret is T retVal)
                {
                    return retVal;
                }

                LogEnvironment.LogDebugEvent("Deserialization for StrongType-Formatted JSON failed.", LogSeverity.Warning);
            }

            return
                System.Text.Json.JsonSerializer
                    .Deserialize<T>(r, serializer); //serializer.Deserialize<T>(new JsonTextReader(r));
        }

        /*private static object DeserializeObject(JsonSerializer serializer, TextReader r, Type t = null)
        {
            using var jr = new JsonTextReader(r);
            if (t != null)
            {
                return serializer.Deserialize(jr, t);
            }

            return serializer.Deserialize(jr);
        }*/

        private static string Serialize<T>(T value, JsonSerializerOptions settings)
        {
            using MemoryStream mst = new MemoryStream();
            Serialize(value, settings, mst);
            var data = mst.ToArray();
            return Utf8NoBom.GetString(data);
        }

        private static void Serialize<T>(T value, JsonSerializerOptions settings, Stream writer)
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

        /*private static void Serialize(object value, JsonSerializer serializer, TextWriter writer)
        {
            serializer.Serialize(writer, value);
        }*/

        private static JsonSerializerOptions GetSerializer(SerializationTypingMode strongTypeMode, bool preserveReferences, bool useCamelCase)
        {
            JsonSerializerOptions tmp = preserveReferences
                ? strongTypedSerializerSettingsWithReferences
                : strongTypedSerializerSettings; ;

            tmp = new JsonSerializerOptions(tmp);
            if (useCamelCase)
            {
                tmp.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            }

            return tmp;
        }

        /*private static JsonSerializer GetSerializer(JsonSerializerSettings settings)
        {
            return JsonSerializer.Create(settings);
        }*/

        private static JsonSerializerOptions BuildSerializerOptions(bool preserveReferences)
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

        private static void ProcessTypeExtensions(JsonTypeInfo obj)
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

    public enum SerializationTypingMode
    {
        StaticTyping,
        AssistedPolymorphism,
        NativePolymorphism
    }
}
