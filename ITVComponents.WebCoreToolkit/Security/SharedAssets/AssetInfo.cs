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

        /// <summary>
        /// Die Argumente, die die Vorlage fuehrt - was eine Freigabe damit ueberhaupt bezeichnen kann.
        /// Leer bei Vorlagen ohne Objektbindung; die verhalten sich wie eh und je.
        /// </summary>
        public AssetArgumentDeclaration[] Arguments { get; set; } = Array.Empty<AssetArgumentDeclaration>();

        /// <summary>
        /// Worauf diese Freigabe zeigt. Leer, solange die Vorlage keine Argumente fuehrt.
        /// </summary>
        public AssetArgumentValues Values { get; set; } = AssetArgumentValues.Empty;

        /// <summary>
        /// Wie streng die Bestaetigung der Argumente verlangt wird.
        /// </summary>
        public AssetArgumentEnforcement Enforcement { get; set; } = AssetArgumentEnforcement.None;
    }
}
