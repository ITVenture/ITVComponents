using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Ein Endpunkt, der Argumente eines geteilten Assets versteht - eine Seite, ein Controller, eine
    /// Datei-Behandlung. Die Zeile ist eine Eigenschaft des <b>Codes</b>, nicht eines Mandanten, und
    /// deshalb bewusst mandantenfrei: was ein Endpunkt versteht, ist fuer alle Mandanten dasselbe.
    /// <para>
    /// Die Deklaration ist <b>keine Sicherheitsschranke</b>. Sie fuettert die Teilen-Maske (welche
    /// Argumente sind hier vorzubelegen?) und die Pruefung beim Speichern einer Vorlage. Sicher wird der
    /// Zugriff erst durch die Bestaetigung im Endpunkt selbst.
    /// </para>
    /// </summary>
    [Index(nameof(DeclarationKind), nameof(DeclarationKey), IsUnique = true, Name = "UQ_AssetConsumer")]
    public class AssetConsumer
    {
        [Key]
        public int AssetConsumerId { get; set; }

        /// <summary>
        /// Wodurch der Konsument identifiziert wird - ueber seine Route oder ueber seinen Typ.
        /// </summary>
        public AssetConsumerKind DeclarationKind { get; set; }

        /// <summary>
        /// Die Route-Vorlage bzw. der CLR-Typ, je nach <see cref="DeclarationKind"/>.
        /// </summary>
        [Required, MaxLength(1024)]
        public string DeclarationKey { get; set; }

        /// <summary>
        /// Wann sich dieser Konsument zum ersten Mal gemeldet hat.
        /// </summary>
        public DateTime FirstSeenUtc { get; set; }

        /// <summary>
        /// Wann er sich zuletzt gemeldet hat. Die einzige Auskunft darueber, ob eine Zeile noch aktuell
        /// ist - deshalb wird ein Konsument <b>nie automatisch geloescht</b>: dass er sich seit dem
        /// Neustart nicht gemeldet hat, heisst nicht, dass es ihn nicht mehr gibt.
        /// </summary>
        public DateTime LastSeenUtc { get; set; }

        public virtual ICollection<AssetConsumerArgument> Arguments { get; set; } =
            new List<AssetConsumerArgument>();
    }
}
