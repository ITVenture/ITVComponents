using ITVComponents.AgentServices.Abstractions;
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

        /// <summary>
        /// Welche Angaben dieser Weg braucht, um ein Gerät anzulegen — der <b>erste</b> Schritt des
        /// Assistenten.
        /// </summary>
        /// <remarks>
        /// Was hier steht, weiss die Web-Anwendung: die Kennung beim Anbieter, bei einem Agenten der
        /// Weg dorthin. Was das Gerät selbst braucht, kommt erst danach — siehe
        /// <see cref="DescribeDeviceSettingsAsync"/>.
        /// </remarks>
        IReadOnlyList<TerminalSettingDescriptor> DescribeSettings();

        /// <summary>
        /// Ob nach dem ersten Schritt noch ein zweiter kommt.
        /// </summary>
        /// <remarks>
        /// Bei den Cloud-Wegen nein: dort redet die Anwendung selbst mit dem Anbieter, und mehr als
        /// dessen Gerätekennung gibt es nicht zu wissen. Bei einem Agenten ja — welche Angaben sein
        /// Gerät braucht, weiss nur er.
        /// </remarks>
        bool HasDeviceSettings { get; }

        /// <summary>
        /// Fragt die Angaben ab, die das Gerät selbst verlangt — der <b>zweite</b> Schritt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Bekommt das Gerät, wie es nach dem ersten Schritt aussieht: erst damit lässt sich der Agent
        /// überhaupt erreichen und fragen. Das ist der Grund für zwei Schritte statt einem — die
        /// zweite Feldliste hängt von der Antwort auf die erste ab.
        /// </para>
        /// <para>
        /// <b>Die Werte dieses Schritts landen nicht in der Geräte-Zeile</b>, sondern beim Gerät selbst
        /// (<see cref="ApplyDeviceSettingsAsync"/>). Liefert eine leere Liste, wenn es nichts zu fragen gibt.
        /// </para>
        /// </remarks>
        /// <param name="draft">das Gerät nach dem ersten Schritt; <see cref="TerminalDefinition.TenantId"/> ist gesetzt</param>
        /// <param name="cancellationToken">bricht die Abfrage ab</param>
        Task<IReadOnlyList<SettingDescriptor>> DescribeDeviceSettingsAsync(TerminalDefinition draft,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Die Einstellungen, die das Gerät gerade hat, als JSON — zum Vorbelegen der Maske.
        /// </summary>
        /// <returns>das JSON, oder <c>null</c>, wenn das Gerät noch nicht eingerichtet ist</returns>
        Task<string?> GetDeviceSettingsAsync(TerminalDefinition draft, CancellationToken cancellationToken = default);

        /// <summary>
        /// Eine Auswahl für ein Geräte-Feld, die nur der Rechner vor Ort kennt.
        /// </summary>
        /// <param name="draft">das Gerät</param>
        /// <param name="settingName">das Feld</param>
        /// <param name="draftJson">was in der Maske gerade steht</param>
        /// <param name="cancellationToken">bricht die Abfrage ab</param>
        Task<IReadOnlyList<SettingChoice>> GetDeviceChoicesAsync(TerminalDefinition draft, string settingName,
            string? draftJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Übergibt die Einstellungen an das Gerät, das sie prüft und bei sich ablegt.
        /// </summary>
        /// <returns>das Ergebnis; bei Fehlern je Feld eine Meldung, nichts wurde abgelegt</returns>
        Task<ApplySettingsResult> ApplyDeviceSettingsAsync(TerminalDefinition draft, string settingsJson,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Was es an Terminal-Wegen gibt und was sie zum Einrichten brauchen — die Grundlage des
    /// Assistenten „Gerät hinzufügen".
    /// </summary>
    /// <remarks>
    /// Getrennt von <see cref="ITerminalPaymentService"/>, weil die Frage eine andere ist: jener
    /// bedient EIN Gerät, dieser hier kennt ALLE Wege. Umgesetzt von der Weiche, die sie ohnehin
    /// beisammen hat.
    /// </remarks>
    public interface ITerminalProviderCatalog
    {
        /// <summary>Die Wege, die registriert sind — die Auswahlliste im ersten Schritt.</summary>
        IReadOnlyList<TerminalProviderInfo> GetProviders();

        /// <summary>Die Angaben, die ein bestimmter Weg im ersten Schritt braucht.</summary>
        IReadOnlyList<TerminalSettingDescriptor> DescribeSettings(string providerKey);

        /// <summary>
        /// Der zweite Schritt für den Weg, den <see cref="TerminalDefinition.Provider"/> nennt — siehe
        /// <see cref="ITerminalPaymentService.DescribeDeviceSettingsAsync"/>.
        /// </summary>
        Task<IReadOnlyList<SettingDescriptor>> DescribeDeviceSettingsAsync(TerminalDefinition draft,
            CancellationToken cancellationToken = default);

        /// <summary>Siehe <see cref="ITerminalPaymentService.GetDeviceSettingsAsync"/>.</summary>
        Task<string?> GetDeviceSettingsAsync(TerminalDefinition draft, CancellationToken cancellationToken = default);

        /// <summary>Siehe <see cref="ITerminalPaymentService.GetDeviceChoicesAsync"/>.</summary>
        Task<IReadOnlyList<SettingChoice>> GetDeviceChoicesAsync(TerminalDefinition draft, string settingName,
            string? draftJson, CancellationToken cancellationToken = default);

        /// <summary>Siehe <see cref="ITerminalPaymentService.ApplyDeviceSettingsAsync"/>.</summary>
        Task<ApplySettingsResult> ApplyDeviceSettingsAsync(TerminalDefinition draft, string settingsJson,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Ein Terminal-Weg, wie ihn die Auswahlliste braucht.</summary>
    /// <param name="Key">der Name, der am Gerät gespeichert wird</param>
    /// <param name="HasDeviceSettings">ob nach dem ersten Schritt noch einer kommt</param>
    public readonly record struct TerminalProviderInfo(string Key, bool HasDeviceSettings);

    /// <summary>
    /// Anlegen, ändern und abschalten von Geräten.
    /// </summary>
    /// <remarks>
    /// Der letzte Schritt des Assistenten braucht ihn: ohne ihn liesse sich beschreiben und ausfüllen,
    /// aber nichts speichern.
    /// </remarks>
    public interface ITerminalAdministration
    {
        /// <summary>Die Geräte eines Mandanten, mit allem, was zum Bearbeiten nötig ist.</summary>
        Task<IReadOnlyList<TerminalDefinition>> GetAsync(int tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Legt ein Gerät an oder ändert es.
        /// </summary>
        /// <returns>der Schlüssel des Geräts</returns>
        Task<int> SaveAsync(TerminalDefinition definition, CancellationToken cancellationToken = default);

        /// <summary>
        /// Schaltet ein Gerät ein oder aus.
        /// </summary>
        /// <remarks>
        /// Es gibt bewusst kein Löschen: die Verkäufe darauf verweisen weiterhin auf es, und ein
        /// ausgemustertes Gerät bleibt der Beleg dafür, wo kassiert wurde.
        /// </remarks>
        Task SetEnabledAsync(int tenantId, int terminalId, bool enabled,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Ein Gerät, wie der Assistent es anlegt.</summary>
    public class TerminalDefinition
    {
        /// <summary>Beim Anlegen 0, beim Ändern der bestehende Schlüssel.</summary>
        public int TerminalId { get; set; }

        /// <summary>Der Mandant, dem das Gerät gehört.</summary>
        public int TenantId { get; set; }

        /// <summary>Der gewählte Weg.</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>Die Kennung des Geräts beim Anbieter bzw. im Laden.</summary>
        public string ProviderTerminalId { get; set; } = string.Empty;

        /// <summary>Der Dienst, über den ein Agent erreicht wird. Bei den Cloud-Wegen leer.</summary>
        public string? Route { get; set; }

        /// <summary>Wie das Gerät im Haus genannt wird.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Das Ergebnis des ersten Assistenten-Schritts — gebaut mit <see cref="TerminalSettings.Compose"/>.
        /// </summary>
        /// <remarks>
        /// Beim Agenten-Weg steht hier nur, was die Web-Anwendung braucht (etwa das Objekt am Agenten). Die
        /// Einstellungen des Geräts liegen beim Gerät.
        /// </remarks>
        public string? ConfigurationJson { get; set; }

        /// <summary>Ob das Gerät benutzt werden darf.</summary>
        public bool Enabled { get; set; } = true;
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
    /// Die Zeile nennt den Weg: <see cref="TenantPaymentTerminal.Route"/> den Dienst (den <c>ClientKey</c>
    /// der Kasse), <see cref="TenantPaymentTerminal.ConfigurationJson"/> das Objekt, das dort
    /// <see cref="ITerminalDevice"/> bereitstellt (<see cref="AgentTerminals.ObjectNameSetting"/>).
    /// </para>
    /// <para>
    /// Mitgeliefert ist <see cref="Payments.AgentConnectorTerminalLocator"/>, der über den
    /// <c>IAgentConnector</c> des AgentServices-Webpakets geht. Ein Host mit eigener Verdrahtung darf
    /// einen eigenen registrieren; er hat dann Vorrang.
    /// </para>
    /// </remarks>
    public interface ITerminalAgentLocator
    {
        /// <summary>Liefert den Zugang zu dem Gerät, das die Zeile beschreibt.</summary>
        /// <exception cref="TerminalDeviceException">wenn der Agent nicht erreichbar oder nicht bekannt ist</exception>
        Task<ITerminalDevice> GetDeviceAsync(TenantPaymentTerminal terminal, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Was der Agenten-Weg in der Geräte-Zeile ablegt.
    /// </summary>
    public static class AgentTerminals
    {
        /// <summary>
        /// Der Schlüssel im Konfigurations-JSON, unter dem das Objekt am Agenten steht.
        /// </summary>
        public const string ObjectNameSetting = "objectName";

        /// <summary>
        /// Liest den Objektnamen aus dem Konfigurations-JSON einer Geräte-Zeile.
        /// </summary>
        /// <returns>der Name, oder <c>null</c>, wenn keiner eingetragen ist</returns>
        /// <exception cref="TerminalDeviceException">wenn das JSON nicht lesbar ist</exception>
        public static string? ObjectNameOf(string? configurationJson)
        {
            if (string.IsNullOrWhiteSpace(configurationJson))
            {
                return null;
            }

            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(configurationJson);
                return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                       && document.RootElement.TryGetProperty(ObjectNameSetting, out var value)
                       && value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? value.GetString()
                    : null;
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new TerminalDeviceException(
                    $"The configuration of this terminal is not readable JSON, so the object on the agent is unknown: {ex.Message}", ex);
            }
        }
    }
}
