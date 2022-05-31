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
        public JsonStringEncryptConverter()
        {
        }

        public JsonStringEncryptConverter(string targetEntropy)
        {
            this.targetEntropy = targetEntropy;
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
                if (string.IsNullOrEmpty(targetEntropy))
                {
                    stringValue = PasswordSecurity.Encrypt(stringValue.Substring(8));
                }
                else
                {
                    stringValue = PasswordSecurity.Encrypt(stringValue.Substring(8), targetEntropy);
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
}