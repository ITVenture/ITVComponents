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
            var stringValue = Transform(value);
            if (stringValue == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(stringValue);
        }

        /// <summary>
        /// Applies the encryption transform to a single string value following the same rules as
        /// <see cref="Write"/>: null/empty becomes <c>null</c>, an <c>encrypt:</c>-prefixed value is
        /// encrypted using the configured mode, any other value is returned unchanged. Exposed so the
        /// JSON-DOM path can reuse the exact same logic (System.Text.Json does not run string
        /// converters over the values inside a parsed <see cref="System.Text.Json.Nodes.JsonNode"/>).
        /// </summary>
        public string Transform(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (value.StartsWith("encrypt:", StringComparison.OrdinalIgnoreCase))
            {
                if (mode == JsonValueEncryptionMode.defaultKey)
                {
                    return value.Substring(8).Encrypt();
                }
                if (mode == JsonValueEncryptionMode.stringEntropy)
                {
                    return value.Substring(8).Encrypt(targetEntropy);
                }
                if (mode == JsonValueEncryptionMode.binaryKey)
                {
                    return AesEncryptor.Encrypt(value.Substring(8), encryptionKey);
                }
            }

            return value;
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