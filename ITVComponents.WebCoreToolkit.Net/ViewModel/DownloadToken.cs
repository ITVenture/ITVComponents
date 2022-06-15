using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.ViewModel
{
    public class DownloadToken
    {
        public string DownloadReason { get; set; }
        public string HandlerModuleName { get;set; }
        public string FileIdentifier { get; set; }
        public string DownloadName { get; set; }
        public string ContentType { get; set; }
        public string AssetKey { get; set; }
        public bool FileDownload { get; set; }
    }
}
