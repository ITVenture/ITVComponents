using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ITVComponents.Logging;

namespace ITVComponents.DataAccess.Helpers
{
    public static class HashHelper
    {
        /// <summary>
        /// Calculates the hash of a string (UTF-16) with the named algorithm (e.g. "SHA256", "MD5") as lower-case hex.
        /// An unknown name falls back to SHA-1, as before.
        /// </summary>
        /// <remarks>
        /// A new algorithm object per call: a <see cref="HashAlgorithm"/> instance is not thread-safe, a shared one gave
        /// wrong hashes under parallel calls.
        /// </remarks>
        public static string CalculateHash(string s, string hashAlgorithm)
        {
            using HashAlgorithm algo = CreateAlgorithm(hashAlgorithm);
            byte[] bytes = Encoding.Unicode.GetBytes(s);
            byte[] hash = algo.ComputeHash(bytes);
            return string.Concat(from t in hash select $"{t:x2}");
        }

        private static HashAlgorithm CreateAlgorithm(string hashAlgorithm)
        {
            if (!string.IsNullOrEmpty(hashAlgorithm) && CryptoConfig.CreateFromName(hashAlgorithm) is HashAlgorithm named)
            {
                return named;
            }

            LogEnvironment.LogEvent($"Hash algorithm '{hashAlgorithm}' is not known; SHA-1 is used instead.", LogSeverity.Warning);
            return SHA1.Create();
        }
    }
}
