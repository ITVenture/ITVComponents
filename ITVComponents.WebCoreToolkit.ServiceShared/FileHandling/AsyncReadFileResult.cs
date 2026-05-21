using System;
using System.Collections.Generic;
using System.IO;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
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
