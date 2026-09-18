using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions
{
    /// <summary>
    /// Kassieren am Zahlungsterminal (Achse C), aus Sicht der Anwendung.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Gegenpart zu <see cref="ITerminalDevice"/>: dieser hier kennt Mandant, Verkauf und Provision,
    /// jener nur Betrag und Gerät. Was hier passiert, ist dasselbe wie bei einem Online-Verkauf — eine
    /// Verkaufszeile entsteht, die Provision wird eingefroren, Beobachter werden benachrichtigt. Nur der
    /// Auslöser ist ein anderer: ein Gerät statt einer Zahlungsseite.
    /// </para>
    /// <para>
    /// <b>Alles hier ist asynchron und wiederholbar.</b> Keine dieser Methoden wartet, bis der Kunde
    /// gezahlt hat — das dauert, weil ein Mensch eine Karte einsteckt, und eine Verbindung, die so lange
    /// offen steht, bricht irgendwann im ungünstigsten Moment. Der Ablauf ist deshalb immer: starten,
    /// Kennung behalten, nachfragen.
    /// </para>
    /// </remarks>
    public interface ITerminalPaymentService
    {
        /// <summary>Die Geräte eines Mandanten, für die Auswahl an der Kasse.</summary>
        /// <param name="includeDisabled">auch die abgeschalteten, für die Verwaltung</param>
        Task<IReadOnlyList<TerminalInfo>> GetTerminalsAsync(int tenantId, bool includeDisabled = false,
            CancellationToken cancellationToken = default);

        /// <summary>Ob ein Gerät gerade erreichbar und frei ist.</summary>
        Task<TerminalStatus> GetTerminalStatusAsync(int tenantId, int terminalId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Startet eine Zahlung am Gerät und legt dabei den Verkauf an.
        /// </summary>
        /// <remarks>
        /// Idempotent über <see cref="TerminalSaleRequest.ExternalReference"/>: derselbe Auftrag zweimal
        /// geschickt ergibt <b>keinen</b> zweiten Verkauf und keine zweite Belastung, sondern den Stand
        /// des ersten. Das ist keine Bequemlichkeit, sondern der Normalfall an einer Kasse, deren
        /// Netzwerk hakt.
        /// </remarks>
        Task<TerminalSaleResult> StartPaymentAsync(TerminalSaleRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fragt nach, wie es um einen Vorgang steht — und bucht, was inzwischen feststeht.
        /// </summary>
        /// <remarks>
        /// <b>Der Aufruf, der nach einem Abbruch die Wahrheit herstellt.</b> Die Kasse ruft ihn, solange
        /// der Stand <see cref="TerminalPaymentState.InProgress"/> oder
        /// <see cref="TerminalPaymentState.Unknown"/> ist. Steht das Ergebnis fest, wird der Verkauf hier
        /// gebucht — über dieselbe Senke wie ein Webhook, mit denselben Garantien.
        /// </remarks>
        Task<TerminalSaleResult> GetPaymentAsync(int tenantSaleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bricht einen laufenden Vorgang ab.
        /// </summary>
        /// <remarks>
        /// Kann verweigert werden, wenn die Karte schon aufliegt — dann bleibt der Stand
        /// <see cref="TerminalPaymentState.InProgress"/>, und das ist die richtige Antwort. Ein Abbruch,
        /// der nur behauptet wird, wäre die gefährliche.
        /// </remarks>
        Task<TerminalSaleResult> CancelPaymentAsync(int tenantSaleId, CancellationToken cancellationToken = default);
    }

    /// <summary>Ein Gerät, wie die Kasse es zur Auswahl braucht.</summary>
    /// <param name="TerminalId">unser Schlüssel, nicht der des Anbieters</param>
    /// <param name="DisplayName">wie das Gerät im Haus heisst</param>
    /// <param name="Provider">über wen es abrechnet</param>
    /// <param name="ProviderTerminalId">wie es beim Anbieter bzw. auf dem Agenten heisst</param>
    /// <param name="Enabled">ob es benutzt werden darf</param>
    public readonly record struct TerminalInfo(int TerminalId, string DisplayName, string Provider,
        string ProviderTerminalId, bool Enabled);

    /// <summary>Der Auftrag, an einem Gerät zu kassieren.</summary>
    public class TerminalSaleRequest
    {
        /// <summary>Der Mandant, in dessen Namen kassiert wird.</summary>
        public int TenantId { get; set; }

        /// <summary>
        /// Das Gerät — <b>unser</b> Schlüssel. Absichtlich nicht die Kennung beim Anbieter: die käme aus
        /// der Anfrage, und dann liesse sich auf einem fremden Gerät kassieren.
        /// </summary>
        public int TerminalId { get; set; }

        /// <summary>Die Bestellnummer des Hosts. Trägt die Idempotenz.</summary>
        public string ExternalReference { get; set; } = string.Empty;

        /// <summary>Was auf dem Gerät und auf dem Beleg steht.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Der Betrag in der HAUPTeinheit (49.90), wie beim Online-Verkauf auch.</summary>
        public decimal Amount { get; set; }

        /// <summary>Die Währung. Leer heisst: die Vorgabe aus den Einstellungen.</summary>
        public string? Currency { get; set; }

        /// <summary>Für den Beleg, falls er verschickt wird.</summary>
        public string? CustomerEmail { get; set; }

        /// <summary>Ob der Kunde am Gerät abbrechen darf.</summary>
        public bool AllowCustomerCancellation { get; set; } = true;

        /// <summary>Was der Host mitgeben will.</summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>Was aus einem Terminalvorgang geworden ist.</summary>
    public class TerminalSaleResult
    {
        /// <summary>Der Verkauf bei uns.</summary>
        public int TenantSaleId { get; set; }

        /// <summary>Die Bestellnummer des Hosts.</summary>
        public string ExternalReference { get; set; } = string.Empty;

        /// <summary>Das benutzte Gerät.</summary>
        public int TerminalId { get; set; }

        /// <summary>Der Stand am Gerät.</summary>
        public TerminalPaymentState State { get; set; }

        /// <summary>Der Stand in unseren Büchern.</summary>
        public TenantSaleStatus SaleStatus { get; set; }

        /// <summary>Der Betrag in der kleinsten Einheit.</summary>
        public long AmountMinor { get; set; }

        /// <summary>Die Währung.</summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>Ein Trinkgeld, falls das Gerät eines erfasst hat.</summary>
        public long? TipMinor { get; set; }

        /// <summary>Der Grund des Scheiterns, roh vom Anbieter.</summary>
        public string? FailureCode { get; set; }

        /// <summary>Die Meldung des Anbieters. Zur Anzeige, nicht für Fallunterscheidungen.</summary>
        public string? FailureMessage { get; set; }

        /// <summary>Was auf den Beleg gehört, sobald es feststeht.</summary>
        public TerminalReceipt? Receipt { get; set; }

        /// <summary>
        /// Ob die Kasse weiter nachfragen soll. Wahr bei <c>InProgress</c> und
        /// <b>ebenso bei <c>Unknown</c></b> — genau dann ist Nachfragen die einzige richtige Reaktion.
        /// </summary>
        public bool KeepPolling => State is TerminalPaymentState.InProgress or TerminalPaymentState.Unknown;
    }

    /// <summary>
    /// Findet den Agenten, auf dem ein bestimmtes Terminal lebt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bewusst nur ein Vertrag, ohne mitgelieferte Umsetzung.</b> Wie aus der Geräte-Zeile eine
    /// Verbindung wird, weiss die Anwendung — sie hat ihre Proxy-Verdrahtung schon, und das Toolkit
    /// hätte hier nichts beizutragen ausser einer Annahme, die bei zwei Agenten falsch wäre.
    /// </para>
    /// <para>
    /// Die Zeile trägt beides: <see cref="TenantPaymentTerminal.Route"/> nennt den Dienst, und
    /// <see cref="TenantPaymentTerminal.ConfigurationJson"/> kann darüber hinaus alles enthalten, was
    /// der Weg dorthin braucht — etwa den Namen des Objekts, das auf dem Agenten
    /// <see cref="ITerminalDevice"/> bereitstellt. <b>Dasselbe JSON geht danach unverändert an das
    /// Gerät weiter</b>, das sich daraus nimmt, was es versteht. Eine Quelle für beide Hälften der
    /// Frage „wen rufe ich, und wie redet der mit dem Terminal".
    /// </para>
    /// </remarks>
    public interface ITerminalAgentLocator
    {
        /// <summary>Liefert den Zugang zu dem Gerät, das die Zeile beschreibt.</summary>
        /// <exception cref="TerminalDeviceException">wenn der Agent nicht erreichbar oder nicht bekannt ist</exception>
        Task<ITerminalDevice> GetDeviceAsync(TenantPaymentTerminal terminal, CancellationToken cancellationToken = default);
    }
}
