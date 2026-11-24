using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.Security
{
    /// <summary>
    /// AES encryption of strings, blobs and streams with a passphrase or a random key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes format version 2: AES-256-GCM (authenticated), PBKDF2-HMAC-SHA256 with 600'000 rounds for passphrases (the
    /// master key is cached per process), HKDF-SHA256 per value (see <see cref="AesFormatV2"/>). The <c>…WithKey</c>
    /// methods are meant for random keys (<see cref="CreateKey"/>, e.g. tenant keys) and skip the stretching.
    /// </para>
    /// <para>
    /// Reads format version 1 as well (AES-256-CBC, PBKDF2-HMAC-SHA1 with 10'000 rounds, no authentication). Reading a
    /// string in version 1 logs a warning, once per calling place, so the value can be encrypted again; see
    /// <see cref="IsLegacyFormat(string)"/>. Every failed decryption - wrong key or modified data, either version - is
    /// the same <see cref="CryptographicException"/>.
    /// </para>
    /// </remarks>
    public static class AesEncryptor
    {
        /// <summary>Version 1 parameters - fixed, the existing data was written with them.</summary>
        private const int LegacyIterations = 10000;

        /// <summary>The length of a version 1 initialization vector. A version 2 "iv" is 14 bytes.</summary>
        private const int LegacyIvLength = 16;

        private const int LegacyStreamMagic = 45000815;

        /// <summary>
        /// Decrypts an AES encrypted string using the specified password
        /// </summary>
        public static string Decrypt(string input, string password)
        {
            return Decrypt(input, ToEntropy(password));
        }

        /// <summary>
        /// Decrypts an AES encrypted blob using the specified password
        /// </summary>
        public static byte[] Decrypt(byte[] input, string password)
        {
            return Decrypt(input, ToEntropy(password));
        }

        /// <summary>
        /// Decrypts an AES encrypted string (Base64 of a blob). Reads both versions, whether written with a passphrase
        /// or with a key.
        /// </summary>
        public static string Decrypt(string input, byte[] entropy)
        {
            byte[] raw;
            try
            {
                raw = Convert.FromBase64String(input);
            }
            catch (FormatException ex)
            {
                throw AesFormatV2.DecryptionFailed(ex);
            }

            if (IsLegacyFormat(raw))
            {
                LegacyFormatWarning.Report();
            }

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
        /// Decrypts an AES encrypted blob. Reads both versions, whether written with a passphrase or with a key.
        /// </summary>
        public static byte[] Decrypt(byte[] input, byte[] entropy)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.Length != 0 && input[0] == AesFormatV2.Marker)
            {
                return AesFormatV2.DecryptBlob(input, entropy);
            }

            try
            {
                byte saltLength = input[0];
                byte initLength = input[1];
                byte[] salt = new byte[saltLength];
                byte[] iv = new byte[initLength];
                byte[] encrypted = new byte[input.Length - 2 - saltLength - initLength];
                int offset = 2;
                Array.Copy(input, offset, salt, 0, salt.Length);
                offset += salt.Length;
                Array.Copy(input, offset, iv, 0, iv.Length);
                offset += iv.Length;
                Array.Copy(input, offset, encrypted, 0, encrypted.Length);
                return Decrypt(encrypted, entropy, iv, salt);
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or OverflowException or ArgumentException and not ArgumentNullException)
            {
                throw AesFormatV2.DecryptionFailed(ex);
            }
        }

        /// <summary>
        /// Decrypts data whose initialization vector and salt are stored separately (see
        /// <see cref="Encrypt(byte[],byte[],out byte[],out byte[])"/>). Reads both versions.
        /// </summary>
        public static byte[] Decrypt(byte[] encrypted, byte[] entropy, byte[] initializationVector, byte[] salt)
        {
            using var cs = GetDecryptStream(new MemoryStream(encrypted), entropy, initializationVector, salt);
            using var os = new MemoryStream();
            cs.CopyTo(os);
            return os.ToArray();
        }

        /// <summary>
        /// Opens a decrypting stream for data whose initialization vector and salt are stored separately. Reads both
        /// versions; the version follows from the initialization vector (version 1: 16 bytes).
        /// </summary>
        public static Stream GetDecryptStream(Stream targetStream, byte[] entropy, byte[] initializationVector, byte[] salt, bool leaveOpen = false)
        {
            if (initializationVector == null || salt == null)
            {
                throw new ArgumentNullException(initializationVector == null ? nameof(initializationVector) : nameof(salt));
            }

            if (initializationVector.Length != LegacyIvLength)
            {
                return AesFormatV2.OpenDetachedDecryptStream(targetStream, entropy, initializationVector, salt, leaveOpen);
            }

            return LegacyDecryptStream(targetStream, entropy, initializationVector, salt, leaveOpen);
        }

        /// <summary>
        /// Opens a decrypting stream for data that carries its parameters in a header (see
        /// <see cref="GetEncryptStream(Stream,byte[],bool)"/>). Reads both versions.
        /// </summary>
        public static Stream GetDecryptStream(Stream targetStream, byte[] entropy, bool leaveOpen = false)
        {
            var magic = new byte[4];
            int read = 0;
            while (read < magic.Length)
            {
                int n = targetStream.Read(magic, read, magic.Length - read);
                if (n == 0)
                {
                    throw AesFormatV2.DecryptionFailed(new EndOfStreamException("The stream ends before its header."));
                }

                read += n;
            }

            int number = BitConverter.ToInt32(magic, 0);
            if (number == AesFormatV2.StreamMagic)
            {
                return AesFormatV2.OpenDecryptStream(targetStream, magic, entropy, leaveOpen);
            }

            if (number != LegacyStreamMagic)
            {
                throw AesFormatV2.DecryptionFailed(new InvalidDataException("The stream was not written by AesEncryptor."));
            }

            if (!targetStream.CanSeek)
            {
                // The version 1 header reader always worked from the start of a seekable stream.
                throw new NotSupportedException("A stream in the legacy format can only be read from a seekable stream.");
            }

            CryptDecoratorStream decorator;
            try
            {
                decorator = new CryptDecoratorStream(targetStream, leaveOpen);
            }
            catch (EndOfStreamException ex)
            {
                throw AesFormatV2.DecryptionFailed(ex);
            }

            return LegacyDecryptStream(decorator, entropy, decorator.InitializationVector, decorator.Salt, false);
        }

        /// <summary>
        /// Encrypts a string using the given password
        /// </summary>
        public static string Encrypt(string input, string password)
        {
            return Encrypt(input, ToEntropy(password));
        }

        /// <summary>
        /// Encrypts a blob using the given password
        /// </summary>
        public static byte[] Encrypt(byte[] input, string password)
        {
            return Encrypt(input, ToEntropy(password));
        }

        /// <summary>
        /// Encrypts a string with a passphrase (as bytes); the result is Base64.
        /// </summary>
        public static string Encrypt(string input, byte[] entropy)
        {
            return Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(input), entropy));
        }

        /// <summary>
        /// Encrypts a blob with a passphrase (as bytes).
        /// </summary>
        public static byte[] Encrypt(byte[] input, byte[] entropy)
        {
            return AesFormatV2.EncryptBlob(input, entropy, AesFormatV2.ModePassphrase);
        }

        /// <summary>
        /// Encrypts a string with a random key (e.g. from <see cref="CreateKey"/>); the result is Base64. No key stretching:
        /// meant for keys, not for passphrases a person chose.
        /// </summary>
        public static string EncryptWithKey(string input, byte[] key)
        {
            return Convert.ToBase64String(EncryptWithKey(Encoding.UTF8.GetBytes(input), key));
        }

        /// <summary>
        /// Encrypts a blob with a random key (e.g. from <see cref="CreateKey"/>). No key stretching: meant for keys, not
        /// for passphrases a person chose. Decrypted with <see cref="Decrypt(byte[],byte[])"/>.
        /// </summary>
        public static byte[] EncryptWithKey(byte[] input, byte[] key)
        {
            return AesFormatV2.EncryptBlob(input, RequireKey(key), AesFormatV2.ModeKey);
        }

        /// <summary>
        /// Encrypts data and hands out initialization vector and salt for separate storage. In version 2 both carry more
        /// than before (the "iv" 14 bytes, the salt the key parameters) - store them as they are, e.g. Base64 in a text
        /// field.
        /// </summary>
        public static byte[] Encrypt(byte[] clearText, byte[] entropy, out byte[] initializationVector, out byte[] salt)
        {
            using var ms = new MemoryStream();
            using (var cs = GetEncryptStream(ms, entropy, out initializationVector, out salt, true))
            {
                cs.Write(clearText);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Opens an encrypting stream and hands out initialization vector and salt for separate storage; the target
        /// stream gets the encrypted data only. Must be disposed to complete the data.
        /// </summary>
        public static Stream GetEncryptStream(Stream targetStream, byte[] entropy, out byte[] initializationVector,
            out byte[] salt, bool leaveOpen = false)
        {
            return AesFormatV2.CreateDetachedEncryptStream(targetStream, entropy, AesFormatV2.ModePassphrase, out initializationVector, out salt, leaveOpen);
        }

        /// <summary>
        /// Opens an encrypting stream with a random key that hands out initialization vector and salt for separate
        /// storage. Must be disposed to complete the data.
        /// </summary>
        public static Stream GetEncryptStreamWithKey(Stream targetStream, byte[] key, out byte[] initializationVector,
            out byte[] salt, bool leaveOpen = false)
        {
            return AesFormatV2.CreateDetachedEncryptStream(targetStream, RequireKey(key), AesFormatV2.ModeKey, out initializationVector, out salt, leaveOpen);
        }

        /// <summary>
        /// Encrypts with a random key and hands out initialization vector and salt for separate storage.
        /// </summary>
        public static byte[] EncryptWithKey(byte[] clearText, byte[] key, out byte[] initializationVector, out byte[] salt)
        {
            using var ms = new MemoryStream();
            using (var cs = GetEncryptStreamWithKey(ms, key, out initializationVector, out salt, true))
            {
                cs.Write(clearText);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Opens an encrypting stream that writes its parameters in a header. Must be disposed to complete the data.
        /// </summary>
        public static Stream GetEncryptStream(Stream targetStream, byte[] entropy, bool leaveOpen = false)
        {
            return AesFormatV2.CreateEncryptStream(targetStream, entropy, AesFormatV2.ModePassphrase, leaveOpen);
        }

        /// <summary>
        /// Opens an encrypting stream with a random key that writes its parameters in a header. Must be disposed to
        /// complete the data.
        /// </summary>
        public static Stream GetEncryptStreamWithKey(Stream targetStream, byte[] key, bool leaveOpen = false)
        {
            return AesFormatV2.CreateEncryptStream(targetStream, RequireKey(key), AesFormatV2.ModeKey, leaveOpen);
        }

        /// <summary>
        /// Whether an encrypted string (Base64) is still in format version 1. False for version 2 and for anything that is
        /// no encrypted value at all. Meant for finding values that should be encrypted again.
        /// </summary>
        public static bool IsLegacyFormat(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted))
            {
                return false;
            }

            var buffer = new byte[(encrypted.Length * 3 / 4) + 3];
            return Convert.TryFromBase64String(encrypted, buffer, out int written) && IsLegacyFormat(buffer.AsSpan(0, written).ToArray());
        }

        /// <summary>Whether an encrypted blob is still in format version 1.</summary>
        public static bool IsLegacyFormat(byte[] encrypted)
        {
            // Version 1 starts with the salt length (always 32), version 2 with 00.
            return encrypted is { Length: > 2 } && encrypted[0] != AesFormatV2.Marker;
        }

        /// <summary>Creates a random 256 bit key.</summary>
        public static byte[] CreateKey()
        {
            return RandomNumberGenerator.GetBytes(32);
        }

        private static Stream LegacyDecryptStream(Stream source, byte[] entropy, byte[] initializationVector, byte[] salt, bool leaveOpen)
        {
            using (var asm = Aes.Create())
            {
                // PBKDF2 with SHA-1, 10000 iterations, 32 bytes: the parameters of the existing data - they must not change.
                var key = Rfc2898DeriveBytes.Pbkdf2(entropy, salt, LegacyIterations, HashAlgorithmName.SHA1, 32);
                ICryptoTransform decryptor = asm.CreateDecryptor(key, initializationVector);
                return new UniformFailureReadStream(new CryptoStream(source, decryptor, CryptoStreamMode.Read, leaveOpen));
            }
        }

        private static byte[] RequireKey(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length < 16)
            {
                throw new ArgumentException(
                    "A key must have at least 128 bits. For a passphrase use Encrypt/GetEncryptStream, which stretch it.", nameof(key));
            }

            return key;
        }

        private static byte[] ToEntropy(string password)
        {
            return Encoding.UTF32.GetBytes(password);
        }

        /// <summary>Turns every decryption error of a version 1 stream into the uniform exception.</summary>
        private sealed class UniformFailureReadStream : Stream
        {
            private readonly Stream inner;

            public UniformFailureReadStream(Stream inner)
            {
                this.inner = inner;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                try
                {
                    return inner.Read(buffer, offset, count);
                }
                catch (CryptographicException ex)
                {
                    throw AesFormatV2.DecryptionFailed(ex);
                }
            }

            public override bool CanRead => inner.CanRead;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try
                    {
                        inner.Dispose();
                    }
                    catch (CryptographicException ex)
                    {
                        // CryptoStream verifies the final block on dispose when it was not read to the end.
                        throw AesFormatV2.DecryptionFailed(ex);
                    }
                }

                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Warns when a string in format version 1 is read - once per calling place and process, with the stack trace, so
        /// an integrator can find the value and encrypt it again.
        /// </summary>
        private static class LegacyFormatWarning
        {
            private static readonly ConcurrentDictionary<string, byte> Reported = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

            public static void Report()
            {
                var trace = new StackTrace(1, false);
                var callers = trace.GetFrames()
                    .Select(f => f.GetMethod())
                    .Where(m => m?.DeclaringType != null && m.DeclaringType.Namespace != typeof(AesEncryptor).Namespace)
                    .Take(6)
                    .Select(m => $"{m.DeclaringType.FullName}.{m.Name}");
                string place = string.Join("|", callers);
                if (Reported.TryAdd(place, 0))
                {
                    LogEnvironment.LogEvent(
                        "An encrypted value was read in the legacy format (AES-CBC without authentication, PBKDF2-SHA1 with " +
                        "10000 rounds). Encrypt it again to store it in the current format. This is reported once per " +
                        $"calling place. Read at:{Environment.NewLine}{trace}",
                        LogSeverity.Warning);
                }
            }
        }
    }
}
