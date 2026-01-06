using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.BlobStore.Internal;

namespace ITVComponents.BlobStore.Model
{
    public class File
    {
        private readonly FileListModel original;

        internal File(FileListModel original)
        {
            this.original = original;
        }

        public string Name
        {
            get => original.Name;
            set
            {
                original.Name = value; 
                original.MarkDirty();
            }
        }

        public DateTime ModifiedUtc
        {
            get { return original.ModifiedUtc;}
            set
            {
                original.ModifiedUtc = value; 
                original.MarkDirty();
            }
        }

        public DateTime CreatedUtc
        {
            get { return original.CreatedUtc; }
            set
            {
                original.CreatedUtc = value;
                original.MarkDirty();
            }
        }

        public long Size => original.Size;

        public byte[] Hash => original.Hash;

        internal Guid Id => original.Id;

        internal Guid LinkedFile => original.LinkId;
    }
}
