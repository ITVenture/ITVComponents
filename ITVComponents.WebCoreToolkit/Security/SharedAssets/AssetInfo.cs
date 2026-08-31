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

        /// <summary>
        /// Ob die Freigabe ohne Anmeldung benutzt werden darf (Benutzerfilter <c>##ANONYMOUS</c>). Bei
        /// einem Ad-hoc-Ticket immer true - es traegt sein Geheimnis selbst.
        /// <para>
        /// Sagt nichts darueber, WER gerade zugreift: ein anonym geteilter Link kann sehr wohl von einem
        /// angemeldeten Benutzer benutzt werden. Diese Unterscheidung steht in
        /// <see cref="AssetContext.VisitorIsAnonymous"/>.
        /// </para>
        /// </summary>
        public bool IsAnonymous { get; set; }

        /// <summary>
        /// Ab wann die Freigabe gilt, oder null. Frueher nur am <see cref="FullAssetInfo"/> und damit nur
        /// fuer den Eigentuemer sichtbar - der Empfaenger darf wissen, wie lange sein Link noch traegt.
        /// </summary>
        public DateTime? NotBefore { get; set; }

        /// <summary>
        /// Bis wann die Freigabe gilt, oder null.
        /// </summary>
        public DateTime? NotAfter { get; set; }
    }
}
