using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Ein Argument, das eine Asset-Vorlage fuehrt - also das, worauf eine damit erzeugte Freigabe zeigt.
    /// <para>
    /// <b>Logische Referenz statt Fremdschluessel:</b> <see cref="AssetTemplateId"/> verweist auf die
    /// Vorlage, ohne eine Beziehung im Modell zu bilden. Die Vorlage ist generisch (sie traegt die
    /// Berechtigung des Hosts als Typparameter), und eine echte Beziehung wuerde diese Tabelle in
    /// dieselbe Typkette zwingen - mitsamt allen Kontexten, Handlern und Constraints. Dieselbe
    /// Entscheidung wie bei den Mandanten-Referenzen im Billing-Zweig.
    /// </para>
    /// <para>
    /// Preis: die Datenbank raeumt nicht mit auf. Wer eine Vorlage loescht, muss ihre Argumente mit
    /// loeschen - das passiert im Anbieter, nicht per Cascade.
    /// </para>
    /// </summary>
    [Index(nameof(AssetTemplateId), nameof(ArgumentName), IsUnique = true, Name = "UQ_AssetTemplateArgument")]
    public class AssetTemplateArgument
    {
        [Key]
        public int AssetTemplateArgumentId { get; set; }

        /// <summary>Die Vorlage, zu der dieses Argument gehoert. Logische Referenz, kein Fremdschluessel.</summary>
        public int AssetTemplateId { get; set; }

        [Required, MaxLength(128)]
        public string ArgumentName { get; set; }

        public AssetArgumentType ArgumentType { get; set; }

        public bool Required { get; set; }

        public int SortOrder { get; set; }

        /// <summary>
        /// Der Schluessel des Aufloesers, mit dem der Host ein Argument einer Anfrage auf die geteilte
        /// Ebene normalisiert - "zu dieser Position gehoert Auftrag 4711". Leer, wenn nur direkt
        /// verglichen wird.
        /// </summary>
        [MaxLength(128)]
        public string ResolverKey { get; set; }
    }
}
