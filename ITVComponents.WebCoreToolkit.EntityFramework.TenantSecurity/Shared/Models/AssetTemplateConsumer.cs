using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Verbindet eine Asset-Vorlage mit einem Endpunkt, der ihre Argumente versteht (siehe
    /// <see cref="AssetConsumer"/>).
    /// <para>
    /// Das ist der Eintrag, der die Vorlage von einer blossen Pfadschranke zu etwas macht, mit dem sich
    /// arbeiten laesst: aus der Route-Vorlage des Konsumenten und den Argumentwerten laesst sich die
    /// fertige URL bauen, und die Teilen-Maske weiss, welche Felder sie zeigen muss.
    /// </para>
    /// <para>
    /// Verwiesen wird ueber Art und Schluessel statt ueber die Id des Konsumenten: die Zeile in
    /// <see cref="AssetConsumer"/> entsteht erst, wenn sich der Endpunkt zum ersten Mal meldet - eine
    /// Vorlage darf aber schon vorher auf ihn zeigen duerfen.
    /// </para>
    /// </summary>
    [Index(nameof(AssetTemplateId), nameof(DeclarationKind), nameof(DeclarationKey), IsUnique = true,
        Name = "UQ_AssetTemplateConsumer")]
    public class AssetTemplateConsumer
    {
        [Key]
        public int AssetTemplateConsumerId { get; set; }

        /// <summary>Die Vorlage. Logische Referenz, kein Fremdschluessel - siehe <see cref="AssetTemplateArgument"/>.</summary>
        public int AssetTemplateId { get; set; }

        public AssetConsumerKind DeclarationKind { get; set; }

        [Required, MaxLength(1024)]
        public string DeclarationKey { get; set; }

        /// <summary>
        /// Ob aus diesem Konsumenten der Link gebaut wird, wenn eine Freigabe entsteht. Genau einer je
        /// Vorlage sollte das sein; die uebrigen sind Stellen, die dieselbe Freigabe mitbenutzen (die
        /// Datei-Behandlung hinter der Seite zum Beispiel).
        /// </summary>
        public bool IsEntryPoint { get; set; }
    }
}
