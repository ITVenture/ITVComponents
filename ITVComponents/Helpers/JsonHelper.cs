using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings;
using Newtonsoft.Json;

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
        private static JsonSerializerSettings simpleSerializerSettings = new JsonSerializerSettings
        {
            CheckAdditionalContent = true,
            ConstructorHandling = ConstructorHandling.Default,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Include
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
            var settings = new JsonSerializerSettings
            {
                CheckAdditionalContent = true,
                ConstructorHandling = ConstructorHandling.Default,
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                NullValueHandling = NullValueHandling.Include,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            };

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
        public static void WriteObjectStrongTyped(object value, Encoding encoding, Stream targetStream, bool preserveReferences = false)
        {
            string serialized = ToJsonStrongTyped(value, preserveReferences);
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
        /// <returns>the deserialized object</returns>
        public static T ReadStrongTypedObject<T>(Stream sourceStream, Encoding encoding, bool preserveReferences = false)
        {
            string s;
            using (var tr = new StreamReader(sourceStream, encoding,false, 1024,true))
            {
                s = tr.ReadToEnd();
            }

            return FromJsonStringStrongTyped<T>(s,preserveReferences);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        public static void WriteObjectStrongTyped(object value, Stream targetStream, bool preserveReferences = false)
        {
            WriteObjectStrongTyped(value, Encoding.UTF8, targetStream, preserveReferences);
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="sourceStream">the source-stream from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <returns>the deserialized object</returns>
        public static T ReadStrongTypedObject<T>(Stream sourceStream, bool preserveReferences = false)
        {
            return ReadStrongTypedObject<T>(sourceStream, Encoding.UTF8, preserveReferences);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="fileName">the name of the file where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        public static void WriteObjectStrongTyped(object value, string fileName, bool preserveReferences = false)
        {
            using (FileStream targetStream = File.OpenWrite(fileName))
            {
                targetStream.SetLength(0);
                WriteObjectStrongTyped(value, Encoding.UTF8, targetStream, preserveReferences);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <returns>the deserialized object</returns>
        public static T ReadStrongTypedObject<T>(string fileName, bool preserveReferences = false)
        {
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return ReadStrongTypedObject<T>(sourceStream, Encoding.UTF8, preserveReferences);
            }
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="fileName">the name of the file where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        public static void WriteObjectStrongTyped(object value, Encoding encoding, string fileName, bool preserveReferences = false)
        {
            using (FileStream targetStream = File.OpenWrite(fileName))
            {
                targetStream.SetLength(0);
                WriteObjectStrongTyped(value, encoding, targetStream, preserveReferences);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <returns>the deserialized object</returns>
        public static T ReadStrongTypedObject<T>(string fileName, Encoding encoding, bool preserveReferences = false)
        {
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return ReadStrongTypedObject<T>(sourceStream, encoding, preserveReferences);
            }
        }

        
        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        public static void WriteObject(object value, Encoding encoding, Stream targetStream, bool preserveReferences = false)
        {
            string serialized = ToJson(value, preserveReferences);
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
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(Stream sourceStream, Encoding encoding, bool preserveReferences = false)
        {
            string s;
            using (var tr = new StreamReader(sourceStream, encoding,false, 1024,true))
            {
                s = tr.ReadToEnd();
            }

            return FromJsonString<T>(s, preserveReferences);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        public static void WriteObject(object value, Stream targetStream, bool preserveReferences = false)
        {
            WriteObject(value, Encoding.UTF8, targetStream, preserveReferences);
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="sourceStream">the source-stream from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(Stream sourceStream, bool preserveReferences = false)
        {
            return ReadObject<T>(sourceStream, Encoding.UTF8, preserveReferences);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="fileName">the name of the file where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        public static void WriteObject(object value, string fileName, bool preserveReferences = false)
        {
            using (FileStream targetStream = File.OpenWrite(fileName))
            {
                targetStream.SetLength(0);
                WriteObject(value, Encoding.UTF8, targetStream, preserveReferences);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(string fileName, bool preserveReferences = false)
        {
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return ReadObject<T>(sourceStream, Encoding.UTF8, preserveReferences);
            }
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="fileName">the name of the file where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        public static void WriteObject(object value, Encoding encoding, string fileName, bool preserveReferences = false)
        {
            using (FileStream targetStream = File.OpenWrite(fileName))
            {
                targetStream.SetLength(0);
                WriteObject(value, encoding, targetStream, preserveReferences);
            }
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="encoding">the encoding to use on the file</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(string fileName, Encoding encoding, bool preserveReferences = false)
        {
            using (FileStream sourceStream = File.OpenRead(fileName))
            {
                return ReadObject<T>(sourceStream, encoding, preserveReferences);
            }
        }

        /// <summary>
        /// Converts the given object to a simple Json-string with or without preserving the references
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="preserveReferences">indicates whether or not to preserve the object references</param>
        /// <returns>the json-string representation of the given object</returns>
        public static string ToJson(object value, bool preserveReferences = false)
        {
            return JsonConvert.SerializeObject(value, !preserveReferences ? simpleSerializerSettings : simpleSerializerSettingsWithReferences);
        }

        /// <summary>
        /// Converts the given string into an object of the target type with or without reference perseverance
        /// </summary>
        /// <typeparam name="T">the target type to convert the json string into</typeparam>
        /// <param name="json">the json representing the object</param>
        /// <param name="preserveReferences">indicates whether the references where preserved in the serialization</param>
        /// <returns>the deserialized object</returns>
        public static T FromJsonString<T>(string json, bool preserveReferences = false)
        {
            return JsonConvert.DeserializeObject<T>(json, !preserveReferences ? simpleSerializerSettings : simpleSerializerSettingsWithReferences);
        }

        /// <summary>
        /// Converts the given object to a strong-typed Json-string with or without preserving the references
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="preserveReferences">indicates whether or not to preserve the object references</param>
        /// <returns>the json-string representation of the given object</returns>
        public static string ToJsonStrongTyped(object value, bool preserveReferences = false)
        {
            return JsonConvert.SerializeObject(value, !preserveReferences ? strongTypedSerializerSettings : strongTypedSerializerSettingsWithReferences);
        }

        /// <summary>
        /// Converts the given string into an object of the target type with or without reference perseverance
        /// </summary>
        /// <typeparam name="T">the target type to convert the json string into</typeparam>
        /// <param name="json">the json representing the object</param>
        /// <param name="preserveReferences">indicates whether the references where preserved in the serialization</param>
        /// <returns>the deserialized object</returns>
        public static T FromJsonStringStrongTyped<T>(string json, bool preserveReferences = false)
        {
            return JsonConvert.DeserializeObject<T>(json, !preserveReferences ? strongTypedSerializerSettings : strongTypedSerializerSettingsWithReferences);
        }
    }
}
