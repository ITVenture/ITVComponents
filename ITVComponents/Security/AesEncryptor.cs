using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.Security
{
    public static class AesEncryptor
    {
        /// <summary>
        /// Decrypts an AES encrypted string using the specified password
        /// </summary>
        /// <param name="input">the string to encrypt</param>
        /// <param name="password">the password for encryption</param>
        /// <returns>an encrypted string</returns>
        public static string Decrypt(string input, string password)
        {
            return Decrypt(input, ToEntropy(password));
        }

        /// <summary>
        /// Decrypts an AES encrypted blob using the specified password
        /// </summary>
        /// <param name="input">the blob to encrypt</param>
        /// <param name="password">the password for encryption</param>
        /// <returns>an encrypted blob</returns>
        public static byte[] Decrypt(byte[] input, string password)
        {
            return Decrypt(input, ToEntropy(password));
        }

        /// <summary>
        /// Decrypts an AES encrypted string using the specified password
        /// </summary>
        /// <param name="input">the string to encrypt</param>
        /// <param name="entropy">the password for encryption</param>
        /// <returns>an encrypted string</returns>
        public static string Decrypt(string input, byte[] entropy)
        {
            byte[] raw = Convert.FromBase64String(input);
            byte[] pre = Encoding.UTF8.GetPreamble();
            int offset = 0;
            var retVal = Decrypt(raw, entropy);
            if (pre.SequenceEqual(retVal.Take(pre.Length)))
            {
                offset = pre.Length;
                LogEnvironment.LogEvent("An encrypted value contains a BOM. Consider re-encrypting it.", LogSeverity.Warning);
            }

            return Encoding.UTF8.GetString(retVal, offset, retVal.Length - offset);
        }

        /// <summary>
        /// Decrypts an AES encrypted blob using the specified password
        /// </summary>
        /// <param name="input">the blob to encrypt</param>
        /// <param name="entropy">the password for encryption</param>
        /// <returns>an encrypted blob</returns>
        public static byte[] Decrypt(byte[] input, byte[] entropy)
        {
            byte saltLength = input[0];
            byte initLength = input[1];
            byte[] salt = new byte[saltLength];
            byte[] iv = new byte[initLength];
            byte[] encrypted = new byte[input.Length - 2 - saltLength - initLength];
            int offset = 2;
            Array.Copy(input,offset,salt,0,salt.Length);
            offset += salt.Length;
            Array.Copy(input, offset, iv, 0, iv.Length);
            offset += iv.Length;
            Array.Copy(input, offset, encrypted, 0, encrypted.Length);
            return Decrypt(encrypted, entropy, iv, salt);
        }

        /// <summary>
        /// Decrypts data using distributed values for entropy, iv and salt
        /// </summary>
        /// <param name="encrypted">the raw-encrypted value</param>
        /// <param name="entropy">the encryption password</param>
        /// <param name="initializationVector">the initialization vector that was used for this specific encryption-data</param>
        /// <param name="salt">the salt that was used for this specific encryption data</param>
        /// <returns>the decrypted data</returns>
        public static byte[] Decrypt(byte[] encrypted, byte[] entropy, byte[] initializationVector, byte[] salt)
        {
            using var cs = GetDecryptStream(new MemoryStream(encrypted), entropy, initializationVector, salt);
            using var os = new MemoryStream();
            cs.CopyTo(os);
            return os.ToArray();
        }

        /// <summary>
        /// Decrypts data using distributed values for entropy, iv and salt
        /// </summary>
        /// <param name="targetStream">the target stream, where the encrypted data is read from</param>
        /// <param name="entropy">the encryption password</param>
        /// <param name="initializationVector">the initialization vector that was used for this specific encryption-data</param>
        /// <param name="salt">the salt that was used for this specific encryption data</param>
        /// <param name="leaveOpen">indicates whether to leave the inner stream open when the created cryptostream is being disposed</param>
        /// <returns>a stream that is capable for reading the decrypted data</returns>
        public static Stream GetDecryptStream(Stream targetStream, byte[] entropy, byte[] initializationVector, byte[] salt, bool leaveOpen = false)
        {
            using (var asm = new AesManaged())
            {
                using (Rfc2898DeriveBytes pwd = new Rfc2898DeriveBytes(entropy, salt, 10000))
                {
                    var key = pwd.GetBytes(32);
                    ICryptoTransform decryptor = asm.CreateDecryptor(key, initializationVector);
                    return new CryptoStream(targetStream, decryptor, CryptoStreamMode.Read, leaveOpen);
                }
            }
        }

        /// <summary>
        /// Decrypts data using distributed values for entropy, iv and salt
        /// </summary>
        /// <param name="targetStream">the target stream, where the encrypted data is read from</param>
        /// <param name="entropy">the encryption password</param>
        /// <param name="leaveOpen">indicates whether to leave the inner stream open when the created cryptostream is being disposed</param>
        /// <returns>a stream that is capable for reading the decrypted data</returns>
        public static Stream GetDecryptStream(Stream targetStream, byte[] entropy, bool leaveOpen = false)
        {
            using (var asm = new AesManaged())
            {
                var decorator = new CryptDecoratorStream(targetStream, leaveOpen);
                using (Rfc2898DeriveBytes pwd = new Rfc2898DeriveBytes(entropy, decorator.Salt, 10000))
                {
                    var key = pwd.GetBytes(32);
                    ICryptoTransform decryptor = asm.CreateDecryptor(key, decorator.InitializationVector);
                    return new CryptoStream(decorator, decryptor, CryptoStreamMode.Read, false);
                }
            }
        }

        /// <summary>
        /// Encrypts a string using the given password
        /// </summary>
        /// <param name="input">the string to encrypt</param>
        /// <param name="password">the password that is used for encryption</param>
        /// <returns>the encrypted string</returns>
        public static string Encrypt(string input, string password)
        {
            return Encrypt(input, ToEntropy(password));
        }

        /// <summary>
        /// Encrypts a blob using the given password
        /// </summary>
        /// <param name="input">the blob to encrypt</param>
        /// <param name="password">the password that is used for encryption</param>
        /// <returns>the encrypted blob</returns>
        public static byte[] Encrypt(byte[] input, string password)
        {
            return Encrypt(input, ToEntropy(password));
        }

        /// <summary>
        /// Encrypts a string using the given password
        /// </summary>
        /// <param name="input">the string to encrypt</param>
        /// <param name="entropy">the password that is used for encryption</param>
        /// <param name="useDriveKey">indicates whether to use Key-derivation for the provided key</param>
        /// <returns>the encrypted string</returns>
        public static string Encrypt(string input, byte[] entropy)
        {
            var retVal = Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(input), entropy));
            return $"{retVal}";
        }

        /// <summary>
        /// Encrypts a blob using the given password
        /// </summary>
        /// <param name="input">the blob to encrypt</param>
        /// <param name="entropy">the password that is used for encryption</param>
        /// <param name="useDriveKey">indicates whether to use Key-derivation for the provided key</param>
        /// <returns>the encrypted blob</returns>
        public static byte[] Encrypt(byte[] input, byte[] entropy)
        {
            var encrypted = Encrypt(input, entropy, out var iv, out var salt);
            var totalBytes = new byte[salt.Length + iv.Length + encrypted.Length + 2];
            totalBytes[0] = (byte)salt.Length;
            totalBytes[1] = (byte)iv.Length;
            int offset = 2;
            Array.Copy(salt, 0, totalBytes, offset, salt.Length);
            offset += salt.Length;
            Array.Copy(iv, 0, totalBytes, offset, iv.Length);
            offset += iv.Length;
            Array.Copy(encrypted, 0, totalBytes, offset, encrypted.Length);
            return totalBytes;
        }

        /// <summary>
        /// Decrypts data using distributed values for entropy, iv and salt
        /// </summary>
        /// <param name="clearText">the raw-clear-text value</param>
        /// <param name="entropy">the encryption password</param>
        /// <param name="initializationVector">the initialization vectory that was used for this specific encryption-data</param>
        /// <param name="salt">the salt that was used for this specific encryption data</param>
        /// <returns>the decrypted data</returns>
        public static byte[] Encrypt(byte[] clearText, byte[] entropy, out byte[] initializationVector, out byte[] salt)
        {
            using var ms = new MemoryStream();
            using (var cs = GetEncryptStream(ms, entropy, out initializationVector, out salt, true))
            {
                cs.Write(clearText);
            }

            var encrypted = ms.ToArray();
            return encrypted;
        }

        /// <summary>
        /// Decrypts data using distributed values for entropy, iv and salt
        /// </summary>
        /// <param name="targetStream">the target stream to which the encrypted data will be written</param>
        /// <param name="entropy">the encryption password</param>
        /// <param name="initializationVector">the initialization vectory that was used for this specific encryption-data</param>
        /// <param name="salt">the salt that was used for this specific encryption data</param>
        /// <param name="leaveOpen">indicates whether to leave the provided stream open when the returned one is being disposed</param>
        /// <returns>a stream that encrypts written data</returns>
        public static Stream GetEncryptStream(Stream targetStream, byte[] entropy, out byte[] initializationVector,
            out byte[] salt, bool leaveOpen = false)
        {
            using (AesManaged asm = new AesManaged())
            {
                salt = RandomSalt();
                using (Rfc2898DeriveBytes pwd = new Rfc2898DeriveBytes(entropy, salt, 10000))
                {
                    var key = pwd.GetBytes(32);
                    ICryptoTransform encryptor = asm.CreateEncryptor(key, asm.IV);
                    initializationVector = (byte[])asm.IV.Clone();
                    return new CryptoStream(targetStream, encryptor, CryptoStreamMode.Write, leaveOpen);
                }
            }
        }

        public static Stream GetEncryptStream(Stream targetStream, byte[] entropy, bool leaveOpen = false)
        {
            using (Aes asm = Aes.Create())
            {
                var salt = RandomSalt();
                var decorator = new CryptDecoratorStream(targetStream, (byte[])asm.IV.Clone(), salt, leaveOpen);
                using (Rfc2898DeriveBytes pwd = new Rfc2898DeriveBytes(entropy, salt, 10000))
                {
                    var key = pwd.GetBytes(32);
                    ICryptoTransform encryptor = asm.CreateEncryptor(key, decorator.InitializationVector);
                    return new CryptoStream(decorator, encryptor, CryptoStreamMode.Write, false);
                }
            }
        }

        public static byte[] CreateKey()
        {
            using (AesManaged asm = new AesManaged())
            {
                asm.KeySize = 256;
                asm.GenerateKey();
                return (byte[])asm.Key.Clone();
            }
        }

        private static byte[] ToEntropy(string password)
        {
            return Encoding.UTF32.GetBytes(password);
        }
        
        private static byte[] RandomSalt()
        {
            int _saltSize = 32;
            byte[] ba = new byte[_saltSize];
            using var tmp = RandomNumberGenerator.Create();
            tmp.GetBytes(ba);
            return ba;
        }
    }
}
