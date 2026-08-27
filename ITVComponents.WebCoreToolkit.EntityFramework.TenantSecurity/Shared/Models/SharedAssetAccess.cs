using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Ein protokollierter Zugriff auf eine Freigabe.
    /// <para>
    /// Geschrieben wird <b>je Vorgang</b> - nicht je Anfrage. Unterressourcen einer Seite laufen nicht
    /// durch den Riegel und tauchen hier deshalb nicht auf; andernfalls waere die Tabelle nach einer
    /// Woche unbenutzbar.
    /// </para>
    /// <para>
    /// <b>Der Mandantenbezug ist eine logische Referenz ohne Filter.</b> Die Tabelle liegt als
    /// Systemtabelle im <c>ICoreSystemContext</c> - wie die Argument-Registry, und aus demselben Grund:
    /// die Freigabe selbst ist generisch. Die Mandantengrenze zieht hier deshalb der Aufrufer und nicht
    /// die Datenbank; wer eigene Abfragen darauf schreibt, muss <see cref="TenantName"/> selbst
    /// einschraenken.
    /// </para>
    /// </summary>
    [Index(nameof(TenantName), nameof(Created), Name = "IX_SharedAssetAccessTenantTime")]
    [Index(nameof(AssetKey), Name = "IX_SharedAssetAccessAsset")]
    public class SharedAssetAccess
    {
        [Key]
        public int SharedAssetAccessId { get; set; }

        /// <summary>Die Freigabe, oder null bei einem Ad-hoc-Ticket.</summary>
        [MaxLength(128)]
        public string AssetKey { get; set; }

        /// <summary>
        /// Die Kennung eines Ad-hoc-Tickets, oder null. Sie ist das Einzige, worueber sich Zugriffe auf
        /// ein Ticket zuordnen lassen - ausgegebene Tickets werden bewusst nirgends gesammelt.
        /// </summary>
        [MaxLength(64)]
        public string TicketNonce { get; set; }

        /// <summary>Die Vorlage - das Einzige, was beide Faelle gemeinsam haben.</summary>
        [MaxLength(128)]
        public string TemplateSystemKey { get; set; }

        [Required, MaxLength(128)]
        public string TenantName { get; set; }

        [MaxLength(256)]
        public string RecipientLabel { get; set; }

        [MaxLength(256)]
        public string AccessedBy { get; set; }

        [MaxLength(1024)]
        public string RequestPath { get; set; }

        [MaxLength(1024)]
        public string ArgumentSummary { get; set; }

        public bool Granted { get; set; }

        /// <summary>Kurz und maschinenlesbar. Der ausfuehrliche Grund steht im Anwendungs-Log.</summary>
        [MaxLength(64)]
        public string DenyReason { get; set; }

        public DateTime Created { get; set; }
    }
}
