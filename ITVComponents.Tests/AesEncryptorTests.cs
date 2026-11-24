using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ITVComponents.Logging;
using ITVComponents.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Tests
{
    /// <summary>
    /// AesEncryptor format version 2 (issue Security 01): authenticated encryption, both key modes, streams, separately
    /// stored parameters, reading version 1, one exception for every failure.
    /// </summary>
    [TestClass]
    public class AesEncryptorTests
    {
        private static readonly byte[] Passphrase = Encoding.UTF32.GetBytes("So long and thanks for all the fish");

        private static readonly byte[] Key = AesEncryptor.CreateKey();

        [TestMethod]
        public void Strings_And_Blobs_Round_Trip_With_Passphrase_And_Key()
        {
            var text = "Grüezi – ein Text mit Umlauten";
            Assert.AreEqual(text, AesEncryptor.Decrypt(AesEncryptor.Encrypt(text, Passphrase), Passphrase));
            Assert.AreEqual(text, AesEncryptor.Decrypt(AesEncryptor.EncryptWithKey(text, Key), Key));

            var blob = RandomNumberGenerator.GetBytes(1000);
            CollectionAssert.AreEqual(blob, AesEncryptor.Decrypt(AesEncryptor.Encrypt(blob, Passphrase), Passphrase));
            CollectionAssert.AreEqual(blob, AesEncryptor.Decrypt(AesEncryptor.EncryptWithKey(blob, Key), Key));
            CollectionAssert.AreEqual(Array.Empty<byte>(), AesEncryptor.Decrypt(AesEncryptor.Encrypt(Array.Empty<byte>(), Passphrase), Passphrase));
        }

        [TestMethod]
        public void Version_2_Is_Written_And_Carries_Its_Iteration_Count()
        {
            var encrypted = AesEncryptor.Encrypt(new byte[] { 1, 2, 3 }, Passphrase);

            Assert.AreEqual(0, encrypted[0], "marker");
            Assert.AreEqual(2, encrypted[1], "version");
            Assert.AreEqual(1, encrypted[2], "passphrase mode");
            Assert.AreEqual(600_000, BitConverter.ToInt32(encrypted, 3));
            Assert.IsFalse(AesEncryptor.IsLegacyFormat(encrypted));
            Assert.AreEqual(2, AesEncryptor.EncryptWithKey(new byte[] { 1 }, Key)[2], "key mode");
        }

        [TestMethod]
        public void Same_Input_Gives_Different_Values()
        {
            Assert.AreNotEqual(AesEncryptor.Encrypt("x", Passphrase), AesEncryptor.Encrypt("x", Passphrase));
        }

        [TestMethod]
        public void Streams_Round_Trip_Across_Segment_Boundaries()
        {
            foreach (int length in new[] { 0, 1, 64 * 1024 - 1, 64 * 1024, 2 * 64 * 1024, 200_000 })
            {
                var data = RandomNumberGenerator.GetBytes(length);
                CollectionAssert.AreEqual(data, StreamRoundTrip(data, s => AesEncryptor.GetEncryptStream(s, Passphrase, true), s => AesEncryptor.GetDecryptStream(s, Passphrase, true)), $"passphrase, {length} bytes");
                CollectionAssert.AreEqual(data, StreamRoundTrip(data, s => AesEncryptor.GetEncryptStreamWithKey(s, Key, true), s => AesEncryptor.GetDecryptStream(s, Key, true)), $"key, {length} bytes");
            }
        }

        [TestMethod]
        public void Separately_Stored_Parameters_Round_Trip_And_Mix_Blob_And_Stream()
        {
            var data = RandomNumberGenerator.GetBytes(150_000);

            var encrypted = AesEncryptor.Encrypt(data, Passphrase, out var iv, out var salt);
            Assert.AreEqual(14, iv.Length, "a version 2 iv is recognized by its length");
            CollectionAssert.AreEqual(data, AesEncryptor.Decrypt(encrypted, Passphrase, iv, salt));

            using var target = new MemoryStream();
            byte[] streamIv, streamSalt;
            using (var cs = AesEncryptor.GetEncryptStreamWithKey(target, Key, out streamIv, out streamSalt, true))
            {
                cs.Write(data);
            }

            CollectionAssert.AreEqual(data, AesEncryptor.Decrypt(target.ToArray(), Key, streamIv, streamSalt), "stream written, blob read");
        }

        [TestMethod]
        public void Version_1_Values_Are_Still_Read()
        {
            var data = Encoding.UTF8.GetBytes("legacy");

            var blob = LegacyBlob(data, Passphrase);
            Assert.IsTrue(AesEncryptor.IsLegacyFormat(blob));
            CollectionAssert.AreEqual(data, AesEncryptor.Decrypt(blob, Passphrase));
            Assert.AreEqual("legacy", AesEncryptor.Decrypt(Convert.ToBase64String(blob), Passphrase));
            Assert.IsTrue(AesEncryptor.IsLegacyFormat(Convert.ToBase64String(blob)));

            var cipher = LegacyCipher(data, Passphrase, out var iv, out var salt);
            CollectionAssert.AreEqual(data, AesEncryptor.Decrypt(cipher, Passphrase, iv, salt), "separately stored parameters");

            using var legacyStream = new MemoryStream(LegacyStream(data, Passphrase));
            using var reader = AesEncryptor.GetDecryptStream(legacyStream, Passphrase, true);
            using var plain = new MemoryStream();
            reader.CopyTo(plain);
            CollectionAssert.AreEqual(data, plain.ToArray(), "stream with header");
        }

        [TestMethod]
        public void Reading_A_Version_1_String_Warns_Once_Per_Calling_Place()
        {
            var legacy = Convert.ToBase64String(LegacyBlob(Encoding.UTF8.GetBytes("old"), Passphrase));
            var warnings = new List<string>();
            using (LogEnvironment.SetTemporaryLogHook((debug, text, severity, context) =>
                   {
                       if (!debug && text.Contains("legacy format"))
                       {
                           warnings.Add(text);
                       }
                   }))
            {
                for (int i = 0; i < 3; i++)
                {
                    AesEncryptor.Decrypt(legacy, Passphrase);
                }
            }

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], nameof(Reading_A_Version_1_String_Warns_Once_Per_Calling_Place), "the stack trace names the caller");
        }

        [TestMethod]
        public void Every_Failure_Is_The_Same_Exception()
        {
            var encrypted = AesEncryptor.Encrypt(Encoding.UTF8.GetBytes("secret value"), Passphrase);
            var message = Expect(() => AesEncryptor.Decrypt(encrypted, Encoding.UTF32.GetBytes("wrong")));

            foreach (int position in new[] { 2, 4, 10, encrypted.Length - 20, encrypted.Length - 1 })
            {
                var modified = (byte[])encrypted.Clone();
                modified[position] ^= 0x01;
                Assert.AreEqual(message, Expect(() => AesEncryptor.Decrypt(modified, Passphrase)), $"byte {position} modified");
            }

            Assert.AreEqual(message, Expect(() => AesEncryptor.Decrypt(encrypted.Take(encrypted.Length - 1).ToArray(), Passphrase)), "cut off");
            Assert.AreEqual(message, Expect(() => AesEncryptor.Decrypt("not base64 !", Passphrase)), "no Base64");
        }

        [TestMethod]
        public void A_Forged_Iteration_Count_Is_Rejected_Without_Running_It()
        {
            var encrypted = AesEncryptor.Encrypt(new byte[] { 1 }, Passphrase);
            BitConverter.GetBytes(int.MaxValue).CopyTo(encrypted, 3);

            Assert.ThrowsExactly<CryptographicException>(() => AesEncryptor.Decrypt(encrypted, Passphrase));
        }

        [TestMethod]
        public void Modified_Cut_Or_Reordered_Streams_Fail()
        {
            var data = RandomNumberGenerator.GetBytes(3 * 64 * 1024 + 100);
            using var target = new MemoryStream();
            using (var cs = AesEncryptor.GetEncryptStreamWithKey(target, Key, true))
            {
                cs.Write(data);
            }

            var encrypted = target.ToArray();
            int segment = 64 * 1024 + 16;
            int headerLength = encrypted.Length - (3 * segment + 100 + 16);

            var cutAtSegment = encrypted.Take(headerLength + 2 * segment).ToArray();
            Assert.ThrowsExactly<CryptographicException>(() => ReadAll(cutAtSegment), "cut off exactly after a segment");

            var swapped = (byte[])encrypted.Clone();
            Array.Copy(encrypted, headerLength, swapped, headerLength + segment, segment);
            Array.Copy(encrypted, headerLength + segment, swapped, headerLength, segment);
            Assert.ThrowsExactly<CryptographicException>(() => ReadAll(swapped), "two segments swapped");

            var flipped = (byte[])encrypted.Clone();
            flipped[headerLength + segment + 5] ^= 0x01;
            Assert.ThrowsExactly<CryptographicException>(() => ReadAll(flipped), "a byte modified");
        }

        [TestMethod]
        public void Key_Mode_Refuses_Short_Keys()
        {
            Assert.ThrowsExactly<ArgumentException>(() => AesEncryptor.EncryptWithKey("x", new byte[8]));
        }

        private static byte[] ReadAll(byte[] encrypted)
        {
            using var reader = AesEncryptor.GetDecryptStream(new MemoryStream(encrypted), Key);
            using var plain = new MemoryStream();
            reader.CopyTo(plain);
            return plain.ToArray();
        }

        private static string Expect(Action action)
        {
            var ex = Assert.ThrowsExactly<CryptographicException>(action);
            return ex.Message;
        }

        private static byte[] StreamRoundTrip(byte[] data, Func<Stream, Stream> encrypt, Func<Stream, Stream> decrypt)
        {
            using var target = new MemoryStream();
            using (var cs = encrypt(target))
            {
                cs.Write(data);
            }

            target.Position = 0;
            using var reader = decrypt(target);
            using var plain = new MemoryStream();
            reader.CopyTo(plain);
            return plain.ToArray();
        }

        // Reference implementation of format version 1 (AES-256-CBC, PBKDF2-HMAC-SHA1, 10'000 rounds), independent of the
        // code under test.

        private static byte[] LegacyCipher(byte[] data, byte[] entropy, out byte[] iv, out byte[] salt)
        {
            salt = RandomNumberGenerator.GetBytes(32);
            using var aes = Aes.Create();
            iv = aes.IV;
            var key = Rfc2898DeriveBytes.Pbkdf2(entropy, salt, 10000, HashAlgorithmName.SHA1, 32);
            using var encryptor = aes.CreateEncryptor(key, iv);
            return encryptor.TransformFinalBlock(data, 0, data.Length);
        }

        private static byte[] LegacyBlob(byte[] data, byte[] entropy)
        {
            var cipher = LegacyCipher(data, entropy, out var iv, out var salt);
            return new[] { (byte)salt.Length, (byte)iv.Length }.Concat(salt).Concat(iv).Concat(cipher).ToArray();
        }

        private static byte[] LegacyStream(byte[] data, byte[] entropy)
        {
            var cipher = LegacyCipher(data, entropy, out var iv, out var salt);
            return BitConverter.GetBytes(45000815).Concat(BitConverter.GetBytes(iv.Length)).Concat(BitConverter.GetBytes(salt.Length))
                .Concat(iv).Concat(salt).Concat(cipher).ToArray();
        }
    }
}
