using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Ein zurueckgezogenes Ad-hoc-Ticket.
    /// <para>
    /// Die ehrliche Schwaeche eines Tickets ist, dass es nirgends steht - also laesst es sich auch nicht
    /// einzeln loeschen. Diese Tabelle ist das Gegenmittel, und sie ist bewusst klein gehalten: es
    /// stehen nur die WIDERRUFENEN Kennungen darin, nicht die ausgegebenen. Ein Ticket, das niemand
    /// zurueckzieht, hinterlaesst hier keine Zeile.
    /// </para>
    /// <para>
    /// <see cref="ExpiresUtc"/> ist die Frist des Tickets selbst: danach gilt es ohnehin nicht mehr, und
    /// die Zeile darf weg. Ohne dieses Feld waere die Sperrliste eine Tabelle, die nur waechst.
    /// </para>
    /// </summary>
    [Index(nameof(Nonce), IsUnique = true, Name = "UQ_RevokedAssetTicket")]
    public class RevokedAssetTicket
    {
        [Key]
        public int RevokedAssetTicketId { get; set; }

        /// <summary>Die Kennung des Tickets aus seiner Nutzlast.</summary>
        [Required, MaxLength(64)]
        public string Nonce { get; set; }

        /// <summary>
        /// Wann das Ticket von selbst geendet haette. Ab dann ist die Zeile ueberfluessig.
        /// </summary>
        public DateTime ExpiresUtc { get; set; }

        public DateTime RevokedUtc { get; set; }
    }
}
