using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Serializer-settings configuring newtonsoft to type-full-qualify each serialized object
        /// </summary>
        private static JsonSerializerSettings strongTypedSerializerSettings = new JsonSerializerSettings
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
        private static JsonSerializerSettings simpleSerializerSettings = new JsonSerializerSettings()
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
        private static JsonSerializerSettings strongTypedSerializerSettingsWithReferences = new JsonSerializerSettings
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
        private static JsonSerializerSettings simpleSerializerSettingsWithReferences = new JsonSerializerSettings
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
            var tmp = JsonConvert.DeserializeObject(jsonString, settings);
            return JsonConvert.SerializeObject(tmp, settings);
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
            string serialized = ToJsonStrongTyped(value, preserveReferences, useCamelCase);
            using (var tw = new StreamWriter(targetStream, encoding,1024, true))
            {
                tw.Write(serialized);
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
        public static T ReadStrongTypedObject<T>(Stream sourceStream, Encoding encoding, bool preserveReferences = false, bool useCamelCase = false)
        {
            string s;
            using (var tr = new StreamReader(sourceStream, encoding,false, 1024,true))
            {
                s = tr.ReadToEnd();
            }

            return FromJsonStringStrongTyped<T>(s,preserveReferences, useCamelCase);
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
            string serialized = ToJson(value, preserveReferences, useCamelCase);
            using (var tw = new StreamWriter(targetStream, encoding,1024, true))
            {
                tw.Write(serialized);
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
            string s;
            using (var tr = new StreamReader(sourceStream, encoding,false, 1024,true))
            {
                s = tr.ReadToEnd();
            }

            return FromJsonString<T>(s, preserveReferences, useCamelCase);
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

            return JsonConvert.SerializeObject(value, basicSettings);
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

            return JsonConvert.DeserializeObject<T>(json, basicSettings);
        }
    }
}
