using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Security
{
    /// <summary>
    /// A Stream-Implementation that reads/writes the basic parameters for Encryption to the underlaying stream.
    /// </summary>
    public class CryptDecoratorStream:Stream
    {
        private readonly Stream innerStream;
        private readonly byte[] initializationVector;
        private readonly byte[] salt;
        private readonly bool leaveOpen;
        private int cryptoBlockLength;
        private const int MagicNumber = 45000815;

        /// <summary>
        /// Initializes a new instance of the CryptDecoratorStream class. Use this overload to write a new file using a crypto-stream.
        /// </summary>
        /// <param name="innerStream">the basic-filestream that points to a resource</param>
        /// <param name="initializationVector">the initializationvector used for encryption</param>
        /// <param name="salt">the salt used for encryption</param>
        /// <param name="leaveOpen">indicates whether to leave the inner stream open, when this stream is being disposed</param>
        public CryptDecoratorStream(Stream innerStream, byte[] initializationVector, byte[] salt, bool leaveOpen)
        {
            this.innerStream = innerStream;
            this.initializationVector = initializationVector;
            this.salt = salt;
            this.leaveOpen = leaveOpen;
            InitStream(innerStream);
        }

        /// <summary>
        /// Initializes a new instance of the CryptDecoratorStream class. Use this overload to read from an existing file using a crypto-stream.
        /// </summary>
        /// <param name="innerStream">the baseic-filestream that points to an encrypted resource</param>
        /// <param name="leaveOpen">indicates whether to leave the inner stream open, when this stream is being disposed</param>
        public CryptDecoratorStream(Stream innerStream, bool leaveOpen)
        {
            this.innerStream = innerStream;
            this.leaveOpen = leaveOpen;
            InitStream(innerStream, out initializationVector, out salt, out cryptoBlockLength);
        }

        /// <summary>When overridden in a derived class, gets a value indicating whether the current stream supports reading.</summary>
        /// <returns>
        /// <see langword="true" /> if the stream supports reading; otherwise, <see langword="false" />.</returns>
        public override bool CanRead => innerStream.CanRead;

        /// <summary>When overridden in a derived class, gets a value indicating whether the current stream supports seeking.</summary>
        /// <returns>
        /// <see langword="true" /> if the stream supports seeking; otherwise, <see langword="false" />.</returns>
        public override bool CanSeek => innerStream.CanSeek;

        /// <summary>When overridden in a derived class, gets a value indicating whether the current stream supports writing.</summary>
        /// <returns>
        /// <see langword="true" /> if the stream supports writing; otherwise, <see langword="false" />.</returns>
        public override bool CanWrite => innerStream.CanWrite;

        /// <summary>When overridden in a derived class, gets the length in bytes of the stream.</summary>
        /// <exception cref="T:System.NotSupportedException">A class derived from <see langword="Stream" /> does not support seeking.</exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
        /// <returns>A long value representing the length of the stream in bytes.</returns>
        public override long Length => innerStream.Length - cryptoBlockLength;

        /// <summary>
        /// Gets the Initialization vector that is used for encryption
        /// </summary>
        public byte[] InitializationVector => initializationVector;

        /// <summary>
        /// Gets the Salt that is used for encryption
        /// </summary>
        public byte[] Salt => salt;

        /// <summary>When overridden in a derived class, gets or sets the position within the current stream.</summary>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support seeking.</exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
        /// <returns>The current position within the stream.</returns>
        public override long Position
        {
            get
            {
                return innerStream.Position - cryptoBlockLength;
            }
            set
            {
                innerStream.Position = value + cryptoBlockLength;
            }
        }

        /// <summary>When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.</summary>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        public override void Flush()
        {
            innerStream.Flush();
        }

        /// <summary>When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.</summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset" /> and <paramref name="count" /> is larger than the buffer length.</exception>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="buffer" /> is <see langword="null" />.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="offset" /> or <paramref name="count" /> is negative.</exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support reading.</exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return innerStream.Read(buffer, offset, count);
        }

        /// <summary>When overridden in a derived class, sets the position within the current stream.</summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin" /> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point used to obtain the new position.</param>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output.</exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                offset += cryptoBlockLength;
            }

            return innerStream.Seek(offset, origin) - cryptoBlockLength;
        }

        /// <summary>When overridden in a derived class, sets the length of the current stream.</summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output.</exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
        public override void SetLength(long value)
        {
            innerStream.SetLength(value + cryptoBlockLength);
        }

        /// <summary>When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.</summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count" /> bytes from <paramref name="buffer" /> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset" /> and <paramref name="count" /> is greater than the buffer length.</exception>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="buffer" /> is <see langword="null" />.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="offset" /> or <paramref name="count" /> is negative.</exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurred, such as the specified file cannot be found.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support writing.</exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// <see cref="M:System.IO.Stream.Write(System.Byte[],System.Int32,System.Int32)" /> was called after the stream was closed.</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            innerStream.Write(buffer, offset, count);
        }

        /// <summary>Releases the unmanaged resources used by the <see cref="T:System.IO.Stream" /> and optionally releases the managed resources.</summary>
        /// <param name="disposing">
        /// <see langword="true" /> to release both managed and unmanaged resources; <see langword="false" /> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
            {
                innerStream?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitStream(Stream stream)
        {
            var magic = BitConverter.GetBytes(MagicNumber).Concat(BitConverter.GetBytes(initializationVector.Length))
                .Concat(BitConverter.GetBytes(salt.Length)).Concat(initializationVector).Concat(salt).ToArray();
            if (stream.Position != 0)
            {
                throw new InvalidOperationException(
                    "An empty stream is required to initialize the CryptoStream object successfully");
            }

            cryptoBlockLength = magic.Length;
            innerStream.Write(magic);
        }

        private void InitStream(Stream stream, out byte[] initializationVector, out byte[] salt, out int cryptoBlockLength)
        {
            var tmp = new byte[12];
            if (stream.Position != 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            stream.Read(tmp, 0, tmp.Length);
            var mg = BitConverter.ToInt32(tmp, 0);
            var ivl = BitConverter.ToInt32(tmp, 4);
            var sl = BitConverter.ToInt32(tmp, 8);
            if (mg != MagicNumber)
            {
                throw new InvalidOperationException("The given file was not written using a CryptoStream!");
            }

            initializationVector = new byte[ivl];
            salt = new byte[sl];
            cryptoBlockLength = 12 + ivl + sl;
            stream.Read(initializationVector, 0, ivl);
            stream.Read(salt, 0, sl);
        }
    }
}
