using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ITVComponents.Json.Strategy
{
    public interface IJsonStrategy
    {
        string EncryptJsonValues(string jsonString, string password);

        string EncryptJsonValues(object rawObject, string password);

        string EncryptJsonValues(object rawObject, byte[] encryptionKey);

        string ToJson<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options)
            where TSerializerOptions : class;

        string ToJson<TProtocol>(TProtocol value, SerializationTypingMode typingMode,
            bool preserveReferences, bool useCamelCase);

        string ToJson(object value, SerializationTypingMode typingMode, Type? type,
            bool preserveReferences, bool useCamelCase);

        void WriteObject<TProto>(TProto value, SerializationTypingMode typingMode, Stream targetStream,
            bool preserveReferences, bool useCamelCase);

        void WriteObject<TProtocol>(TProtocol value, SerializationTypingMode typingMode, string fileName,
            bool preserveReferences, bool useCamelCase);

        void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options, string fileName)
            where TSerializerOptions : class;

        void WriteObject<TProtocol, TSerializerOptions>(TProtocol value, TSerializerOptions options,
            Stream targetStream) where TSerializerOptions : class;

        T ReadObject<T>(Stream sourceStream, SerializationTypingMode typingMode,
            bool preserveReferences, bool useCamelCase);

        T ReadObject<T, TSerializerOptions>(Stream sourceStream, TSerializerOptions options)
            where TSerializerOptions : class;

        T ReadObject<T>(string fileName, SerializationTypingMode typingMode, bool preserveReferences,
            bool useCamelCase);

        T FromJsonString<T>(string json, SerializationTypingMode typingMode,
            bool preserveReferences, bool useCamelCase);

        T FromJsonString<T, TSerializerOptions>(string json, TSerializerOptions options)
            where TSerializerOptions : class;

        object FromJsonString(string json, Type t, SerializationTypingMode typingMode,
            bool preserveReferences,
            bool useCamelCase);

        object FromJsonString<TSerializerOptions>(string json, Type t, TSerializerOptions options)
            where TSerializerOptions : class;

        TSerializerOptions WithStrongContract<TSerializerOptions>(TSerializerOptions options)
            where TSerializerOptions : class;
    }
}
