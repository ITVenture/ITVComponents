using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.Security
{
    /// <summary>
    /// Format version 2 of <see cref="AesEncryptor"/>: AES-256-GCM (authenticated), key derivation in two stages
    /// (issue Security 01).
    /// </summary>
    /// <remarks>
    /// <para><b>Key derivation.</b></para>
    /// <list type="bullet">
    /// <item>Passphrase mode: PBKDF2-HMAC-SHA256 with <see cref="DefaultIterations"/> rounds and a random salt per process
    /// turns the secret into a master key, which is cached. HKDF-SHA256 with a random salt per value derives the key of
    /// the value.</item>
    /// <item>Key mode (random keys, e.g. tenant keys): HKDF only - stretching a 256 bit random key gains nothing.</item>
    /// </list>
    /// <para>Every value has its own key and its own random salt; all salts and the iteration count are part of the value.</para>
    /// <para><b>Blob layout:</b> <c>00 | 02 | mode | [iterations (Int32 LE) | kdf salt length | kdf salt] | value salt length
    /// | value salt | nonce (12) | tag (16) | ciphertext</c>. The bracketed part exists in passphrase mode only. Everything
    /// before the nonce is authenticated as associated data. A version 1 blob starts with its salt length (32), never
    /// with 00.</para>
    /// <para><b>Stream layout:</b> <c>magic (Int32 LE) | 02 | mode | [...] | value salt | nonce prefix (8) | segment size
    /// (Int32 LE)</c>, then segments, see <see cref="GcmSegmentStream"/>.</para>
    /// </remarks>
    internal static class AesFormatV2
    {
        internal const byte Marker = 0x00;

        internal const byte Version = 2;

        internal const byte ModePassphrase = 1;

        internal const byte ModeKey = 2;

        internal const int DefaultIterations = 600_000;

        /// <summary>Bounds for an iteration count read from a value - a forged value must not make us run forever.</summary>
        private const int MinIterations = 10_000;

        private const int MaxIterations = 10_000_000;

        internal const int StreamMagic = 45000816;

        internal const int SegmentSize = 64 * 1024;

        internal const int NonceSize = 12;

        internal const int NoncePrefixSize = 8;

        internal const int TagSize = 16;

        private const int SaltSize = 32;

        private const int KeySize = 32;

        private const int MaxCachedMasterKeys = 16;

        /// <summary>The salt of the expensive first stage for everything this process writes in passphrase mode.</summary>
        private static readonly byte[] ProcessKdfSalt = RandomNumberGenerator.GetBytes(SaltSize);

        /// <summary>Master keys of the first stage, by (hash of the secret, iterations, salt).</summary>
        private static readonly ConcurrentDictionary<string, byte[]> MasterKeys = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);

        internal const string UniformFailureMessage =
            "The value could not be decrypted (wrong key or modified data).";

        /// <summary>
        /// The one exception for every failed decryption, whatever the reason - so a caller cannot tell a wrong key from
        /// modified data (no oracle). The reason goes to the debug log.
        /// </summary>
        internal static CryptographicException DecryptionFailed(Exception cause)
        {
            if (cause != null)
            {
                LogEnvironment.LogDebugEvent($"Decryption failed: {cause.OutlineException()}", LogSeverity.Warning);
            }

            return new CryptographicException(UniformFailureMessage);
        }

        /// <summary>The parameters that lead from the secret to the key of one value.</summary>
        internal sealed class KeyParameters
        {
            public byte Mode { get; init; }

            public int Iterations { get; init; }

            public byte[] KdfSalt { get; init; }

            public byte[] ValueSalt { get; init; }

            public static KeyParameters CreateNew(byte mode)
            {
                return new KeyParameters
                {
                    Mode = mode,
                    Iterations = mode == ModePassphrase ? DefaultIterations : 0,
                    KdfSalt = mode == ModePassphrase ? ProcessKdfSalt : null,
                    ValueSalt = RandomNumberGenerator.GetBytes(SaltSize)
                };
            }

            public void WriteTo(BinaryWriter writer)
            {
                writer.Write(Mode);
                if (Mode == ModePassphrase)
                {
                    writer.Write(Iterations);
                    writer.Write((byte)KdfSalt.Length);
                    writer.Write(KdfSalt);
                }

                writer.Write((byte)ValueSalt.Length);
                writer.Write(ValueSalt);
            }

            public static KeyParameters ReadFrom(BinaryReader reader)
            {
                byte mode = reader.ReadByte();
                int iterations = 0;
                byte[] kdfSalt = null;
                if (mode == ModePassphrase)
                {
                    iterations = reader.ReadInt32();
                    if (iterations < MinIterations || iterations > MaxIterations)
                    {
                        throw new InvalidDataException($"Iteration count {iterations} is out of range.");
                    }

                    kdfSalt = ReadExactly(reader, reader.ReadByte());
                }
                else if (mode != ModeKey)
                {
                    throw new InvalidDataException($"Unknown key mode {mode}.");
                }

                var valueSalt = ReadExactly(reader, reader.ReadByte());
                return new KeyParameters { Mode = mode, Iterations = iterations, KdfSalt = kdfSalt, ValueSalt = valueSalt };
            }

            /// <summary>The key of the value: HKDF over the master key (passphrase mode) or over the secret itself (key mode).</summary>
            public byte[] DeriveKey(byte[] secret, string purpose)
            {
                byte[] inputKey = Mode == ModePassphrase ? MasterKey(secret, Iterations, KdfSalt) : secret;
                return HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKey, KeySize, ValueSalt, Encoding.ASCII.GetBytes(purpose));
            }
        }

        /// <summary>Encrypts a blob in format version 2.</summary>
        internal static byte[] EncryptBlob(byte[] plain, byte[] secret, byte mode)
        {
            var parameters = KeyParameters.CreateNew(mode);
            byte[] header;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(Marker);
                writer.Write(Version);
                parameters.WriteTo(writer);
                writer.Flush();
                header = ms.ToArray();
            }

            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var cipher = new byte[plain.Length];
            using (var gcm = new AesGcm(parameters.DeriveKey(secret, "ITVComponents.AesEncryptor.v2.blob"), TagSize))
            {
                gcm.Encrypt(nonce, plain, cipher, tag, header);
            }

            var retVal = new byte[header.Length + NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(header, 0, retVal, 0, header.Length);
            Buffer.BlockCopy(nonce, 0, retVal, header.Length, NonceSize);
            Buffer.BlockCopy(tag, 0, retVal, header.Length + NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, retVal, header.Length + NonceSize + TagSize, cipher.Length);
            return retVal;
        }

        /// <summary>Decrypts a blob in format version 2; any failure is the uniform <see cref="CryptographicException"/>.</summary>
        internal static byte[] DecryptBlob(byte[] blob, byte[] secret)
        {
            try
            {
                KeyParameters parameters;
                int headerLength;
                using (var ms = new MemoryStream(blob, false))
                using (var reader = new BinaryReader(ms))
                {
                    if (reader.ReadByte() != Marker || reader.ReadByte() != Version)
                    {
                        throw new InvalidDataException("Not a version 2 value.");
                    }

                    parameters = KeyParameters.ReadFrom(reader);
                    headerLength = (int)ms.Position;
                }

                int cipherLength = blob.Length - headerLength - NonceSize - TagSize;
                if (cipherLength < 0)
                {
                    throw new InvalidDataException("The value is too short.");
                }

                var header = new ReadOnlySpan<byte>(blob, 0, headerLength);
                var nonce = new ReadOnlySpan<byte>(blob, headerLength, NonceSize);
                var tag = new ReadOnlySpan<byte>(blob, headerLength + NonceSize, TagSize);
                var cipher = new ReadOnlySpan<byte>(blob, headerLength + NonceSize + TagSize, cipherLength);
                var plain = new byte[cipherLength];
                using (var gcm = new AesGcm(parameters.DeriveKey(secret, "ITVComponents.AesEncryptor.v2.blob"), TagSize))
                {
                    gcm.Decrypt(nonce, cipher, tag, plain, header);
                }

                return plain;
            }
            catch (Exception ex) when (ex is CryptographicException or InvalidDataException or EndOfStreamException or ArgumentException and not ArgumentNullException)
            {
                throw DecryptionFailed(ex);
            }
        }

        /// <summary>Writes the stream header and returns the encrypting stream.</summary>
        internal static Stream CreateEncryptStream(Stream target, byte[] secret, byte mode, bool leaveOpen)
        {
            var parameters = KeyParameters.CreateNew(mode);
            var noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixSize);
            byte[] header;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(StreamMagic);
                writer.Write(Version);
                parameters.WriteTo(writer);
                writer.Write(noncePrefix);
                writer.Write(SegmentSize);
                writer.Flush();
                header = ms.ToArray();
            }

            target.Write(header, 0, header.Length);
            var key = parameters.DeriveKey(secret, "ITVComponents.AesEncryptor.v2.stream");
            return GcmSegmentStream.ForWriting(target, key, header, noncePrefix, SegmentSize, leaveOpen);
        }

        /// <summary>
        /// Reads the rest of a stream header (the magic number was read already) and returns the decrypting stream.
        /// </summary>
        internal static Stream OpenDecryptStream(Stream source, byte[] magic, byte[] secret, bool leaveOpen)
        {
            try
            {
                using var headerCopy = new MemoryStream();
                headerCopy.Write(magic, 0, magic.Length);
                var recording = new RecordingReadStream(source, headerCopy);
                KeyParameters parameters;
                byte[] noncePrefix;
                int segmentSize;
                using (var reader = new BinaryReader(recording, Encoding.ASCII, true))
                {
                    if (reader.ReadByte() != Version)
                    {
                        throw new InvalidDataException("Unknown stream version.");
                    }

                    parameters = KeyParameters.ReadFrom(reader);
                    noncePrefix = ReadExactly(reader, NoncePrefixSize);
                    segmentSize = reader.ReadInt32();
                    if (segmentSize <= 0 || segmentSize > 16 * 1024 * 1024)
                    {
                        throw new InvalidDataException($"Segment size {segmentSize} is out of range.");
                    }
                }

                var key = parameters.DeriveKey(secret, "ITVComponents.AesEncryptor.v2.stream");
                return GcmSegmentStream.ForReading(source, key, headerCopy.ToArray(), noncePrefix, segmentSize, leaveOpen);
            }
            catch (Exception ex) when (ex is CryptographicException or InvalidDataException or EndOfStreamException or ArgumentException and not ArgumentNullException)
            {
                throw DecryptionFailed(ex);
            }
        }

        /// <summary>
        /// Encrypting stream whose parameters are stored separately by the caller: the target gets only the segments.
        /// "iv" = <c>00 | 02 | nonce prefix (8) | segment size (Int32 LE)</c> (14 bytes, a version 1 IV has 16), "salt" =
        /// the key parameters. Both are authenticated with every segment.
        /// </summary>
        internal static Stream CreateDetachedEncryptStream(Stream target, byte[] secret, byte mode, out byte[] initializationVector, out byte[] salt, bool leaveOpen)
        {
            var parameters = KeyParameters.CreateNew(mode);
            var noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixSize);
            salt = Serialize(parameters.WriteTo);
            initializationVector = Serialize(w =>
            {
                w.Write(Marker);
                w.Write(Version);
                w.Write(noncePrefix);
                w.Write(SegmentSize);
            });

            var key = parameters.DeriveKey(secret, "ITVComponents.AesEncryptor.v2.detached");
            return GcmSegmentStream.ForWriting(target, key, salt.Concat(initializationVector).ToArray(), noncePrefix, SegmentSize, leaveOpen);
        }

        /// <summary>Decrypting stream for data with separately stored parameters, see <see cref="CreateDetachedEncryptStream"/>.</summary>
        internal static Stream OpenDetachedDecryptStream(Stream source, byte[] secret, byte[] initializationVector, byte[] salt, bool leaveOpen)
        {
            try
            {
                KeyParameters parameters;
                using (var reader = new BinaryReader(new MemoryStream(salt, false)))
                {
                    parameters = KeyParameters.ReadFrom(reader);
                }

                byte[] noncePrefix;
                int segmentSize;
                using (var reader = new BinaryReader(new MemoryStream(initializationVector, false)))
                {
                    if (reader.ReadByte() != Marker || reader.ReadByte() != Version)
                    {
                        throw new InvalidDataException("The initialization vector is no version 2 value.");
                    }

                    noncePrefix = ReadExactly(reader, NoncePrefixSize);
                    segmentSize = reader.ReadInt32();
                    if (segmentSize <= 0 || segmentSize > 16 * 1024 * 1024)
                    {
                        throw new InvalidDataException($"Segment size {segmentSize} is out of range.");
                    }
                }

                var key = parameters.DeriveKey(secret, "ITVComponents.AesEncryptor.v2.detached");
                return GcmSegmentStream.ForReading(source, key, salt.Concat(initializationVector).ToArray(), noncePrefix, segmentSize, leaveOpen);
            }
            catch (Exception ex) when (ex is CryptographicException or InvalidDataException or EndOfStreamException or ArgumentException and not ArgumentNullException)
            {
                throw DecryptionFailed(ex);
            }
        }

        private static byte[] Serialize(Action<BinaryWriter> write)
        {
            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, true))
            {
                write(writer);
            }

            return ms.ToArray();
        }

        private static byte[] MasterKey(byte[] secret, int iterations, byte[] kdfSalt)
        {
            string cacheKey = $"{Convert.ToHexString(SHA256.HashData(secret))}:{iterations}:{Convert.ToBase64String(kdfSalt)}";
            if (MasterKeys.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var derived = Rfc2898DeriveBytes.Pbkdf2(secret, kdfSalt, iterations, HashAlgorithmName.SHA256, KeySize);
            if (MasterKeys.Count >= MaxCachedMasterKeys)
            {
                // Simple bound: the cache holds the keys of the few salts a process meets; a full cache starts over.
                MasterKeys.Clear();
            }

            return MasterKeys.GetOrAdd(cacheKey, derived);
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            var retVal = reader.ReadBytes(count);
            if (retVal.Length != count)
            {
                throw new EndOfStreamException("The value ends inside its header.");
            }

            return retVal;
        }

        /// <summary>Passes reads through and keeps a copy of what was read (the stream header, for authentication).</summary>
        private sealed class RecordingReadStream : Stream
        {
            private readonly Stream inner;
            private readonly Stream copy;

            public RecordingReadStream(Stream inner, Stream copy)
            {
                this.inner = inner;
                this.copy = copy;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = inner.Read(buffer, offset, count);
                copy.Write(buffer, offset, read);
                return read;
            }

            public override bool CanRead => true;

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
        }
    }
}
