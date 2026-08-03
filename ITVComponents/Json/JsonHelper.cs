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
using ITVComponents.Json.Strategy;
using ITVComponents.Json.Strategy.Impl;
using ITVComponents.Logging;
using ITVComponents.Settings;
using Microsoft.Extensions.FileProviders;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;

namespace ITVComponents.Json
{
    public static class JsonHelper
    {
        private const string NativeStrategy = "DefaultImpl";

        private static Dictionary<string, IJsonStrategy> strategies = new Dictionary<string, IJsonStrategy>
            { { NativeStrategy, new DefaultJsonStrategy() } };

        private static string defaultStrategy = NativeStrategy;

        private static T Strategy<T>(string strategyName) where T : IJsonStrategy
        {
            if (!string.IsNullOrEmpty(strategyName) && strategies.ContainsKey(strategyName))
            {
                return (T)strategies[strategyName];
            }

            return (T)strategies[defaultStrategy];
        }

        public static void ExtendNativeProtocolType<TProto, TExt>(string discriminator) where TExt:TProto
        {
            Strategy<DefaultJsonStrategy>(NativeStrategy).ExtendNativeProtocolType<TProto,TExt>(discriminator);
        }

        /// <summary>
        /// Meldet einen Typ fuer <see cref="SerializationTypingMode.AssistedPolymorphism"/> unter einem
        /// stabilen <b>Kurznamen</b> an. Ohne Registrierung wird der <c>AssemblyQualifiedName</c>
        /// geschrieben (unveraendertes Verhalten).
        /// </summary>
        /// <remarks>
        /// Fuer alles, was laenger liegt als ein Prozessaufruf: der Kurzname ueberlebt das Umbenennen und
        /// Verschieben der Klasse, und er ist zusammen mit
        /// <see cref="RestrictManualTypesToRegistered"/> eine Positivliste gegen das Laden beliebiger
        /// Typen aus fremden Daten. Der Name gehoert zum Datenformat und darf sich nachtraeglich nicht
        /// mehr aendern.
        /// </remarks>
        public static void RegisterManualType<T>(string alias) => ManualTypeRegistry.Register<T>(alias);

        /// <summary>Meldet einen Typ unter einem stabilen Kurznamen an (siehe <see cref="RegisterManualType{T}"/>).</summary>
        public static void RegisterManualType(Type type, string alias) => ManualTypeRegistry.Register(type, alias);

        /// <summary>
        /// Beschraenkt das Auflösen von Typen im aktuellen Ausfuehrungsfluss auf registrierte Kurznamen -
        /// ein <c>AssemblyQualifiedName</c> in den Daten wird dann nicht geladen. Beim Dispose gilt wieder
        /// der vorige Zustand.
        /// </summary>
        public static IDisposable RestrictManualTypesToRegistered() => ManualTypeRegistry.RestrictToRegisteredTypes();

        public static string EncryptJsonValues(this string jsonString, string password = null, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).EncryptJsonValues(jsonString, password);
        }

        public static string EncryptJsonValues(this object rawObject, string password = null, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).EncryptJsonValues(rawObject, password);
        }

        public static string EncryptJsonValues(this object rawObject, byte[] encryptionKey = null, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).EncryptJsonValues(rawObject, encryptionKey);
        }

        /// <summary>
        /// Serializes an instance of Type TProtocol to string using the provided SerializerOptions
        /// </summary>
        /// <typeparam name="TProtocol">the Type from which to use the type-settings for the serializer</typeparam>
        /// <param name="value">the value to serialize</param>
        /// <param name="options">the options used for serialization</param>
        /// <returns>the serialized string</returns>
        public static string ToJson<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options, string strategy = null) where TSerializerOptions:class
        {
            return Strategy<IJsonStrategy>(strategy).ToJson<TProtocol, TSerializerOptions>(value, options);
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
            bool preserveReferences = false, bool useCamelCase = false, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).ToJson<TProtocol>(value, typingMode, preserveReferences, useCamelCase);
        }

        public static string ToJson(object value, SerializationTypingMode typingMode, Type? type,
            bool preserveReferences = false, bool useCamelCase = false, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).ToJson(value, typingMode, type, preserveReferences, useCamelCase);
        }

        /// <summary>
        /// Writes an object to a stream using strong-typed json settings
        /// </summary>
        /// <param name="value">the value to serialize</param>
        /// <param name="targetStream">the target stream where the content is written to</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        public static void WriteObject<TProto>(TProto value, SerializationTypingMode typingMode, Stream targetStream, bool preserveReferences = false, bool useCamelCase = false, string strategy = null)
        {
            Strategy<IJsonStrategy>(strategy).WriteObject(value, typingMode, targetStream, preserveReferences, useCamelCase);
        }

        public static void WriteObject<TProtocol>(TProtocol value, SerializationTypingMode typingMode, string fileName,
            bool preserveReferences = false, bool useCamelCase = false, string strategy = null)
        {
            Strategy<IJsonStrategy>(strategy).WriteObject(value, typingMode, fileName, preserveReferences, useCamelCase);
        }

        public static void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options, string fileName, string strategy = null) where TSerializerOptions:class
        {
            Strategy<IJsonStrategy>(strategy).WriteObject(value, options, fileName);
        }

        public static void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options, Stream targetStream, string strategy = null) where TSerializerOptions:class
        {
            Strategy<IJsonStrategy>(strategy).WriteObject(value, options, targetStream);
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
            bool preserveReferences = false, bool useCamelCase = false, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).ReadObject<T>(sourceStream, typingMode, preserveReferences, useCamelCase);
        }

        public static T ReadObject<T, TSerializerOptions>(Stream sourceStream, TSerializerOptions options, string strategy = null) where TSerializerOptions:class
        {
            return Strategy<IJsonStrategy>(strategy).ReadObject<T,TSerializerOptions>(sourceStream, options);
        }

        /// <summary>
        /// Reads an object from a stream. Uses the strong-typed json settings
        /// </summary>
        /// <typeparam name="T">the target type to convert the data into</typeparam>
        /// <param name="fileName">the name of the source-file from which the data is read</param>
        /// <param name="preserveReferences">indicates whether to keep the object references in the serialized string</param>
        /// <param name="useCamelCase">indicates whether to use camelCase notation for properties</param>
        /// <returns>the deserialized object</returns>
        public static T ReadObject<T>(string fileName, SerializationTypingMode typingMode, bool preserveReferences = false, bool useCamelCase = false, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).ReadObject<T>(fileName, typingMode, preserveReferences, useCamelCase);
        }

        public static T FromJsonString<T>(string json, SerializationTypingMode typingMode,
            bool preserveReferences = false, bool useCamelCase = false, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).FromJsonString<T>(json , typingMode, preserveReferences, useCamelCase);
        }

        public static T FromJsonString<T, TSerializerOptions>(string json, TSerializerOptions options, string strategy = null) where TSerializerOptions : class
        {
            return Strategy<IJsonStrategy>(strategy).FromJsonString<T, TSerializerOptions>(json, options);
        }

        public static object FromJsonString(string json, Type t, SerializationTypingMode typingMode,
            bool preserveReferences = false,
            bool useCamelCase = false, string strategy = null)
        {
            return Strategy<IJsonStrategy>(strategy).FromJsonString(json, t, typingMode, preserveReferences, useCamelCase);
        }

        public static object FromJsonString<TSerializerOptions>(string json, Type t, TSerializerOptions options, string strategy = null) where TSerializerOptions:class
        {
            return Strategy<IJsonStrategy>(strategy).FromJsonString(json, t, options);
        }

        public static TSerializerOptions WithStrongContract<TSerializerOptions>(TSerializerOptions options, string strategy = null) where TSerializerOptions:class
        {
            return Strategy<IJsonStrategy>(strategy).WithStrongContract(options);
        }
    }

    public enum SerializationTypingMode
    {
        StaticTyping,
        AssistedPolymorphism,
        NativePolymorphism
    }
}
