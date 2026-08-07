using System;
using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Consent;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Der Nachweis einer erteilten (oder verweigerten) Zustimmung: wer wann wozu in welcher Sprache Ja
    /// oder Nein gesagt hat.
    /// </summary>
    /// <remarks>
    /// Bewusst ohne Fremdschluessel - wie <see cref="PendingOnboarding"/>: die Zustimmung faellt beim
    /// anonymen Start, lange bevor es einen Mandanten gibt, und sie soll den Benutzer und den Mandanten
    /// ueberleben. Ein Nachweis, der beim Loeschen des Mandanten mitverschwindet, ist keiner.
    /// <para>
    /// Es wird auch die Ablehnung festgehalten, nicht nur die Zustimmung: dass jemand den Newsletter
    /// ausdruecklich NICHT wollte, ist genau die Auskunft, die man spaeter braucht - sie unterscheidet
    /// sich von "wurde nie gefragt".
    /// </para>
    /// </remarks>
    [Index(nameof(UserId), Name = "IX_ConsentRecordUser")]
    [Index(nameof(TenantId), Name = "IX_ConsentRecordTenant")]
    [Index(nameof(ConsentKey), Name = "IX_ConsentRecordKey")]
    public class ConsentRecord
    {
        [Key]
        public int ConsentRecordId { get; set; }

        /// <summary>Der Schluessel des Zustimmungspunkts aus der Konfiguration.</summary>
        [Required, MaxLength(200)]
        public string ConsentKey { get; set; }

        /// <summary>Der Stand des Dokuments, der dabei angezeigt wurde. Leer, wenn keiner gepflegt ist.</summary>
        [MaxLength(100)]
        public string Version { get; set; }

        /// <summary>Zugestimmt (<c>true</c>) oder ausdruecklich abgelehnt (<c>false</c>).</summary>
        public bool Accepted { get; set; }

        /// <summary>
        /// Wann die Zustimmung erteilt wurde - der Zeitpunkt der Handlung des Benutzers, NICHT der des
        /// Ablegens. Bei einem geparkten Onboarding liegen dazwischen Stunden oder Tage.
        /// </summary>
        public DateTime AcceptedUtc { get; set; }

        /// <summary>Der Benutzer, sofern er zum Zeitpunkt der Ablage feststeht. Kein FK.</summary>
        [MaxLength(450)]
        public string UserId { get; set; }

        /// <summary>
        /// Die E-Mail, unter der zugestimmt wurde. Beim anonymen Start ist sie die einzige Kennung, die es
        /// gibt - und sie bleibt lesbar, wenn das Konto spaeter verschwindet.
        /// </summary>
        [MaxLength(256)]
        public string Email { get; set; }

        /// <summary>
        /// Wen die Zustimmung betrifft - die natuerliche Person hinter dem Konto, den Mandanten, oder
        /// beide.
        /// </summary>
        public ConsentScope Scope { get; set; }

        /// <summary>
        /// Der Mandant, dem die Zustimmung gilt. Null beim blossen Anlegen eines Kontos - und ebenso bei
        /// jeder rein persoenlichen Zustimmung, auch wenn sie waehrend einer Mandanten-Anlage erteilt
        /// wurde. Kein FK.
        /// </summary>
        public int? TenantId { get; set; }

        /// <summary>Die Sprache, in der der Text angezeigt wurde.</summary>
        [MaxLength(35)]
        public string Culture { get; set; }

        /// <summary>Das Hilfe-Thema, das dabei als Dokument verlinkt war.</summary>
        [MaxLength(200)]
        public string HelpSlug { get; set; }

        /// <summary>
        /// Der Vorgang, aus dem die Zustimmung stammt (<c>TenantOnboarding</c> oder
        /// <c>AccountRegistration</c>) - fuer die Auswertung, aus welcher Kante ein Nachweis kam.
        /// </summary>
        [MaxLength(100)]
        public string Origin { get; set; }
    }
}
