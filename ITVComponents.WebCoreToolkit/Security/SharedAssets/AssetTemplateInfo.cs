using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    public class AssetTemplateInfo
    {
        public string AssetTemplateTitle { get; set; }

        public string TemplateKey { get; set; }

        /// <summary>
        /// Die Argumente, die diese Vorlage verlangt. Die Teilen-Maske zeigt daraus ihre Felder - in
        /// dieser Reihenfolge, nicht alphabetisch.
        /// </summary>
        public AssetArgumentDeclaration[] Arguments { get; set; } = Array.Empty<AssetArgumentDeclaration>();
    }
}
