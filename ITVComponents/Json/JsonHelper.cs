using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using ITVComponents.Cloning;
using ITVComponents.Json.Contracts;
using ITVComponents.Json.Converters;
using ITVComponents.Logging;
using ITVComponents.Settings;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;

namespace ITVComponents.Json
{
    public static class JsonHelper
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly DynamicContractResolver strongContract = new DynamicContractResolver();

        static JsonHelper()
        {
            DynamicContractResolver.ConfigureType(typeof(IManualSerializer), typeof(SimpleContract), "SimpleContract");
        }

        /// <summary>
        /// Serializer-settings configuring newtonsoft to type-full-qualify each serialized object
        /// </summary>
        private static readonly JsonSerializerOptions strongTypedSerializerSettings = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            //ReferenceHandler = 
            TypeInfoResolver = strongContract,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            WriteIndented = true,
        };
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
        private static readonly JsonSerializerOptions simpleSerializerSettings = new JsonSerializerOptions()
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
        };
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
        private static readonly JsonSerializerOptions strongTypedSerializerSettingsWithReferences = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            ReferenceHandler = ReferenceHandler.Preserve,
            TypeInfoResolver = strongContract,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            WriteIndented = true,
        };
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
        private static readonly JsonSerializerOptions simpleSerializerSettingsWithReferences = new JsonSerializerOptions
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
        };
        /*{
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects
        };*/

        public static string EncryptJsonValues(this string jsonString, string password = null)
        {
            var setng = new JsonSerializerOptions(simpleSerializerSettingsWithReferences);
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
            return Serialize(tmp, setng, false);
        }

        public static string EncryptJsonValues(this object rawObject, string password = null)
        {
            var settings = new JsonSerializerOptions(simpleSerializerSettingsWithReferences);/*new JsonSerializerSettings
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
            return Serialize(rawObject, settings, false);
        }

        public static string EncryptJsonValues(this object rawObject, byte[] encryptionKey = null)
        {
            var settings = new JsonSerializerOptions(simpleSerializerSettingsWithReferences);/*new JsonSerializerSettings
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
            return Serialize(rawObject, settings, false);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObjectStrongTyped(object value, Stream targetStream, bool preserveReferences = false, bool useCamelCase = false)
        {
            var serializer = GetSerializer(true, preserveReferences, useCamelCase);
            Serialize(value, serializer, targetStream, true);
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="sourceStream">the source-stream from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadStrongTypedObject<T>(Stream sourceStream, bool preserveReferences = false, bool useCamelCase= false)
        {
            var serializer = GetSerializer(true, preserveReferences, useCamelCase);
            return DeserializeObject<T>(serializer, sourceStream, true);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="fileName">the name of the file where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObjectStrongTyped(object value, string fileName, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (FileStream targetStream = File.OpenWrite(fileName))
            {
                targetStream.SetLength(0);
                WriteObjectStrongTyped(value, targetStream, preserveReferences, useCamelCase);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadStrongTypedObject<T>(string fileName, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return ReadStrongTypedObject<T>(sourceStream, preserveReferences, useCamelCase);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="sourceStream">the source-stream from which the data is read</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(Stream sourceStream, Encoding encoding, bool preserveReferences = false, bool useCamelCase = false)
        {
            var serializer = GetSerializer(false, preserveReferences, useCamelCase);
            return DeserializeObject<T>(serializer, sourceStream, false);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObject(object value, Stream targetStream, bool preserveReferences = false, bool useCamelCase = false)
        {
            var serializer = GetSerializer(false, preserveReferences, useCamelCase);
            //serializer.Encoder = JavaScriptEncoder.;
            Serialize(value, serializer, targetStream, false);
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="sourceStream">the source-stream from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(Stream sourceStream, bool preserveReferences = false, bool useCamelCase = false)
        {
            return ReadObject<T>(sourceStream, Encoding.UTF8, preserveReferences, useCamelCase);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="fileName">the name of the file where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObject(object value, string fileName, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (FileStream targetStream = File.OpenWrite(fileName))
            {
                targetStream.SetLength(0);
                WriteObject(value, targetStream, preserveReferences, useCamelCase);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(string fileName, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return ReadObject<T>(sourceStream, Encoding.UTF8, preserveReferences, useCamelCase);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(string fileName, Encoding encoding, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return ReadObject<T>(sourceStream, encoding, preserveReferences, useCamelCase);
            }
        }

        /// <summary>
        /// Converts the given object to a simple Json-string with or without preserving the references
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="preserveReferences">indicates whether or not to preserve the object references</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the json-string representation of the given object</returns>
        public static string ToJson(object value, bool preserveReferences = false, bool useCamelCase = false)
        {
            var basicSettings =
                new JsonSerializerOptions(!preserveReferences ? simpleSerializerSettings : simpleSerializerSettingsWithReferences);
            return ToJson(value, basicSettings, useCamelCase, false);
        }

        /// <summary>
        /// Converts the given string into an object of the target type with or without reference perseverance
        /// </summary>
        /// <typeparam name="T">the target type to convert the json string into</typeparam>
        /// <param name="json">the json representing the object</param>
        /// <param name="preserveReferences">indicates whether the references where preserved in the serialization</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T FromJsonString<T>(string json, bool preserveReferences = false, bool useCamelCase = false)
        {
            var basicSettings =
                new JsonSerializerOptions(!preserveReferences ? simpleSerializerSettings : simpleSerializerSettingsWithReferences);
            return FromJson<T>(json, basicSettings, useCamelCase, false);
        }

        /// <summary>
        /// Converts the given string into an object of the target type with or without reference perseverance
        /// </summary>
        /// <param name="t">the target type to convert the json string into</param>
        /// <param name="json">the json representing the object</param>
        /// <param name="preserveReferences">indicates whether the references where preserved in the serialization</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static object FromJsonString(Type t, string json, bool preserveReferences = false,
            bool useCamelCase = false)
        {
            var basicSettings = new JsonSerializerOptions(!preserveReferences
                ? simpleSerializerSettings
                : simpleSerializerSettingsWithReferences);
            return FromJson(t, json, basicSettings, useCamelCase);
        }

        /// <summary>
        /// Converts the given object to a strong-typed Json-string with or without preserving the references
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="preserveReferences">indicates whether or not to preserve the object references</param>
        /// <returns>the json-string representation of the given object</returns>
        public static string ToJsonStrongTyped(object value, bool preserveReferences = false, bool useCamelCase = false)
        {
            JsonSerializerOptions basicSettings = new JsonSerializerOptions(!preserveReferences
                ? strongTypedSerializerSettings
                : strongTypedSerializerSettingsWithReferences);
            return ToJson(value, basicSettings, useCamelCase, true);
        }

        /// <summary>
        /// Converts the given string into an object of the target type with or without reference perseverance
        /// </summary>
        /// <typeparam name="T">the target type to convert the json string into</typeparam>
        /// <param name="json">the json representing the object</param>
        /// <param name="preserveReferences">indicates whether the references where preserved in the serialization</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T FromJsonStringStrongTyped<T>(string json, bool preserveReferences = false, bool useCamelCase = false)
        {
            var basicSettings = new JsonSerializerOptions(!preserveReferences
                ? strongTypedSerializerSettings
                : strongTypedSerializerSettingsWithReferences);
            return FromJson<T>(json, basicSettings, useCamelCase, true);
        }

        /// <summary>
        /// Basic implementation for json-serialization
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="basicSettings">the estimated basic-settings</param>
        /// <param name="useCamelCase">indicates whether to use camelCase-naming convention</param>
        /// <returns>a string representing the json-notation of the given object</returns>
        private static string ToJson(object value, JsonSerializerOptions basicSettings, bool useCamelCase, bool strongTyped)
        {
            if (useCamelCase)
            {
                basicSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            }

            return Serialize(value, basicSettings, strongTyped);
        }

        /// <summary>
        /// Basic Implementation for json-deserialization
        /// </summary>
        /// <typeparam name="T">the type to deserialize</typeparam>
        /// <param name="json">the json-text that represent the target object</param>
        /// <param name="basicSettings">the estimated basic-settings</param>
        /// <param name="useCamelCase">indicates whether to use camelCase-naming convention</param>
        /// <returns>the deserialized object</returns>
        private static T FromJson<T>(string json, JsonSerializerOptions basicSettings, bool useCamelCase, bool strongTyped)
        {
            if (useCamelCase)
            {
                basicSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            }

            return DeserializeObject<T>(json, basicSettings, strongTyped);
        }

        private static object FromJson(Type t, string json, JsonSerializerOptions basicSettings, bool useCamelCase)
        {
            if (useCamelCase)
            {
                basicSettings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                //basicSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            }

            return DeserializeObject(json, basicSettings, t);
        }

        private static T DeserializeObject<T>(string json, JsonSerializerOptions settings, bool strongTyped)
        {
            var data = Utf8NoBom.GetBytes(json);
            using MemoryStream mst = new MemoryStream(data);
            return DeserializeObject<T>(mst, settings, strongTyped);
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
            return DeserializeObject<T>(settings, r, strongTyped);
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

        private static T DeserializeObject<T>(JsonSerializerOptions serializer, Stream r, bool strongTyped)
        {
            if (strongTyped)
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

        private static string Serialize(object value, JsonSerializerOptions settings, bool strongTyped)
        {
            using MemoryStream mst = new MemoryStream();
            Serialize(value, settings, mst, strongTyped);
            var data = mst.ToArray();
            return Utf8NoBom.GetString(data);
        }

        private static void Serialize(object value, JsonSerializerOptions settings, Stream writer, bool strongTyped)
        {
            if (value is IManualSerializer mas)
            {
                //var serializer = GetSerializer(settings);
                //Serialize(value, serializer, writer);
                System.Text.Json.JsonSerializer.Serialize(writer, mas, settings);
            }
            else if (strongTyped)
            {
                System.Text.Json.JsonSerializer.Serialize(writer, new SimpleContract{Value = value}, settings);
            }
            else
            {
                System.Text.Json.JsonSerializer.Serialize(writer, value, settings);
            }
        }

        /*private static void Serialize(object value, JsonSerializer serializer, TextWriter writer)
        {
            serializer.Serialize(writer, value);
        }*/

        private static JsonSerializerOptions GetSerializer(bool strongTyped, bool preserveReferences, bool useCamelCase)
        {
            JsonSerializerOptions tmp;
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
    }
}
