using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.Security;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ITVComponents.Json.Converters
{
    public class JsonStringEncryptConverter : System.Text.Json.Serialization.JsonConverter<string>
    {
        private string targetEntropy = null;
        private byte[] encryptionKey = null;
        private JsonValueEncryptionMode mode;
        public JsonStringEncryptConverter()
        {
            mode = JsonValueEncryptionMode.defaultKey;
        }

        public JsonStringEncryptConverter(string targetEntropy)
        {
            this.targetEntropy = targetEntropy;
            mode = !string.IsNullOrEmpty(targetEntropy)
                ? JsonValueEncryptionMode.stringEntropy
                : JsonValueEncryptionMode.defaultKey;
        }

        public JsonStringEncryptConverter(byte[] encryptionKey)
        {
            this.encryptionKey = encryptionKey;
            mode = encryptionKey != null ? JsonValueEncryptionMode.binaryKey : JsonValueEncryptionMode.defaultKey;
        }

        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            return default;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            var stringValue = value;
            if (string.IsNullOrEmpty(stringValue))
            {
                writer.WriteNullValue();
                return;
            }

            if (stringValue.StartsWith("encrypt:", StringComparison.OrdinalIgnoreCase))
            {
                if (mode == JsonValueEncryptionMode.defaultKey)
                {
                    stringValue = stringValue.Substring(8).Encrypt();
                }
                else if (mode == JsonValueEncryptionMode.stringEntropy)
                {
                    stringValue = stringValue.Substring(8).Encrypt(targetEntropy);
                }
                else if (mode == JsonValueEncryptionMode.binaryKey)
                {
                    stringValue = AesEncryptor.Encrypt(stringValue.Substring(8), encryptionKey);
                }
            }

            writer.WriteStringValue(stringValue);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }
    }

    public enum JsonValueEncryptionMode
    {
        defaultKey,
        stringEntropy,
        binaryKey
    }
}