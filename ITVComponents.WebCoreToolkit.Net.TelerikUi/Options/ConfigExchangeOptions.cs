using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Options
{
    public class ConfigExchangeOptions
    {
        public bool UseConfigExchange { get; set; } = false;
        public string UploadModuleName { get; set; }
        public string HandlerReason { get; set; }
        public string UploadHintCallback { get; set; }
        public string ResultCallback { get; set; }
        public string HandlerTarget { get; set; }
        public string IdExtension { get; set; }
        public string UploaderDivId { get; set; }
        public string ResultTab { get; set; }
        public string DownloadName { get; set; } = "config.json";
        public string DownloadReason { get; set; } = "DownloadConfig";
        public string FileIdentifier { get; set; } = "SystemConfiguration";
    }
}
