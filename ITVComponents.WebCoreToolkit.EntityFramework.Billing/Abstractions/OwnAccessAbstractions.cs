using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions
{
    /// <summary>
    /// Ein Feld beim Anlegen eines eigenen Zugangs.
    /// </summary>
    /// <remarks>
    /// Dieselbe Feldbeschreibung wie bei den Terminals, mit zwei Ergänzungen. <see cref="Secret"/> gibt es, damit
    /// die Maske ein Kennwortfeld zeichnet und der Wert nie in der Zugangs-Zeile landet.
    /// <see cref="PartOfCredential"/> gibt es, damit beim Erneuern eines Schlüssels nur das gefragt wird, was zur
    /// Anmeldung gehört.
    /// </remarks>
    public sealed class OwnAccessField : SettingDescriptor
    {
        /// <summary>Ein Geheimnis: nur als Kennwortfeld zeigen, nur im ExternalService ablegen.</summary>
        public bool Secret { get; set; }

        /// <summary>Gehört zur Anmeldung, ohne geheim zu sein (die Benutzer-Id neben dem Schlüssel).</summary>
        public bool PartOfCredential { get; set; }
    }

    /// <summary>Ein Geheimnis, das als ExternalService des Mandanten abgelegt wird.</summary>
    /// <remarks>Keine <c>record</c>-Klasse: deren <c>ToString()</c> schriebe das Geheimnis aus.</remarks>
    public sealed class OwnAccessSecret
    {
        /// <summary>Initializes a new instance of the <see cref="OwnAccessSecret"/> class.</summary>
        /// <param name="connectionSuffix">angehängt an den Namen des Zugangs; leer für das Hauptgeheimnis</param>
        /// <param name="authenticationType">wie es verwendet wird</param>
        /// <param name="clientId">bei ApiKey der Kopf, bei Basic der Benutzer</param>
        /// <param name="secret">das Geheimnis</param>
        public OwnAccessSecret(string connectionSuffix, ExternalServiceAuthenticationType authenticationType,
            string? clientId, string secret)
        {
            ConnectionSuffix = connectionSuffix;
            AuthenticationType = authenticationType;
            ClientId = clientId;
            Secret = secret;
        }

        /// <summary>Angehängt an den Namen des Zugangs; leer für das Hauptgeheimnis.</summary>
        public string ConnectionSuffix { get; }

        /// <summary>Wie das Geheimnis verwendet wird.</summary>
        public ExternalServiceAuthenticationType AuthenticationType { get; }

        /// <summary>Bei ApiKey der Kopf, bei Basic der Benutzer.</summary>
        public string? ClientId { get; }

        /// <summary>Das Geheimnis.</summary>
        public string Secret { get; }

        /// <inheritdoc />
        public override string ToString() => $"{AuthenticationType} secret '{ConnectionSuffix}' (withheld)";
    }

    /// <summary>Was aus den Angaben der Maske wird: die Zeile und die Geheimnisse.</summary>
    public sealed class OwnAccessDraft
    {
        /// <summary>Wie der Zugang beim Anbieter heisst (Instanz, Space-Id, Händlercode).</summary>
        public string ProviderAccountId { get; set; } = string.Empty;

        /// <summary>Die übrigen nicht-geheimen Angaben, oder leer.</summary>
        public string? SettingsJson { get; set; }

        /// <summary>Die Geheimnisse.</summary>
        public IReadOnlyList<OwnAccessSecret> Secrets { get; set; } = [];
    }

    /// <summary>Was eine Prüfung oder Einrichtung ergab.</summary>
    /// <param name="Success">ob es geklappt hat</param>
    /// <param name="Message">was der Mandant dazu lesen soll</param>
    public readonly record struct OwnAccessCheck(bool Success, string? Message);

    /// <summary>
    /// Was ein Anbieter zum Einrichten eines eigenen Zugangs beiträgt.
    /// </summary>
    /// <remarks>
    /// Je Anbieter genau einer. Die Verwaltung (<see cref="IProviderAccessAdministration"/>) legt Zeile und
    /// Geheimnisse an. Was gefragt wird, wie die Angaben aufgeteilt werden und wie sich prüfen lässt, ob der
    /// Zugang funktioniert, weiss nur der Anbieter.
    /// </remarks>
    public interface IOwnAccessProvider
    {
        /// <summary>Der Anbieter: <c>payrexx</c>, <c>wallee</c>, <c>sumup</c>.</summary>
        string Key { get; }

        /// <summary>Die Felder der Maske, Geheimnisse eingeschlossen.</summary>
        IReadOnlyList<OwnAccessField> DescribeFields();

        /// <summary>Teilt die Angaben in Zeile und Geheimnisse auf.</summary>
        /// <exception cref="TenantPaymentException"><c>InvalidSettings</c>, wenn etwas fehlt oder nicht passt.</exception>
        OwnAccessDraft Compose(IReadOnlyDictionary<string, string?> values);

        /// <summary>
        /// Nur die Geheimnisse - zum Erneuern eines Schlüssels am SELBEN Konto. Die Felder dazu sind die mit
        /// <see cref="OwnAccessField.Secret"/> oder <see cref="OwnAccessField.PartOfCredential"/>.
        /// </summary>
        IReadOnlyList<OwnAccessSecret> ComposeSecrets(IReadOnlyDictionary<string, string?> values);

        /// <summary>Prüft mit einem harmlosen Aufruf, ob der Zugang funktioniert.</summary>
        Task<OwnAccessCheck> VerifyAsync(ProviderAccessSelection access, CancellationToken cancellationToken = default);

        /// <summary>
        /// Der Pfad, an den das Konto des Mandanten seine Meldungen schickt - ohne die Kennung des Zugangs, die
        /// angehängt wird. Leer, wenn der Anbieter keinen braucht (SumUp nennt die Adresse je Vorgang selbst).
        /// </summary>
        string? WebhookPath { get; }

        /// <summary>Ob sich die Meldungen im Konto des Mandanten von hier aus einrichten lassen.</summary>
        bool CanSetUpWebhooks { get; }

        /// <summary>Richtet die Meldungen im Konto des Mandanten ein (wallee: Webhook-URL und Listener).</summary>
        Task<OwnAccessCheck> SetUpWebhooksAsync(ProviderAccessSelection access, string webhookUrl,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Ein eigener Zugang, wie die Verwaltung ihn zeigt - ohne Geheimnisse.</summary>
    public sealed class OwnAccessInfo
    {
        public int AccessId { get; set; }

        public string Provider { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string ProviderAccountId { get; set; } = string.Empty;

        public bool Disabled { get; set; }

        public DateTime? LastVerifiedUtc { get; set; }

        public string? LastVerificationError { get; set; }

        /// <summary>Der Pfad, den der Mandant in seinem Konto als Webhook-Adresse einträgt; leer, wenn keiner nötig ist.</summary>
        public string? WebhookPath { get; set; }

        /// <summary>Ob sich die Meldungen von hier aus einrichten lassen.</summary>
        public bool CanSetUpWebhooks { get; set; }
    }

    /// <summary>
    /// Anlegen, Prüfen, Erneuern und Abschalten eigener Zugänge.
    /// </summary>
    /// <remarks>
    /// Prüft nur die Zugehörigkeit zum Mandanten. Rechte, Hauptschalter und Feature prüft der Handler der
    /// Oberfläche, wie bei den Terminals.
    /// </remarks>
    public interface IProviderAccessAdministration
    {
        /// <summary>Die Anbieter, für die sich ein eigener Zugang einrichten lässt.</summary>
        IReadOnlyList<string> GetProviders();

        /// <summary>Die Felder der Maske eines Anbieters.</summary>
        IReadOnlyList<OwnAccessField> DescribeFields(string provider);

        /// <summary>Die eigenen Zugänge eines Mandanten, abgeschaltete eingeschlossen.</summary>
        Task<IReadOnlyList<OwnAccessInfo>> GetAsync(int tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Legt einen eigenen Zugang an und prüft ihn. Ein bisher aktiver beim selben Anbieter wird abgeschaltet -
        /// seine Verkäufe bleiben auf ihm erstattbar.
        /// </summary>
        Task<int> CreateAsync(int tenantId, string provider, string displayName,
            IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);

        /// <summary>Ersetzt die Geheimnisse am SELBEN Konto (Schlüssel erneuert) und prüft danach.</summary>
        Task RenewSecretsAsync(int tenantId, int accessId, IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default);

        /// <summary>Schaltet einen Zugang ab oder wieder an. Wieder an nur, wenn beim Anbieter kein anderer aktiv ist.</summary>
        Task SetDisabledAsync(int tenantId, int accessId, bool disabled, CancellationToken cancellationToken = default);

        /// <summary>Prüft einen Zugang und hält das Ergebnis an der Zeile fest.</summary>
        Task<OwnAccessCheck> VerifyAsync(int tenantId, int accessId, CancellationToken cancellationToken = default);

        /// <summary>Richtet die Meldungen im Konto des Mandanten ein, soweit der Anbieter das kann.</summary>
        /// <param name="tenantId">der Mandant</param>
        /// <param name="accessId">der Zugang</param>
        /// <param name="publicBaseUrl">die öffentliche Adresse der Anwendung, ohne Pfad</param>
        /// <param name="cancellationToken">bricht den Vorgang ab</param>
        Task<OwnAccessCheck> SetUpWebhooksAsync(int tenantId, int accessId, string publicBaseUrl,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Ein Terminal-Weg, bei dem das Gerät über einen Code gekoppelt wird, den es selbst anzeigt (SumUp Solo).
    /// </summary>
    /// <remarks>
    /// Die Gerätekennung entsteht erst durch die Kopplung. Der Assistent fragt darum nach dem Code statt nach der
    /// Kennung und legt das Gerät mit dem Ergebnis an.
    /// </remarks>
    public interface ITerminalPairing
    {
        /// <summary>Der Weg, für den das gilt.</summary>
        string Key { get; }

        /// <summary>Koppelt das Gerät und liefert die Kennung, unter der es danach angesprochen wird.</summary>
        Task<string> PairAsync(int tenantId, string pairingCode, string displayName,
            CancellationToken cancellationToken = default);
    }
}
