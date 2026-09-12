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

        /// <summary>
        /// Was diese Vorlage ueber die Teilen-Maske vorgibt - <b>als Text, ungedeutet</b>. Null, wenn sie
        /// nichts vorgibt; dann sieht die Maske aus wie bisher.
        /// </summary>
        /// <remarks>
        /// Bewusst nicht ausgewertet: welche Felder eine Teilen-Maske hat, ist die Sache dieser Maske und
        /// nicht die des Kerns - eines davon (die Reichweite) existiert ueberhaupt nur dort, als
        /// Zusammenfassung der Benutzer- und Mandantenfilter. Gedeutet wird das hier von
        /// <c>ShareDialogOptions.Parse</c> in den AdminViews.
        /// </remarks>
        public string ShareDialogConfig { get; set; }
    }
}
