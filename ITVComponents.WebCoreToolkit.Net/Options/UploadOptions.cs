using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.Options
{
    public class UploadOptions
    {
        public long MaxUploadSize { get; set; } = 30000000;

        public List<byte[]> MagicNumbers { get; set; } = new List<byte[]>();

        public List<string> FileExtensions { get; set; } = new List<string>();

        public List<string> UncheckedFileExtensions { get; set; } = new List<string>();
    }
}
