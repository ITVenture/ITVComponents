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

        /// <summary>
        /// Die Argumentwerte, die sich aus dem AUFGERUFENEN PFAD ablesen liessen - ueber benannte
        /// Gruppen im Pfadmuster der Vorlage (<c>^/CustomerCare/Customers/(?&lt;CustomerId&gt;\d+)$</c>).
        /// </summary>
        /// <remarks>
        /// Der schwaechste der drei Wege, aber der einzige ohne jede Mitwirkung der Seite. Ein Muster
        /// ohne benannte Gruppen liefert hier nichts - und ein Muster wie <c>.*</c> auch nicht, was
        /// stimmig ist: was keine Stelle bezeichnet, kann keinen Wert benennen.
        /// </remarks>
        public IReadOnlyDictionary<string, string> PathValues { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
