using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.BlobStore.Internal
{
    internal class FileListModel
    {
        public string Name { get; set; }
        public long Size { get;  set; }
        public long FirstBlock { get; set; } = -1;
        public Guid Id { get; set; }
        public Guid LinkId { get; set; }
        public bool Deleted { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public byte[] Hash { get; set; }

        public event EventHandler Dirty;

        internal virtual void MarkDirty()
        {
            Dirty?.Invoke(this, EventArgs.Empty);
        }
    }
}
