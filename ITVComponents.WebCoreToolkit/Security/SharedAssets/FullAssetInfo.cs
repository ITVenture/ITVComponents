using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    public class FullAssetInfo:AssetInfo
    {
        public DateTime? NotBefore { get; set; }

        public DateTime? NotAfter { get; set; }

        public string AnonymousAccessTokenRaw { get; set; }

        public List<string> UserShares { get; } = new List<string>();

        public List<string> UserScopeShares { get; } = new List<string>();
    }
}
