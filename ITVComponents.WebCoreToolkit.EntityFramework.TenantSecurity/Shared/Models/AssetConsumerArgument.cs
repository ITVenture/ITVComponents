using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Ein Argument, das ein <see cref="AssetConsumer"/> versteht.
    /// <para>
    /// Eigene Tabelle und nicht flach am Konsumenten, weil <see cref="AssetConsumer.LastSeenUtc"/> dem
    /// Endpunkt gehoert und nicht jedem Argument: flach muesste eine Sichtung N Zeilen anfassen und
    /// koennte innerhalb desselben Endpunkts abweichende Zeitstempel hinterlassen. Ausserdem werden so
    /// "Argument weg" und "Endpunkt weg" unterscheidbar.
    /// </para>
    /// </summary>
    [Index(nameof(AssetConsumerId), nameof(ArgumentName), IsUnique = true, Name = "UQ_AssetConsumerArgument")]
    public class AssetConsumerArgument
    {
        [Key]
        public int AssetConsumerArgumentId { get; set; }

        public int AssetConsumerId { get; set; }

        [Required, MaxLength(128)]
        public string ArgumentName { get; set; }

        /// <summary>
        /// Der erwartete Typ. Steuert die Eingabemaske und - wichtiger - den Vergleich: der laeuft ueber
        /// eine kanonische Form, damit "04711" und 4711 dieselbe Antwort geben.
        /// </summary>
        public AssetArgumentType ArgumentType { get; set; }

        public bool Required { get; set; }

        /// <summary>
        /// Die Reihenfolge, in der der Endpunkt seine Argumente nennt. Die Teilen-Maske zeigt sie so an -
        /// alphabetisch waere fuer den Benutzer willkuerlich.
        /// </summary>
        public int SortOrder { get; set; }

        [ForeignKey(nameof(AssetConsumerId))]
        public virtual AssetConsumer Consumer { get; set; }
    }
}
