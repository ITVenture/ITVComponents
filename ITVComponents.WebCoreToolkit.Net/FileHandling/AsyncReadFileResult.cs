using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.FileHandling
{
    public class AsyncReadFileResult
    {
        public bool Success { get; set; }
        public string DownloadName { get; set; }
        public string ContentType { get; set; }
        public bool? FileDownload { get; set; }
        public byte[] FileContent { get; set; }
    }
}
