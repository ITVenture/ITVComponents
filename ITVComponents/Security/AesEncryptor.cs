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
    internal static class AesEncryptor
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
            using (var asm = new AesManaged())
            {
                using (Rfc2898DeriveBytes pwd = new Rfc2898DeriveBytes(entropy, salt, 10000))
                {
                    var key = pwd.GetBytes(32);
                    ICryptoTransform decryptor = asm.CreateDecryptor(key, iv);
                    using var ms = new MemoryStream(encrypted);
                    using var cs = new CryptoStream(ms,decryptor, CryptoStreamMode.Read);
                    using var os = new MemoryStream();
                    cs.CopyTo(os);
                    return os.ToArray();
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
        /// <returns>the encrypted blob</returns>
        public static byte[] Encrypt(byte[] input, byte[] entropy)
        {
            using (AesManaged asm = new AesManaged())
            {
                var salt = RandomSalt();
                using (Rfc2898DeriveBytes  pwd = new Rfc2898DeriveBytes(entropy,salt, 10000))
                {
                    var key = pwd.GetBytes(32);
                    ICryptoTransform encryptor = asm.CreateEncryptor(key, asm.IV);
                    using var ms = new MemoryStream();
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(input);
                    }

                    var encrypted = ms.ToArray();
                    var totalBytes = new byte[salt.Length + asm.IV.Length + encrypted.Length + 2];
                    totalBytes[0] = (byte) salt.Length;
                    totalBytes[1] = (byte) asm.IV.Length;
                    int offset = 2;
                    Array.Copy(salt,0,totalBytes,offset,salt.Length);
                    offset += salt.Length;
                    Array.Copy(asm.IV,0,totalBytes,offset, asm.IV.Length);
                    offset += asm.IV.Length;
                    Array.Copy(encrypted,0,totalBytes,offset, encrypted.Length);
                    return totalBytes;
                }
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
