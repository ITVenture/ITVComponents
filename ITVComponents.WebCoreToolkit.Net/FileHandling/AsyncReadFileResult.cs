using System;
using System.Collections.Generic;
using System.IO;
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
        public Stream FileContent { get; set; }

        public List<IDisposable> DeferredDisposals { get; } = new List<IDisposable>();
    }
}
