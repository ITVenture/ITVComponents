using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.BlobStore.Model
{
    internal class Block
    {
        public long Position { get; set; }
        public long NextBlock { get; set; }
        public byte[] Payload { get; set; }
        public int Length { get; set; }
        public long BytesAhead { get; set; }
        public long TotalUsed { get; set; }
    }
}
