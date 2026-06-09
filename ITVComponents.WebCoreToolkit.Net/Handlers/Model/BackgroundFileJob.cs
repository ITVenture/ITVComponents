using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.Options;
using ITVComponents.WebCoreToolkit.ServiceShared.Model;
using ITVComponents.WebCoreToolkit.ServiceShared.Options;

namespace ITVComponents.WebCoreToolkit.Net.Handlers.Model
{
    internal class BackgroundFileJob
    {
        public string UploadModule { get; set; }
        public string Reason { get; set; }
        public string UploadHint { get; set; }
        public bool WithAuthorization { get; set; }
        public ParsedMultipartFile[] ParsedFiles { get; set; }
        public UploadOptions Options { get; set; }
        public bool HasAsset { get; set; }
        public string AssetKey { get; set; }
    }
}
