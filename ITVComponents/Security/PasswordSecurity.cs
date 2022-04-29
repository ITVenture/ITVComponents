using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using ITVComponents.Logging;

namespace ITVComponents.Security
{
    public static class PasswordSecurity
    {
        /// <summary>
        /// Entropy used to protect data
        /// </summary>
        private static byte[] entropy;

        /// <summary>
        /// indicates whether this class has alrady been initialized
        /// </summary>
        private static bool initialized = false;

        /// <summary>
        /// indicates whether to use aes
        /// </summary>
        private static bool useAes = false;

        /// <summary>
        /// the scopen in which data is protected
        /// </summary>
        private static DataProtectionScope scope;
        
        /// <summary>
        /// Initializes the security module with a specific entropy
        /// </summary>
        /// <param name="entropy">the entropy used to decrypt values</param>
        public static void Initialize(string entropy, DataProtectionScope scope)
        {
            if (!initialized)
            {
                if (entropy != null)
                {
                    PasswordSecurity.entropy = Encoding.UTF32.GetBytes(entropy);
                }
                else
                {
                    LogEnvironment.LogDebugEvent(null, "Passwordsecurity is initialized without entropy!", (int) LogSeverity.Warning, null);
                }

                PasswordSecurity.scope = scope;
                initialized = true;
            }
        }

        /// <summary>
        /// Initializes AES Encryption for passwords and other strings that require protection
        /// </summary>
        /// <param name="passphrase">the AES-Passowrd for encryption and decryption</param>
        public static void InitializeAes(string passphrase)
        {
            if (!initialized)
            {
                entropy = Encoding.UTF32.GetBytes(passphrase);
                useAes = true;
                initialized = true;
            }
        }

        /// <summary>
        /// Creates a securestring from a clear-text string
        /// </summary>
        /// <param name="input">the input string</param>
        /// <returns>a secure-string instance</returns>
        public static SecureString Secure(this string input)
        {
            return ToSecureString(input);
        }

        /// <summary>
        /// Encrypts a string using the selected strategy
        /// </summary>
        /// <param name="input">the input string to encrypt</param>
        /// <returns>an encrypted string that represents the provided value</returns>
        public static string Encrypt(this string input)
        {
            if (!useAes)
            {
                return EncryptString(ToSecureString(input));
            }

            return AesEncryptor.Encrypt(input, entropy);
        }

        /// <summary>
        /// Encrypts a string using the selected strategy
        /// </summary>
        /// <param name="input">the input string to encrypt</param>
        /// <param name="password">the password used for encryption</param>
        /// <returns>an encrypted string that represents the provided value</returns>
        public static string Encrypt(this string input, string password)
        {
            if (!useAes)
            {
                throw new InvalidOperationException("Only available for AES encryption");
            }

            return AesEncryptor.Encrypt(input, password);
        }

        /// <summary>
        /// Decrypts the given string to cleartext
        /// </summary>
        /// <param name="input">the encrypted value</param>
        /// <returns>the cleartext representation of the provided encrypted string</returns>
        public static string Decrypt(this string input)
        {
            if (!useAes)
            {
                return ToInsecureString(DecryptString(input));
            }

            return AesEncryptor.Decrypt(input, entropy);
            //return DecryptAes(input);
        }

        /// <summary>
        /// Decrypts the given string to cleartext
        /// </summary>
        /// <param name="input">the encrypted value</param>
        /// <param name="password">the password used for encryption</param>
        /// <returns>the cleartext representation of the provided encrypted string</returns>
        public static string Decrypt(this string input, string password)
        {
            if (!useAes)
            {
                throw new InvalidOperationException("Only available for AES encryption");
            }

            return AesEncryptor.Decrypt(input, password);
        }
        
        /////////////
        /// <summary>
        /// Encrypts a string using the selected strategy
        /// </summary>
        /// <param name="input">the input string to encrypt</param>
        /// <returns>an encrypted string that represents the provided value</returns>
        public static byte[] Encrypt(this byte[] input)
        {
            if (!useAes)
            {
                throw new InvalidOperationException("Only available for AES encryption");
            }

            return AesEncryptor.Encrypt(input, entropy);
        }

        /// <summary>
        /// Encrypts a string using the selected strategy
        /// </summary>
        /// <param name="input">the input string to encrypt</param>
        /// <param name="password">the password used for encryption</param>
        /// <returns>an encrypted string that represents the provided value</returns>
        public static byte[] Encrypt(this byte[] input, string password)
        {
            if (!useAes)
            {
                throw new InvalidOperationException("Only available for AES encryption");
            }

            return AesEncryptor.Encrypt(input, password);
        }

        /// <summary>
        /// Decrypts the given string to cleartext
        /// </summary>
        /// <param name="input">the encrypted value</param>
        /// <returns>the cleartext representation of the provided encrypted string</returns>
        public static byte[] Decrypt(this byte[] input)
        {
            if (!useAes)
            {
                throw new InvalidOperationException("Only available for AES encryption");
            }

            return AesEncryptor.Decrypt(input, entropy);
            //return DecryptAes(input);
        }

        /// <summary>
        /// Decrypts the given string to cleartext
        /// </summary>
        /// <param name="input">the encrypted value</param>
        /// <param name="password">the password used for encryption</param>
        /// <returns>the cleartext representation of the provided encrypted string</returns>
        public static byte[] Decrypt(this byte[] input, string password)
        {
            if (!useAes)
            {
                throw new InvalidOperationException("Only available for AES encryption");
            }

            return AesEncryptor.Decrypt(input, password);
        }

        /// <summary>
        /// Checks whether the encryption environment was initialized
        /// </summary>
        private static void CheckInitialized()
        {
            if (!initialized)
            {
                LogEnvironment.LogDebugEvent(null, "Passwordsecurity is used without Initialization. Initializing with default-parameters...", (int) LogSeverity.Warning, null);
                Initialize(null,DataProtectionScope.LocalMachine);
            }
        }

        /// <summary>
        /// Converts a string to a secureString
        /// </summary>
        /// <param name="input">the string to convert</param>
        /// <returns>a secureString value representing the input value</returns>
        private static SecureString ToSecureString(string input)
        {
            SecureString secure = new SecureString();
            foreach (char c in input)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }

        /// <summary>
        /// Reads the value of a secureString
        /// </summary>
        /// <param name="input">the securestring containing a password</param>
        /// <returns>the secure string value</returns>
        private static string ToInsecureString(SecureString input)
        {
            string returnValue = string.Empty;
            IntPtr ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(input);
            try
            {
                returnValue = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
            }
            return returnValue;
        }

        /// <summary>
        /// Encrypts a securestring to a password
        /// </summary>
        /// <param name="input">the string to secure</param>
        /// <returns>a string containing the encrypted value</returns>
        private static string EncryptString(SecureString input)
        {
            CheckInitialized();
            byte[] encryptedData = ProtectedData.Protect(
                Encoding.Unicode.GetBytes(ToInsecureString(input)),
                entropy,
                scope);
            return Convert.ToBase64String(encryptedData);
        }

        /// <summary>
        /// Decrypts an encrypted base64 string and puts the result into a securestring
        /// </summary>
        /// <param name="encryptedData">the encrypted base64 string</param>
        /// <returns>a securestring representing the value</returns>
        private static SecureString DecryptString(string encryptedData)
        {
            CheckInitialized();
            try
            {
                if (!encryptedData.StartsWith("plain:"))
                {
                    byte[] decryptedData = ProtectedData.Unprotect(
                        Convert.FromBase64String(encryptedData),
                        entropy,
                        scope);
                    return ToSecureString(Encoding.Unicode.GetString(decryptedData));
                }

                return ToSecureString(encryptedData.Substring(6));
            }
            catch
            {
                return new SecureString();
            }
        }
    }
}