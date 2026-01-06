using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.BlobStore.Model;

namespace ITVComponents.BlobStore.Internal
{
    internal class BlockBroker:IDisposable
    {
        private readonly Stream source;

        private readonly BinaryReader binRead;

        private readonly BinaryWriter binWrite;

        

        public BlockBroker(Stream source)
        {
            this.source = source;
            binRead = new BinaryReader(this.source,Encoding.Default,true);
            binWrite = new BinaryWriter(this.source, Encoding.Default, true);
        }

        public Block ReadBlock(long blockStart, bool readPayload)
        {
            lock (source)
            {
                if (blockStart >= source.Length)
                {
                    return null;
                }

                source.Seek(blockStart, SeekOrigin.Begin);
                var blockLen = binRead.ReadInt32();
                var bytesAhead = binRead.ReadInt64();
                var totalLength = binRead.ReadInt64();
                var nextBlock = binRead.ReadInt64(); 
                byte[] payload = null;
                if (readPayload)
                {
                    payload = binRead.ReadBytes(blockLen);
                }

                var retVal = new Block
                {
                    BytesAhead = bytesAhead,
                    Length = blockLen,
                    Position = blockStart,
                    NextBlock = nextBlock,
                    Payload = payload,
                    TotalUsed = totalLength
                };

                return retVal;
            }
        }

        public void ReadBlock(Block block)
        {
            lock (source)
            {
                source.Seek(block.Position, SeekOrigin.Begin);
                var blockLen = binRead.ReadInt32();
                var bytesAhead = binRead.ReadInt64();
                var totalLength = binRead.ReadInt64();
                var nextBlock = binRead.ReadInt64();
                var payload = binRead.ReadBytes(blockLen);
                block.BytesAhead = bytesAhead;
                if (nextBlock != -1)
                {
                    block.NextBlock = nextBlock;
                }

                block.Payload = payload;
                block.TotalUsed = totalLength;
            }
        }

        public void WriteBlock(Block block)
        {
            lock (source)
            {
                bool writePayload = block.Payload != null;
                source.Seek(block.Position, SeekOrigin.Begin);
                if (writePayload)
                {
                    binWrite.Write(block.Payload.Length);
                }
                else
                {
                    binWrite.Seek(sizeof(int), SeekOrigin.Current);
                }

                binWrite.Write(block.BytesAhead);
                binWrite.Write(block.TotalUsed);
                binWrite.Write(block.NextBlock);
                if (writePayload)
                {
                    binWrite.Write(block.Payload);
                }

                binWrite.Flush();
            }
        }

        public Block AllocateBlock(int payloadLength)
        {
            lock (source)
            {
                source.Seek(0, SeekOrigin.End);
                var blockStart = source.Position;
                var block = new Block
                {
                    Length = payloadLength,
                    Position = blockStart,
                    NextBlock = -1,
                    BytesAhead = 0,
                    TotalUsed = 0,
                    Payload = new byte[payloadLength]
                };

                WriteBlock(block);
                return block;
            }
        }

        public void Dispose()
        {
            binRead.Dispose();
            binWrite.Dispose();
        }
    }
}
