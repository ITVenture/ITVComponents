using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataExchange.Configuration;

namespace ITVComponents.WebCoreToolkit.Net.SpecialFileHandlers.Config
{
    public class DiagQueryDumpConfig
    {
        public DumpConfiguration DumpConfig { get; set; }
        public string DownloadName { get; set; }
        public bool? FileDownload { get; set; }
        public string ContentType { get; set; }
    }
}
