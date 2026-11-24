using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;

namespace ITVComponents.Security
{
    /// <summary>
    /// Segmented AES-GCM for streams (format version 2 of <see cref="AesEncryptor"/>): the data is cut into segments of a
    /// fixed size, each encrypted with its own tag. The nonce is a random prefix plus the segment number; the last segment
    /// is marked in the associated data. So reordering, dropping, duplicating or cutting off segments fails, and no
    /// plaintext is handed out before its segment was verified.
    /// </summary>
    /// <remarks>
    /// Layout after the header: <c>segment = ciphertext | tag (16)</c>. Every segment except the last holds exactly the
    /// segment size; the last holds the rest (possibly nothing). A writer must be disposed - that writes the last segment.
    /// </remarks>
    internal sealed class GcmSegmentStream : Stream
    {
        private const int TagSize = AesFormatV2.TagSize;

        private readonly Stream inner;
        private readonly bool leaveOpen;
        private readonly bool writing;
        private readonly AesGcm gcm;
        private readonly byte[] header;
        private readonly byte[] noncePrefix;
        private readonly int segmentSize;

        private readonly byte[] plainBuffer;
        private int plainCount;
        private uint segmentIndex;

        private byte[] readPlain = Array.Empty<byte>();
        private int readPosition;
        private int readLength;
        private bool finished;
        private int lookahead = -1;
        private bool disposed;

        private GcmSegmentStream(Stream inner, byte[] key, byte[] header, byte[] noncePrefix, int segmentSize, bool leaveOpen, bool writing)
        {
            this.inner = inner;
            this.leaveOpen = leaveOpen;
            this.writing = writing;
            this.header = header;
            this.noncePrefix = noncePrefix;
            this.segmentSize = segmentSize;
            gcm = new AesGcm(key, TagSize);
            plainBuffer = writing ? new byte[segmentSize] : null;
        }

        public static GcmSegmentStream ForWriting(Stream inner, byte[] key, byte[] header, byte[] noncePrefix, int segmentSize, bool leaveOpen)
            => new GcmSegmentStream(inner, key, header, noncePrefix, segmentSize, leaveOpen, true);

        public static GcmSegmentStream ForReading(Stream inner, byte[] key, byte[] header, byte[] noncePrefix, int segmentSize, bool leaveOpen)
            => new GcmSegmentStream(inner, key, header, noncePrefix, segmentSize, leaveOpen, false);

        public override bool CanRead => !writing && !disposed;

        public override bool CanSeek => false;

        public override bool CanWrite => writing && !disposed;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new NotSupportedException("The stream is not writable.");
            }

            while (count > 0)
            {
                if (plainCount == segmentSize)
                {
                    // A full segment is only written once more data follows - the last one is written on dispose.
                    WriteSegment(false);
                }

                int take = Math.Min(segmentSize - plainCount, count);
                Buffer.BlockCopy(buffer, offset, plainBuffer, plainCount, take);
                plainCount += take;
                offset += take;
                count -= take;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
            {
                throw new NotSupportedException("The stream is not readable.");
            }

            while (readPosition == readLength)
            {
                if (finished)
                {
                    return 0;
                }

                ReadSegment();
            }

            int take = Math.Min(readLength - readPosition, count);
            Buffer.BlockCopy(readPlain, readPosition, buffer, offset, take);
            readPosition += take;
            return take;
        }

        public override void Flush()
        {
            if (writing)
            {
                inner.Flush();
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                try
                {
                    if (writing)
                    {
                        WriteSegment(true);
                        inner.Flush();
                    }
                }
                finally
                {
                    disposed = true;
                    gcm.Dispose();
                    if (!leaveOpen)
                    {
                        inner.Dispose();
                    }
                }
            }

            base.Dispose(disposing);
        }

        private void WriteSegment(bool last)
        {
            var cipher = new byte[plainCount];
            var tag = new byte[TagSize];
            gcm.Encrypt(Nonce(), new ReadOnlySpan<byte>(plainBuffer, 0, plainCount), cipher, tag, AssociatedData(last));
            inner.Write(cipher, 0, cipher.Length);
            inner.Write(tag, 0, tag.Length);
            plainCount = 0;
            NextSegment();
        }

        private void ReadSegment()
        {
            var chunk = new byte[segmentSize + TagSize];
            int read = ReadFull(chunk);
            bool last;
            if (read < chunk.Length)
            {
                last = true;
            }
            else
            {
                // A full chunk is the last one only if nothing follows.
                lookahead = inner.ReadByte();
                last = lookahead < 0;
            }

            try
            {
                if (read < TagSize)
                {
                    throw new InvalidDataException("The stream ends inside a segment.");
                }

                int cipherLength = read - TagSize;
                var plain = new byte[cipherLength];
                gcm.Decrypt(Nonce(), new ReadOnlySpan<byte>(chunk, 0, cipherLength), new ReadOnlySpan<byte>(chunk, cipherLength, TagSize), plain, AssociatedData(last));
                readPlain = plain;
                readPosition = 0;
                readLength = cipherLength;
                finished = last;
                NextSegment();
            }
            catch (Exception ex) when (ex is CryptographicException or InvalidDataException)
            {
                throw AesFormatV2.DecryptionFailed(ex);
            }
        }

        private int ReadFull(byte[] chunk)
        {
            int total = 0;
            if (lookahead >= 0)
            {
                chunk[0] = (byte)lookahead;
                lookahead = -1;
                total = 1;
            }

            while (total < chunk.Length)
            {
                int read = inner.Read(chunk, total, chunk.Length - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        private byte[] Nonce()
        {
            var nonce = new byte[AesFormatV2.NonceSize];
            Buffer.BlockCopy(noncePrefix, 0, nonce, 0, noncePrefix.Length);
            BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(noncePrefix.Length), segmentIndex);
            return nonce;
        }

        private byte[] AssociatedData(bool last)
        {
            var retVal = new byte[header.Length + 1];
            Buffer.BlockCopy(header, 0, retVal, 0, header.Length);
            retVal[header.Length] = last ? (byte)1 : (byte)0;
            return retVal;
        }

        private void NextSegment()
        {
            if (segmentIndex == uint.MaxValue)
            {
                throw new InvalidOperationException("The stream has too many segments.");
            }

            segmentIndex++;
        }
    }
}
