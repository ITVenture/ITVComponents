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

        /// <summary>
        /// Wie ausfuehrlich Zugriffe protokolliert werden.
        /// </summary>
        public AssetAuditMode AuditMode { get; set; } = AssetAuditMode.All;

        /// <summary>
        /// Die Vorlage, aus der die Rechte stammen - im Protokoll das Einzige, was gespeicherte
        /// Freigaben und Ad-hoc-Tickets gemeinsam haben.
        /// </summary>
        public string TemplateSystemKey { get; set; }

        /// <summary>
        /// Die Kennung eines Ad-hoc-Tickets, oder null.
        /// </summary>
        public string TicketNonce { get; set; }

        /// <summary>
        /// An wen die Freigabe gerichtet ist.
        /// </summary>
        public string RecipientLabel { get; set; }
    }
}
