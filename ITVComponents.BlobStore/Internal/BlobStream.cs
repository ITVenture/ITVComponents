using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.BlobStore.Model;
using ITVComponents.Helpers;
using File = ITVComponents.BlobStore.Model.File;

namespace ITVComponents.BlobStore.Internal
{
    internal class BlobStream:Stream
    {
        private readonly FileListModel file;
        private readonly long start;
        private readonly BlockBroker broker;
        private readonly int defaultBlockSize;
        private readonly Action onFlush;
        private readonly Action<FileListModel> onClose;
        private long position;
        private long bufferLength;
        private List<Block> blocks = new List<Block>();
        private List<Block> dirtyBlocks = new List<Block>();

        public BlobStream(FileListModel file, BlockBroker broker, int defaultBlockSize, Action onFlush, Action<FileListModel> onClose)
        {
            this.file = file;
            this.broker = broker;
            this.defaultBlockSize = defaultBlockSize;
            this.onFlush = onFlush;
            this.onClose = onClose;
            LoadBlocks();
        }

        internal BlobStream(long start, BlockBroker broker, int defaultBlockSize, Action onFlush)
        {
            this.start = start;
            this.broker = broker;
            this.defaultBlockSize = defaultBlockSize;
            this.onFlush = onFlush;
            LoadBlocks();
            long ln = 0;
            if (blocks.Count != 0)
            {

                ln = blocks[0].TotalUsed;

            }

            file = new FileListModel() { FirstBlock = start, Size = ln};
        }

        public override void Flush()
        {
            foreach (var block in dirtyBlocks)
            {
                broker.WriteBlock(block);
            }

            dirtyBlocks.Clear();
            onFlush?.Invoke();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int retVal = 0;
            var p = Position;
            while (p < file.Size && retVal < count)
            {
                var targetBlock = blocks.First(n => n.BytesAhead <= p && n.BytesAhead+ n.Length > p);
                if (targetBlock.Payload == null)
                {
                    broker.ReadBlock(targetBlock);
                }

                var blockOffset = (int)(p - targetBlock.BytesAhead);
                var readableLength = (int)Math.Min(file.Size - targetBlock.BytesAhead, targetBlock.Length);
                var toRead = Math.Min(count - retVal, readableLength - blockOffset);
                Array.Copy(targetBlock.Payload, blockOffset, buffer, offset + retVal, toRead);
                retVal += toRead;
                p += toRead;
            }

            Position += retVal;
            return retVal;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var initial = position;
            if (origin == SeekOrigin.Begin)
            {
                initial = 0;
            }
            else if (origin == SeekOrigin.End)
            {
                initial = file.Size;
            }

            var newPos = initial + offset;
            return Position = newPos;
        }

        public override void SetLength(long value)
        {
            while (bufferLength < value)
            {
                var block = broker.AllocateBlock(defaultBlockSize);
                var lastBlock = blocks.LastOrDefault();
                if (lastBlock != null)
                {
                    lastBlock.NextBlock = block.Position;
                    block.BytesAhead = lastBlock.BytesAhead + lastBlock.Length;
                    dirtyBlocks.AddIfMissing(lastBlock);
                }
                else
                {
                    file.FirstBlock = block.Position;
                }

                blocks.Add(block);
                dirtyBlocks.AddIfMissing(block);
                bufferLength += block.Length;
            }

            file.Size = value;
            file.MarkDirty();
            var firstBlock = blocks.FirstOrDefault();
            if (firstBlock != null)
            {
                firstBlock.TotalUsed = value;
                dirtyBlocks.AddIfMissing(firstBlock);
            }
        }

        public override void Close()
        {
            Flush();
            onClose?.Invoke(file);
            base.Close();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position + count >= file.Size)
            {
                SetLength(Position + count);
            }

            var p = 0;
            while (p < count)
            {
                var targetPos = Position + p;
                var targetBlock = blocks.First(n => n.BytesAhead <= targetPos && n.BytesAhead + n.Length > targetPos);
                if (targetBlock.Payload == null)
                {
                    broker.ReadBlock(targetBlock);
                }

                var blockOffset = (int)(targetPos - targetBlock.BytesAhead);
                var toWrite = Math.Min(count - p, targetBlock.Length - blockOffset);
                Array.Copy(buffer, offset + p, targetBlock.Payload, blockOffset, toWrite);
                dirtyBlocks.AddIfMissing(targetBlock);
                p += toWrite;
            }

            Position += count;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => file.Size;
        public override long Position
        {
            get => position;
            set
            {
                if (position <= file.Size)
                {
                    position = value;
                }
                else
                {
                    throw new EndOfStreamException("Trying to read behind end of Stream");
                }
            }
        }

        private void LoadBlocks()
        {
            Block block = null;
            var start = file?.FirstBlock??this.start;
            while (start != -1)
            {
                block = broker.ReadBlock(start, false);
                if (block != null)
                {
                    blocks.Add(block);
                    start = block.NextBlock;
                }
                else
                {
                    start = -1;
                }
            }
        }
    }
}
