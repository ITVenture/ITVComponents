using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ITVComponents.Logging;
using ITVComponents.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security
{
    /// <summary>
    /// The one place that creates, protects and reads tenant keys (<c>Tenant.TenantPassword</c>), issue Security 01 part C.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tenant key encrypts the data of its tenant. Stored in plain text, it lies in the same database as that data - a
    /// leaked database would give away both. So the key is stored encrypted with the key of the application
    /// (<see cref="PasswordSecurity"/>, configured outside the database): <c>tk1:</c> followed by the encrypted key.
    /// </para>
    /// <para>
    /// Values without the prefix are keys from before (plain Base64). They are still read - with a warning once per value -
    /// and protected by <c>TenantKeyProtectionService</c> on the next start.
    /// </para>
    /// <para>
    /// Every reader goes through <see cref="Unprotect"/> or <see cref="KeyBytes"/>, never through the column directly.
    /// </para>
    /// </remarks>
    public static class TenantKeys
    {
        /// <summary>Marks a stored tenant key as protected with the application key.</summary>
        public const string ProtectedPrefix = "tk1:";

        private static readonly ConcurrentDictionary<string, byte> WarnedPlainKeys = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        /// <summary>Creates a new random tenant key (256 bit) in its stored, protected form.</summary>
        public static string CreateProtected()
        {
            return Protect(Convert.ToBase64String(AesEncryptor.CreateKey()));
        }

        /// <summary>
        /// The stored form of a tenant key: protected with the application key. A value that is already protected is
        /// returned unchanged; null or empty stays as it is.
        /// </summary>
        public static string Protect(string plainKey)
        {
            if (string.IsNullOrEmpty(plainKey) || IsProtected(plainKey))
            {
                return plainKey;
            }

            return ProtectedPrefix + plainKey.Encrypt();
        }

        /// <summary>Whether a stored tenant key is protected with the application key.</summary>
        public static bool IsProtected(string storedKey)
        {
            return storedKey != null && storedKey.StartsWith(ProtectedPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// The tenant key as text (Base64) from its stored form; null or empty stays as it is. A key stored before the
        /// protection is returned as it is, with a warning once per key.
        /// </summary>
        public static string Unprotect(string storedKey)
        {
            if (string.IsNullOrEmpty(storedKey))
            {
                return storedKey;
            }

            if (IsProtected(storedKey))
            {
                return storedKey.Substring(ProtectedPrefix.Length).Decrypt();
            }

            if (WarnedPlainKeys.TryAdd(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(storedKey))), 0))
            {
                LogEnvironment.LogEvent(
                    "A tenant key is stored unprotected. It is protected with the application key on the next start " +
                    "(TenantKeyProtectionService); until then it lies in plain text in the database.",
                    LogSeverity.Warning);
            }

            return storedKey;
        }

        /// <summary>The tenant key as bytes, for <see cref="AesEncryptor.EncryptWithKey(byte[],byte[])"/>; null without key.</summary>
        public static byte[] KeyBytes(string storedKey)
        {
            var plain = Unprotect(storedKey);
            return string.IsNullOrEmpty(plain) ? null : Convert.FromBase64String(plain);
        }
    }
}
