using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Security;
using Newtonsoft.Json;

namespace ITVComponents.Settings
{
    public class JsonStringEncryptConverter : JsonConverter
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

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var stringValue = (string) value;
            if (string.IsNullOrEmpty(stringValue))
            {
                writer.WriteNull();
                return;
            }

            if (stringValue.StartsWith("encrypt:", StringComparison.OrdinalIgnoreCase))
            {
                if (mode == JsonValueEncryptionMode.defaultKey)
                {
                    stringValue = PasswordSecurity.Encrypt(stringValue.Substring(8));
                }
                else if (mode == JsonValueEncryptionMode.stringEntropy)
                {
                    stringValue = PasswordSecurity.Encrypt(stringValue.Substring(8), targetEntropy);
                }
                else if (mode == JsonValueEncryptionMode.binaryKey)
                {
                    stringValue = AesEncryptor.Encrypt(stringValue.Substring(8), encryptionKey);
                }
            }

            writer.WriteValue(stringValue);

            /*var buffer = Encoding.UTF8.GetBytes(stringValue);

            using (var inputStream = new MemoryStream(buffer, false))
            using (var outputStream = new MemoryStream())
            using (var aes = new AesManaged
            {
                Key = _encryptionKeyBytes
            })
            {
                var iv = aes.IV; // first access generates a new IV
                outputStream.Write(iv, 0, iv.Length);
                outputStream.Flush();

                var encryptor = aes.CreateEncryptor(_encryptionKeyBytes, iv);
                using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write))
                {
                    inputStream.CopyTo(cryptoStream);
                }

                Convert.ToBase64String(outputStream.ToArray()));
            }*/
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = reader.Value as string;
            /*if (string.IsNullOrEmpty(value))
            {
                return value;
            }*/
            return value;

            /*try {
                var buffer = Convert.FromBase64String(value);
    
                using (var inputStream = new MemoryStream(buffer, false))
                using (var outputStream = new MemoryStream())
                using (var aes = new AesManaged {
                    Key = _encryptionKeyBytes
                }) {
                    var iv = new byte[16];
                    var bytesRead = inputStream.Read(iv, 0, 16);
                    if (bytesRead < 16) {
                        throw new CryptographicException("IV is missing or invalid.");
                    }
    
                    var decryptor = aes.CreateDecryptor(_encryptionKeyBytes, iv);
                    using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read)) {
                        cryptoStream.CopyTo(outputStream);
                    }
    
                    var decryptedValue = Encoding.UTF8.GetString(outputStream.ToArray());
                    return decryptedValue;
                }
            }
            catch {
                return string.Empty;
            }*/
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