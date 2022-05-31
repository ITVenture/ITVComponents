using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    public class AssetInfo
    {
        public string AssetKey { get; set; }
        public string UserScopeName { get; set; }

        public string[] Features { get; set; }

        public string[] Permissions { get; set; }
        public string AssetTitle { get; set; }

        public string AssetRootPath { get; set; }
    }
}
