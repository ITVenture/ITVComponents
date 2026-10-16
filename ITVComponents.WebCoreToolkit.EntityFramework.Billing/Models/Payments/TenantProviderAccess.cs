using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments
{
    /// <summary>
    /// Ein eigener Zugang eines Mandanten bei einem Zahlungsanbieter - das Konto, das er schon hatte, bevor er
    /// zur Plattform kam (wallee, Payrexx, SumUp).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Der Rueckfall neben dem Plattform-Konto, nicht ein Ersatz dafuer.</b> Ohne aktive Zeile fuer einen
    /// Anbieter laeuft alles wie bisher ueber den Zugang der Plattform. Steht eine da, gilt sie fuer NEUE
    /// Vorgaenge dieses Mandanten bei diesem Anbieter - fuer den Online-Verkauf (Achse B) wie an den
    /// Terminals (Achse C).
    /// </para>
    /// <para>
    /// <b>Die Geheimnisse stehen NICHT hier.</b> Sie liegen als Dienst des Mandanten in den ExternalServices
    /// der Sicherheits-Datenbank und sind dort mit dem Schluessel des Mandanten verschluesselt;
    /// <see cref="ConnectionName"/> verweist darauf. Hier steht nur, was jeder sehen darf: wie der Zugang beim
    /// Anbieter heisst (<see cref="ProviderAccountId"/>) und was er sonst braucht (<see cref="SettingsJson"/>).
    /// </para>
    /// <para>
    /// <b>Abschalten statt loeschen, und ein neuer Zugang ist eine neue Zeile.</b> Ein Verkauf friert ein, ueber
    /// welchen Zugang er lief (<see cref="TenantSale.TenantProviderAccessId"/>). Wechselt der Mandant das Konto,
    /// muessen seine alten Verkaeufe weiterhin auf dem ALTEN erstattet werden - ueberschriebe man die Zeile,
    /// ginge die Erstattung ans neue Konto und traefe dort nichts oder eine fremde Transaktion. Dieselbe Regel
    /// wie beim Anbieter selbst (<see cref="TenantSale.Provider"/>).
    /// </para>
    /// <para>
    /// Hoechstens EIN aktiver Zugang je Mandant und Anbieter. Das haelt die Weiche fest, nicht ein gefilterter
    /// Index: dessen Bedingung waere SQL-Server-Syntax, und die Ablage soll auf einer zweiten Datenbank laufen.
    /// </para>
    /// </remarks>
    [Index(nameof(TenantId), nameof(Provider), Name = "IX_TenantProviderAccess_TenantProvider")]
    [Index(nameof(TenantId), nameof(ConnectionName), IsUnique = true, Name = "IX_UniqueTenantProviderAccessConnection")]
    public class TenantProviderAccess
    {
        [Key]
        public int TenantProviderAccessId { get; set; }

        /// <summary>Logical tenant identifier. No FK — Billing stays tenant-model-agnostic.</summary>
        public int TenantId { get; set; }

        /// <summary>
        /// Der Anbieter: <c>wallee</c>, <c>payrexx</c>, <c>sumup</c>. Derselbe Wertebereich und dieselbe Laenge
        /// wie <see cref="TenantSale.Provider"/>.
        /// </summary>
        [Required, MaxLength(64)]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Der Name des ExternalService des Mandanten, der die Geheimnisse haelt
        /// (<c>UniqueConnectionName</c>). Je Zeile ein eigener - sonst wechselte mit einem neuen Konto auch der
        /// Schluessel der alten Zeile, und ihre Erstattungen liefen mit dem falschen.
        /// </summary>
        [Required, MaxLength(255)]
        public string ConnectionName { get; set; } = string.Empty;

        /// <summary>
        /// Wie der Zugang beim Anbieter heisst: die Payrexx-Instanz, die wallee-Space-Id, der SumUp-Haendlercode.
        /// Wird beim Anlegen eines Verkaufs als dessen <see cref="TenantSale.ProviderAccountId"/> eingefroren.
        /// </summary>
        [Required, MaxLength(256)]
        public string ProviderAccountId { get; set; } = string.Empty;

        /// <summary>Was der Mandant in der Verwaltung liest - "Payrexx Filiale Bern".</summary>
        [MaxLength(128)]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Weitere nicht-geheime Angaben des Anbieters als JSON. Was darin steht, entscheidet allein der
        /// Anbieter; die Weiche reicht es nur durch.
        /// </summary>
        public string? SettingsJson { get; set; }

        /// <summary>
        /// Abgeschaltet: gilt fuer keinen neuen Vorgang mehr, bleibt aber fuer die Verkaeufe, die darueber
        /// liefen, erreichbar (Erstattungen, Nachfragen).
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>Wann der Zugang zuletzt erfolgreich geprueft wurde.</summary>
        public DateTime? LastVerifiedUtc { get; set; }

        /// <summary>Was bei der letzten Pruefung schiefging; leer, wenn sie gelang.</summary>
        [MaxLength(1024)]
        public string? LastVerificationError { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }
    }
}
