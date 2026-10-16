using ITVComponents.WebCoreToolkit.Billing.Payrexx.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Mit welcher Payrexx-Instanz ein Aufruf redet: der der Plattform oder der eigenen eines Mandanten.
    /// </summary>
    /// <remarks>
    /// Eine Klasse und kein <c>record</c>: ein record schriebe in <c>ToString()</c> das Geheimnis aus.
    /// </remarks>
    public sealed class PayrexxConnection
    {
        /// <summary>Wo die Haendler-API eines eigenen Zugangs liegt, wenn er nichts anderes sagt.</summary>
        public const string DefaultApiBaseUrl = "https://api.payrexx.com/v1.0/";

        private PayrexxConnection(string instance, string apiSecret, string apiBaseUrl, int? accessId,
            string[] paymentMeans)
        {
            Instance = instance;
            ApiSecret = apiSecret;
            ApiBaseUrl = apiBaseUrl;
            AccessId = accessId;
            PaymentMeans = paymentMeans;
        }

        /// <summary>Der Instanzname - geht als Abfrageparameter mit.</summary>
        public string Instance { get; }

        /// <summary>Das API-Geheimnis - geht als <c>X-API-KEY</c> mit.</summary>
        public string ApiSecret { get; }

        /// <summary>Die Basis der Haendler-API samt Version.</summary>
        public string ApiBaseUrl { get; }

        /// <summary>Der eigene Zugang; leer bei der Plattform.</summary>
        public int? AccessId { get; }

        /// <summary>Ob es die Instanz eines Mandanten ist.</summary>
        public bool IsOwnAccess => AccessId != null;

        /// <summary>Die Zahlungsmittel in der gewuenschten Reihenfolge; leer = was die Instanz anbietet.</summary>
        public string[] PaymentMeans { get; }

        /// <summary>Die Instanz der Plattform aus dem GlobalSetting.</summary>
        public static PayrexxConnection Platform(PayrexxOptions options)
            => new(options.Instance, options.ApiSecret, options.ApiBaseUrl, null, options.PaymentMeans ?? []);

        /// <summary>
        /// Die Verbindung fuer einen Vorgang: die der Plattform, oder die eigene Instanz des Mandanten mit
        /// dessen Geheimnis.
        /// </summary>
        /// <exception cref="TenantPaymentException">
        /// <c>OwnAccessUnavailable</c>, wenn ein eigener Zugang gilt, aber kein Zugangsdaten-Speicher registriert
        /// ist oder dort nichts steht. Auf die Plattform wird dann NICHT ausgewichen.
        /// </exception>
        public static async Task<PayrexxConnection> ForAsync(ProviderAccessSelection access, PayrexxOptions options,
            IProviderCredentialStore? credentials, CancellationToken cancellationToken)
        {
            if (!access.IsOwnAccess)
            {
                return Platform(options);
            }

            if (credentials == null)
            {
                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    $"Tenant {access.TenantId} uses its own Payrexx instance, but no IProviderCredentialStore is registered (AddFlatProviderCredentialStore / AddHierarchyProviderCredentialStore).");
            }

            if (string.IsNullOrWhiteSpace(access.ProviderAccountId) || string.IsNullOrWhiteSpace(access.ConnectionName))
            {
                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    $"The own Payrexx access {access.AccessId} of tenant {access.TenantId} names no instance.");
            }

            var credential = await credentials.GetCredentialAsync(access.TenantId, access.ConnectionName, cancellationToken);
            var settings = access.GetSettings<PayrexxOwnAccessSettings>();
            return new PayrexxConnection(access.ProviderAccountId, credential.Secret,
                string.IsNullOrWhiteSpace(settings?.ApiBaseUrl) ? DefaultApiBaseUrl : settings.ApiBaseUrl,
                access.AccessId, settings?.PaymentMeans ?? []);
        }

        /// <summary>
        /// Der Name des Dienstes, der den Signierschluessel der Webhooks eines eigenen Zugangs haelt.
        /// </summary>
        /// <remarks>
        /// Ein zweiter Dienst neben dem API-Geheimnis, weil Payrexx die Meldungen mit einem EIGENEN Schluessel
        /// signiert, der im Portal unter "Webhooks" entsteht.
        /// </remarks>
        public static string WebhookConnectionName(string connectionName) => $"{connectionName}-webhook";

        /// <inheritdoc />
        public override string ToString()
            => IsOwnAccess ? $"Payrexx instance {Instance} (own access {AccessId})" : $"Payrexx instance {Instance} (platform)";
    }

    /// <summary>
    /// Die nicht-geheimen Angaben eines eigenen Payrexx-Zugangs (<c>TenantProviderAccess.SettingsJson</c>).
    /// </summary>
    public sealed class PayrexxOwnAccessSettings
    {
        /// <summary>Die Haendler-API, falls nicht <c>api.payrexx.com</c> - etwa bei einem White-Label-Anbieter.</summary>
        public string? ApiBaseUrl { get; set; }

        /// <summary>Die Zahlungsmittel in der gewuenschten Reihenfolge (TWINT zuerst spart Gebuehren).</summary>
        public string[]? PaymentMeans { get; set; }
    }
}
