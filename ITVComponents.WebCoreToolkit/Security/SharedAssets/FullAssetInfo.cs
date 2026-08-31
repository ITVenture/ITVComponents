using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    public class FullAssetInfo:AssetInfo
    {
        // NotBefore/NotAfter sind an die Basis gewandert: die Gueltigkeit ist keine Eigentuemer-Auskunft,
        // sondern gehoert zu dem, was auch ein Empfaenger ueber seinen Link wissen darf.

        public string AnonymousAccessTokenRaw { get; set; }

        public List<string> UserShares { get; } = new List<string>();

        public List<string> UserScopeShares { get; } = new List<string>();

    }
}
