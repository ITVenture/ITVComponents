using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions
{
    /// <summary>
    /// Liest und schreibt die Geheimnisse eines eigenen Anbieter-Zugangs - fuer einen ausdruecklich genannten
    /// Mandanten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nach Mandanten-Id und ausdruecklich NICHT ueber den aktuellen Bereich</b> - dieselbe Regel wie
    /// beim <see cref="IPaymentFeatureGate"/>. Gefragt wird aus Webhooks, aus dem Beleg-Sweeper und aus
    /// anonymen Shop-Aufrufen; ein Bereich ist dort entweder keiner oder der falsche. Der vorhandene
    /// <c>IOAuthTokenService</c> taugt deshalb nicht: er liest den Mandanten aus dem Bereich.
    /// </para>
    /// <para>
    /// Ein Vertrag und keine Umsetzung, weil die Geheimnisse in den ExternalServices der Sicherheits-Datenbank
    /// liegen und das Billing-Paket deren Modell nicht kennt. Die Umsetzung sitzt in
    /// <c>EntityFramework.Billing.TenantSecurity</c>.
    /// </para>
    /// </remarks>
    public interface IProviderCredentialStore
    {
        /// <summary>
        /// Die verwendbaren Zugangsdaten unter <paramref name="connectionName"/> fuer diesen Mandanten. Bei
        /// einer OAuth-Anmeldung ist das ein gueltiges Zugriffstoken; ein abgelaufenes wird vorher erneuert.
        /// </summary>
        /// <exception cref="ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments.TenantPaymentException">
        /// mit <c>OwnAccessUnavailable</c>, wenn nichts hinterlegt ist oder die Anmeldung erneuert werden muss.
        /// </exception>
        Task<ProviderCredential> GetCredentialAsync(int tenantId, string connectionName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Hinterlegt ein festes Geheimnis (API-Schluessel, Bearer-Token, Benutzer + Schluessel) als Dienst des
        /// Mandanten - verschluesselt mit dessen Schluessel. Ein vorhandener Dienst gleichen Namens wird
        /// ueberschrieben.
        /// </summary>
        /// <param name="tenantId">der Mandant, dem der Zugang gehoert</param>
        /// <param name="connectionName">der Name des Dienstes, siehe <c>TenantProviderAccess.ConnectionName</c></param>
        /// <param name="authenticationType">
        /// <see cref="ExternalServiceAuthenticationType.ApiKey"/>, <see cref="ExternalServiceAuthenticationType.BearerToken"/>
        /// oder <see cref="ExternalServiceAuthenticationType.Basic"/>. Eine OAuth-Anmeldung entsteht ueber den
        /// Anmeldeweg des Anbieters, nicht hier.
        /// </param>
        /// <param name="clientId">bei ApiKey der Name des Kopfes, bei Basic der Benutzer</param>
        /// <param name="secret">das Geheimnis selbst</param>
        /// <param name="cancellationToken">bricht den Vorgang ab</param>
        Task SaveSecretAsync(int tenantId, string connectionName, ExternalServiceAuthenticationType authenticationType,
            string? clientId, string secret, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Was ein Anbieter-Client braucht, um sich anzumelden.
    /// </summary>
    /// <remarks>
    /// Eine Klasse und kein <c>record</c>: ein record schreibt in <c>ToString()</c> alle Felder aus, und ein
    /// Zugangsobjekt in einer Protokollzeile waere das Geheimnis im Klartext.
    /// </remarks>
    public sealed class ProviderCredential
    {
        /// <summary>Initializes a new instance of the <see cref="ProviderCredential"/> class.</summary>
        public ProviderCredential(ExternalServiceAuthenticationType authenticationType, string? clientId, string secret)
        {
            AuthenticationType = authenticationType;
            ClientId = clientId;
            Secret = secret;
        }

        /// <summary>Wie das Geheimnis zu verwenden ist.</summary>
        public ExternalServiceAuthenticationType AuthenticationType { get; }

        /// <summary>Bei ApiKey der Name des Kopfes, bei Basic der Benutzer; sonst leer.</summary>
        public string? ClientId { get; }

        /// <summary>Das Geheimnis - bei einer OAuth-Anmeldung das gueltige Zugriffstoken.</summary>
        public string Secret { get; }

        /// <inheritdoc />
        public override string ToString() => $"{AuthenticationType} credential (secret withheld)";
    }
}
