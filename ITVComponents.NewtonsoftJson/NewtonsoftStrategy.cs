using ITVComponents.Cloning;
using ITVComponents.Json;
using ITVComponents.Json.Strategy;
using ITVComponents.NewtonsoftJson.CustomConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace ITVComponents.NewtonsoftJson
{
    public class NewtonsoftStrategy: IJsonStrategy
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Serializer-settings configuring newtonsoft to type-full-qualify each serialized object
        /// </summary>
        private static readonly JsonSerializerSettings strongTypedSerializerSettings = new JsonSerializerSettings
        {
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            TypeNameHandling = TypeNameHandling.All
        };

        /// <summary>
        /// Serializer settings configuring newtonsoft to serialize with the default-settings
        /// </summary>
        private static readonly JsonSerializerSettings simpleSerializerSettings = new JsonSerializerSettings()
        {
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
        };

        /// <summary>
        /// Serializer-settings configuring newtonsoft to type-full-qualify each serialized object
        /// </summary>
        private static readonly JsonSerializerSettings strongTypedSerializerSettingsWithReferences = new JsonSerializerSettings
        {
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            TypeNameHandling = TypeNameHandling.All,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
        };

        /// <summary>
        /// Serializer settings configuring newtonsoft to serialize with the default-settings
        /// </summary>
        private static readonly JsonSerializerSettings simpleSerializerSettingsWithReferences = new JsonSerializerSettings
        {
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects
        };


        public string EncryptJsonValues(string jsonString, string password)
        {
            var settings = simpleSerializerSettingsWithReferences.Copy();
            settings.Converters.Add(new JsonStringEncryptConverter(password));
            var tmp = FromJsonString(jsonString, null, settings);
            return ToJson(tmp, settings);
        }

        public string EncryptJsonValues(object rawObject, string password)
        {
            var settings = simpleSerializerSettingsWithReferences.Copy();
            settings.Converters.Add(new JsonStringEncryptConverter(password));
            return ToJson(rawObject, settings);
        }

        public string EncryptJsonValues(object rawObject, byte[] encryptionKey)
        {
            var settings = simpleSerializerSettingsWithReferences.Copy();
            settings.Converters.Add(new JsonStringEncryptConverter(encryptionKey));
            return ToJson(rawObject, settings);
        }

        public string ToJson<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options) where TSerializerOptions : class
        {
            using MemoryStream mst = new MemoryStream();
            WriteObject(value, options, mst);
            var data = mst.ToArray();
            return Utf8NoBom.GetString(data);
        }

        public string ToJson<TProtocol>(TProtocol value, SerializationTypingMode typingMode, bool preserveReferences,
            bool useCamelCase)
        {

            return ToJson(value, typingMode, typeof(TProtocol), preserveReferences, useCamelCase);
        }

        public string ToJson(object value, SerializationTypingMode typingMode, Type? type, bool preserveReferences, bool useCamelCase)
        {
            var strong = typingMode is SerializationTypingMode.AssistedPolymorphism or SerializationTypingMode.NativePolymorphism;
            var serializer = GetSerializer(strong, preserveReferences, useCamelCase);
            using MemoryStream mst = new MemoryStream();
            WriteObject(value, serializer, mst);
            var data = mst.ToArray();
            return Utf8NoBom.GetString(data);
        }

        public void WriteObject<TProto>(TProto value, SerializationTypingMode typingMode, Stream targetStream, bool preserveReferences,
            bool useCamelCase)
        {
            var strong = typingMode is SerializationTypingMode.AssistedPolymorphism or SerializationTypingMode.NativePolymorphism;
            var serializer = GetSerializer(strong, preserveReferences, useCamelCase);
            WriteObject(value, serializer, targetStream);
        }

        public void WriteObject<TProtocol>(TProtocol value, SerializationTypingMode typingMode, string fileName,
            bool preserveReferences, bool useCamelCase)
        {
            var strong = typingMode is SerializationTypingMode.AssistedPolymorphism or SerializationTypingMode.NativePolymorphism;
            var ofs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            var serializer = GetSerializer(strong, preserveReferences, useCamelCase);
            WriteObject(value, serializer, ofs);
        }

        public void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options, string fileName) where TSerializerOptions : class
        {
            var ofs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            WriteObject(value, options, ofs);
        }

        public void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options, Stream targetStream) where TSerializerOptions : class
        {
            if (options is JsonSerializerSettings sts)
            {
                using (TextWriter w = new StreamWriter(targetStream, Utf8NoBom, -1, false))
                {
                    var serializer = JsonSerializer.Create(sts);
                    serializer.Serialize(w, value);
                }
            }
        }

        public T ReadObject<T>(Stream sourceStream, SerializationTypingMode typingMode, bool preserveReferences, bool useCamelCase)
        {
            var strong = typingMode is SerializationTypingMode.AssistedPolymorphism or SerializationTypingMode.NativePolymorphism;
            var sr = GetSerializer(strong, preserveReferences, useCamelCase);
            var retVal = ReadObject(sourceStream, typeof(T), sr);
            if (retVal is T r)
            {
                return r;
            }

            return default;
        }

        public T ReadObject<T, TSerializerOptions>(Stream sourceStream, TSerializerOptions options) where TSerializerOptions : class
        {
            var retVal = ReadObject(sourceStream, typeof(T), options);
            if (retVal is T r)
            {
                return r;
            }

            return default;
        }

        private object ReadObject<TSerializerOptions>(Stream sourceStream, Type t, TSerializerOptions options) where TSerializerOptions : class
        {
            using var r = new StreamReader(sourceStream, Utf8NoBom);
            using var jr = new JsonTextReader(r);
            if (options is JsonSerializerSettings sts)
            {
                JsonSerializer serializer = JsonSerializer.Create(sts);
                if (t != null)
                {
                    return serializer.Deserialize(jr, t);
                }

                return serializer.Deserialize(jr);
            }

            return null;
        }

        public T ReadObject<T>(string fileName, SerializationTypingMode typingMode, bool preserveReferences, bool useCamelCase)
        {
            using var file = File.OpenRead(fileName);
            return ReadObject<T>(file, typingMode, preserveReferences, useCamelCase);
        }

        public T FromJsonString<T>(string json, SerializationTypingMode typingMode, bool preserveReferences, bool useCamelCase)
        {
            var strong = typingMode is SerializationTypingMode.AssistedPolymorphism or SerializationTypingMode.NativePolymorphism;
            var serializer = GetSerializer(strong, preserveReferences, useCamelCase);
            var retVal = FromJsonString(json, typeof(T), serializer);
            if (retVal is T r)
            {
                return r;
            }

            return default;
        }

        public T FromJsonString<T, TSerializerOptions>(string json, TSerializerOptions options) where TSerializerOptions : class
        {
            var tmp = (T)FromJsonString(json, typeof(T), options);
            if (tmp != null)
            {
                return (T)tmp;
            }

            return default;
        }

        public object FromJsonString(string json, Type t, SerializationTypingMode typingMode, bool preserveReferences,
            bool useCamelCase)
        {
            var strong = typingMode is SerializationTypingMode.AssistedPolymorphism or SerializationTypingMode.NativePolymorphism;
            var serializer = GetSerializer(strong, preserveReferences, useCamelCase);
            return FromJsonString(json, t, serializer);
        }

        public object FromJsonString<TSerializerOptions>(string json, Type t, TSerializerOptions options) where TSerializerOptions : class
        {
            var data = Utf8NoBom.GetBytes(json);
            using MemoryStream mst = new MemoryStream(data);
            return ReadObject(mst, t, options);
        }

        public TSerializerOptions WithStrongContract<TSerializerOptions>(TSerializerOptions options) where TSerializerOptions : class
        {
            if (options is JsonSerializerSettings jsop)
            {
                var retVal = jsop;
                retVal.CheckAdditionalContent = true;
                retVal.ConstructorHandling = ConstructorHandling.Default;
                retVal.Formatting = Formatting.Indented;
                retVal.MissingMemberHandling = MissingMemberHandling.Ignore;
                retVal.ObjectCreationHandling = ObjectCreationHandling.Auto;
                retVal.NullValueHandling = NullValueHandling.Include;
                retVal.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
                retVal.TypeNameHandling = TypeNameHandling.All;
                retVal.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
                return retVal as TSerializerOptions;
            }

            throw new NotSupportedException("NewtonsoftJsonStrategy only supports Options of Type JsonSerializerSettings");
        }

        private static JsonSerializerSettings GetSerializer(bool strongTyped, bool preserveReferences, bool useCamelCase)
        {
            JsonSerializerSettings tmp;
            if (strongTyped)
            {
                tmp = preserveReferences
                    ? strongTypedSerializerSettingsWithReferences
                    : strongTypedSerializerSettings;
            }
            else
            {
                tmp = preserveReferences ? simpleSerializerSettingsWithReferences : simpleSerializerSettings;
            }

            tmp = tmp.Copy();
            if (useCamelCase)
            {
                tmp.ContractResolver = new CamelCasePropertyNamesContractResolver();
            }

            return tmp;
        }
    }
}
