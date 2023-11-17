using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Cloning;
using ITVComponents.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ITVComponents.Helpers
{
    public static class JsonHelper
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

        public static string EncryptJsonValues(this string jsonString, string password = null)
        {
            var settings = simpleSerializerSettingsWithReferences.Copy();/*new JsonSerializerSettings
            {
                CheckAdditionalContent = true,
                ConstructorHandling = ConstructorHandling.Default,
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                NullValueHandling = NullValueHandling.Include,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            }*/;

            settings.Converters.Add(new JsonStringEncryptConverter(password));
            var tmp = DeserializeObject(jsonString, settings);
            return Serialize(tmp, settings);
        }

        public static string EncryptJsonValues(this object rawObject, string password = null)
        {
            var settings = simpleSerializerSettingsWithReferences.Copy();/*new JsonSerializerSettings
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
            var settings = simpleSerializerSettingsWithReferences.Copy();/*new JsonSerializerSettings
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
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObjectStrongTyped(object value, Encoding encoding, Stream targetStream, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (var tw = new StreamWriter(targetStream, encoding, 1024, true))
            {
                WriteObjectStrongTyped(value, tw, preserveReferences, useCamelCase);
                //tw.Write(serialized);
            }
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="writer">the text-writer that is used for writing the target object</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObjectStrongTyped(object value, TextWriter writer, bool preserveReferences = false,
            bool useCamelCase = false)
        {
            var serializer = GetSerializer(true, preserveReferences, useCamelCase);
            Serialize(value, serializer, writer);
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
        public static T ReadStrongTypedObject<T>(Stream sourceStream, Encoding encoding, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (var tr = new StreamReader(sourceStream, encoding,false, 1024,true))
            {
                return ReadStrongTypedObject<T>(tr, preserveReferences, useCamelCase);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="reader">the text-reader that points to a stream containing a json-object</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadStrongTypedObject<T>(TextReader reader, bool preserveReferences = false, bool useCamelCase = false)
        {
            var serializer = GetSerializer(true, preserveReferences, useCamelCase);
            return DeserializeObject<T>(serializer, reader);
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
            WriteObjectStrongTyped(value, Encoding.UTF8, targetStream, preserveReferences, useCamelCase);
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
            return ReadStrongTypedObject<T>(sourceStream, Encoding.UTF8, preserveReferences, useCamelCase);
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
                WriteObjectStrongTyped(value, Encoding.UTF8, targetStream, preserveReferences, useCamelCase);
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
                return ReadStrongTypedObject<T>(sourceStream, Encoding.UTF8, preserveReferences, useCamelCase);
            }
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="fileName">the name of the file where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObjectStrongTyped(object value, Encoding encoding, string fileName, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (FileStream targetStream = File.OpenWrite(fileName))
            {
                targetStream.SetLength(0);
                WriteObjectStrongTyped(value, encoding, targetStream, preserveReferences, useCamelCase);
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
        public static T ReadStrongTypedObject<T>(string fileName, Encoding encoding, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return ReadStrongTypedObject<T>(sourceStream, encoding, preserveReferences, useCamelCase);
            }
        }


        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObject(object value, Encoding encoding, Stream targetStream, bool preserveReferences = false, bool useCamelCase = false)
        {
            //string serialized = ToJson(value, preserveReferences, useCamelCase);
            using (var tw = new StreamWriter(targetStream, encoding,1024, true))
            {
                WriteObject(value, tw, preserveReferences, useCamelCase);
                //tw.Write(serialized);
            }
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="writer">the text-writer that is used for writing the target object</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObject(object value, TextWriter writer, bool preserveReferences = false,
            bool useCamelCase = false)
        {
            var serializer = GetSerializer(false, preserveReferences, useCamelCase);
            Serialize(value, serializer, writer);
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
            using (var tr = new StreamReader(sourceStream, encoding,false, 1024,true))
            {
                return ReadObject<T>(tr, preserveReferences, useCamelCase); //s = tr.ReadToEnd();
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="reader">the text-reader that points to a stream containing a json-object</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(TextReader reader, bool preserveReferences = false, bool useCamelCase = false)
        {
            var serializer = GetSerializer(false, preserveReferences, useCamelCase);
            return DeserializeObject<T>(serializer, reader);
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
            WriteObject(value, Encoding.UTF8, targetStream, preserveReferences, useCamelCase);
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
                WriteObject(value, Encoding.UTF8, targetStream, preserveReferences, useCamelCase);
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
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="fileName">the name of the file where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObject(object value, Encoding encoding, string fileName, bool preserveReferences = false, bool useCamelCase = false)
        {
            using (FileStream targetStream = File.OpenWrite(fileName))
            {
                targetStream.SetLength(0);
                WriteObject(value, encoding, targetStream, preserveReferences, useCamelCase);
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
                (!preserveReferences ? simpleSerializerSettings : simpleSerializerSettingsWithReferences).Copy();
            return ToJson(value, basicSettings, useCamelCase);
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
                (!preserveReferences ? simpleSerializerSettings : simpleSerializerSettingsWithReferences).Copy();
            return FromJson<T>(json, basicSettings, useCamelCase);
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
            var basicSettings =
                (!preserveReferences ? simpleSerializerSettings : simpleSerializerSettingsWithReferences).Copy();
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
            var basicSettings = (!preserveReferences
                ? strongTypedSerializerSettings
                : strongTypedSerializerSettingsWithReferences).Copy();
            return ToJson(value, basicSettings, useCamelCase);
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
            var basicSettings = (!preserveReferences
                ? strongTypedSerializerSettings
                : strongTypedSerializerSettingsWithReferences).Copy();
            return FromJson<T>(json, basicSettings, useCamelCase);
        }

        /// <summary>
        /// Basic implementation for json-serialization
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="basicSettings">the estimated basic-settings</param>
        /// <param name="useCamelCase">indicates whether to use camelCase-naming convention</param>
        /// <returns>a string representing the json-notation of the given object</returns>
        private static string ToJson(object value, JsonSerializerSettings basicSettings, bool useCamelCase)
        {
            if (useCamelCase)
            {
                basicSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            }

            return Serialize(value, basicSettings);
        }

        /// <summary>
        /// Basic Implementation for json-deserialization
        /// </summary>
        /// <typeparam name="T">the type to deserialize</typeparam>
        /// <param name="json">the json-text that represent the target object</param>
        /// <param name="basicSettings">the estimated basic-settings</param>
        /// <param name="useCamelCase">indicates whether to use camelCase-naming convention</param>
        /// <returns>the deserialized object</returns>
        private static T FromJson<T>(string json, JsonSerializerSettings basicSettings, bool useCamelCase)
        {
            if (useCamelCase)
            {
                basicSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            }

            return DeserializeObject<T>(json, basicSettings);
        }

        private static object FromJson(Type t, string json, JsonSerializerSettings basicSettings, bool useCamelCase)
        {
            if (useCamelCase)
            {
                basicSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            }

            return DeserializeObject(json, basicSettings, t);
        }

        private static T DeserializeObject<T>(string json, JsonSerializerSettings settings)
        {
            var data = Utf8NoBom.GetBytes(json);
            using MemoryStream mst = new MemoryStream(data);
            using (TextReader r = new StreamReader(mst, Utf8NoBom))
            {
                return DeserializeObject<T>(r, settings);
            }
        }

        private static object DeserializeObject(string json, JsonSerializerSettings settings, Type t = null)
        {
            var data = Utf8NoBom.GetBytes(json);
            using MemoryStream mst = new MemoryStream(data);
            using (TextReader r = new StreamReader(mst, Utf8NoBom))
            {
                return DeserializeObject(r, settings, t);
            }
        }

        private static T DeserializeObject<T>(TextReader r, JsonSerializerSettings settings)
        {
            JsonSerializer s = JsonSerializer.Create(settings);
            return DeserializeObject<T>(s, r);
        }

        private static object DeserializeObject(TextReader r, JsonSerializerSettings settings, Type t = null)
        {
            JsonSerializer s = JsonSerializer.Create(settings);
            return DeserializeObject(s, r, t);
        }

        private static T DeserializeObject<T>(JsonSerializer serializer, TextReader r)
        {
            return serializer.Deserialize<T>(new JsonTextReader(r));
        }

        private static object DeserializeObject(JsonSerializer serializer, TextReader r, Type t = null)
        {
            using var jr = new JsonTextReader(r);
            if (t != null)
            {
                return serializer.Deserialize(jr, t);
            }

            return serializer.Deserialize(jr);
        }

        private static string Serialize(object value, JsonSerializerSettings settings)
        {
            using MemoryStream mst = new MemoryStream();
            using (TextWriter w = new StreamWriter(mst, Utf8NoBom, -1, false))
            {
                Serialize(value, settings, w);
            }

            var data = mst.ToArray();
            return Utf8NoBom.GetString(data);
        }

        private static void Serialize(object value, JsonSerializerSettings settings, TextWriter writer)
        {
            var serializer = GetSerializer(settings);
            Serialize(value, serializer, writer);
        }

        private static void Serialize(object value, JsonSerializer serializer, TextWriter writer)
        {
            serializer.Serialize(writer, value);
        }

        private static JsonSerializer GetSerializer(bool strongTyped, bool preserveReferences, bool useCamelCase)
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

            return GetSerializer(tmp);
        }

        private static JsonSerializer GetSerializer(JsonSerializerSettings settings)
        {
            return JsonSerializer.Create(settings);
        }
    }
}
