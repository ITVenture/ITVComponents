using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments
{
    /// <summary>
    /// Ein Zahlungsterminal eines Mandanten. Ein Mandant kann mehrere haben — je Kasse, je Filiale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Warum es diese Tabelle gibt und die Kasse die Gerätekennung nicht einfach mitschickt:</b> Ohne
    /// sie liesse sich auf dem Terminal eines FREMDEN Mandanten kassieren — die Kennung wäre ein Wert aus
    /// der Anfrage, und niemand prüfte, wem das Gerät gehört. Das Geld landete dann auf dem richtigen
    /// Konto, aber auf dem falschen.
    /// </para>
    /// <para>
    /// Der <see cref="Provider"/> steht hier und nicht nur am Konto: das Gerät entscheidet, über wen
    /// gezahlt wird. Ein Mandant kann sein Ladengeschäft über ein Payrexx-Terminal führen und seinen
    /// Online-Shop über Stripe.
    /// </para>
    /// </remarks>
    [Index(nameof(Provider), nameof(ProviderTerminalId), IsUnique = true, Name = "IX_UniqueProviderTerminal")]
    [Index(nameof(TenantId), Name = "IX_TenantPaymentTerminal_Tenant")]
    public class TenantPaymentTerminal
    {
        /// <summary>Der Schlüssel.</summary>
        [Key]
        public int TenantPaymentTerminalId { get; set; }

        /// <summary>Der Mandant. Kein FK — Billing bleibt vom Mandantenmodell unabhängig.</summary>
        public int TenantId { get; set; }

        /// <summary>
        /// Über wen dieses Gerät abrechnet: <c>stripe</c>, <c>payrexx</c>, <c>wallee</c>, <c>agent</c>.
        /// </summary>
        [MaxLength(64)]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Wie das Gerät <b>dort drüben</b> heisst: die Reader-Id bei Stripe, die Seriennummer bei
        /// Payrexx, die Terminal-Id bei wallee — und beim Agenten der Name des Objekts, das das Terminal
        /// vertritt.
        /// </summary>
        /// <remarks>
        /// Eigene Spalte und nicht Teil von <see cref="ConfigurationJson"/>, aus drei Gründen: die
        /// Zugehörigkeitsprüfung läuft darüber, ein eindeutiger Index über
        /// (<see cref="Provider"/>, diese Spalte) verhindert, dass dasselbe Gerät zweimal registriert
        /// wird — bei zwei verschiedenen Mandanten wäre das genau der Unfall von oben —, und in einer
        /// Fehlersuche muss „Gerät xy ist offline" wiederzufinden sein.
        /// </remarks>
        [MaxLength(256)]
        public string ProviderTerminalId { get; set; } = string.Empty;

        /// <summary>
        /// <b>Wo</b> „dort drüben" ist — beim Agenten-Weg der Name des Dienstes, über den der Kassen-PC
        /// erreicht wird. Bei den Cloud-Wegen leer, weil dort der Anbieter selbst der Weg ist.
        /// </summary>
        /// <remarks>
        /// Eigene Spalte, weil ein Betrieb sie sieht und ändert: zieht ein Kassen-PC um oder bekommt einen
        /// neuen Namen, ist das hier eine Korrektur und keine JSON-Bastelei. Wie aus diesem Namen ein
        /// Proxy wird, entscheidet die Anwendung über ihren
        /// <c>ITerminalAgentLocator</c> — das Toolkit schreibt keinen Transport vor.
        /// </remarks>
        [MaxLength(256)]
        public string? Route { get; set; }

        /// <summary>Wie das Gerät im Haus genannt wird („Kasse 1", „Theke hinten").</summary>
        [MaxLength(128)]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Was nur die konkrete Ausprägung versteht, als JSON — die IP des Terminals bei wallee LTI, eine
        /// Location bei Stripe, ein Kopplungszustand bei Payrexx.
        /// </summary>
        /// <remarks>
        /// Hier landet, was sich je Anbieter unterscheidet und was kein Index je sehen muss. Was jeder
        /// Weg braucht, steht dagegen oben in eigenen Spalten — ein JSON-Klumpen für alles wäre kürzer
        /// zu schreiben und teurer zu betreiben.
        /// </remarks>
        public string? ConfigurationJson { get; set; }

        /// <summary>
        /// Ob das Gerät benutzt werden darf. Ein ausgemustertes wird abgeschaltet und nicht gelöscht —
        /// die Verkäufe darauf verweisen weiterhin auf es.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Wann es angelegt wurde.</summary>
        public DateTime Created { get; set; }

        /// <summary>Wann zuletzt etwas daran geändert wurde.</summary>
        public DateTime Updated { get; set; }

        /// <summary>Wann zuletzt erfolgreich damit gesprochen wurde.</summary>
        public DateTime? LastSeenUtc { get; set; }
    }
}
