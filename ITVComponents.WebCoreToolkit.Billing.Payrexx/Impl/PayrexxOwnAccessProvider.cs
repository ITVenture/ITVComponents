using System.Text.Json;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Was Payrexx zum Einrichten einer eigenen Instanz eines Mandanten beiträgt.
    /// </summary>
    /// <remarks>
    /// Die Meldungen richtet der Mandant in SEINEM Portal ein (Webhooks, Adresse samt Signierschlüssel). Die
    /// Händler-API bietet dafür keinen Aufruf. Die Verwaltung zeigt ihm die Adresse, die er dort einträgt.
    /// </remarks>
    public sealed class PayrexxOwnAccessProvider : OwnAccessProviderBase
    {
        private const string InstanceField = "instance";
        private const string ApiSecretField = "apiSecret";
        private const string SigningKeyField = "webhookSigningKey";
        private const string ApiBaseUrlField = "apiBaseUrl";

        private readonly PayrexxApiClient api;
        private readonly IProviderCredentialStore? credentials;

        /// <summary>Initializes a new instance of the <see cref="PayrexxOwnAccessProvider"/> class.</summary>
        public PayrexxOwnAccessProvider(PayrexxApiClient api, IProviderCredentialStore? credentials = null)
        {
            this.api = api;
            this.credentials = credentials;
        }

        /// <inheritdoc />
        public override string Key => "payrexx";

        /// <inheritdoc />
        public override string? WebhookPath => "/billing/payrexx/webhook";

        /// <inheritdoc />
        protected override IReadOnlyList<OwnAccessField> Fields { get; } =
        [
            new()
            {
                Name = InstanceField, Label = "Payrexx-Instanz", Kind = SettingKind.Text, Required = true,
                HelpText = "Der erste Teil der Adresse Ihres Payrexx-Kontos: https://<instanz>.payrexx.com"
            },
            new()
            {
                Name = ApiSecretField, Label = "API-Secret", Kind = SettingKind.Text, Required = true, Secret = true,
                HelpText = "Im Payrexx-Portal unter Integrationen → API."
            },
            new()
            {
                Name = SigningKeyField, Label = "Signierschlüssel der Webhooks", Kind = SettingKind.Text, Required = true,
                Secret = true,
                HelpText = "Im Payrexx-Portal unter Webhooks. Ohne ihn werden Zahlungsmeldungen abgelehnt."
            },
            new()
            {
                Name = ApiBaseUrlField, Label = "API-Adresse", Kind = SettingKind.Text,
                HelpText = $"Nur bei einem White-Label-Anbieter. Leer: {PayrexxConnection.DefaultApiBaseUrl}"
            }
        ];

        /// <inheritdoc />
        protected override OwnAccessDraft Build(IReadOnlyDictionary<string, string?> values)
        {
            var baseUrl = Value(values, ApiBaseUrlField);
            return new OwnAccessDraft
            {
                ProviderAccountId = Required(values, InstanceField),
                SettingsJson = baseUrl == null ? null : JsonSerializer.Serialize(new PayrexxOwnAccessSettings { ApiBaseUrl = baseUrl }),
                Secrets = BuildSecrets(values)
            };
        }

        /// <inheritdoc />
        protected override IReadOnlyList<OwnAccessSecret> BuildSecrets(IReadOnlyDictionary<string, string?> values)
        =>
        [
            new(string.Empty, ExternalServiceAuthenticationType.ApiKey, "X-API-KEY", Required(values, ApiSecretField)),
            new("-webhook", ExternalServiceAuthenticationType.BearerToken, null, Required(values, SigningKeyField))
        ];

        /// <inheritdoc />
        /// <remarks>
        /// <c>SignatureCheck</c> ist der Aufruf, den Payrexx zum Prüfen der Anmeldung vorsieht: er tut nichts und
        /// antwortet nur mit Erfolg, wenn Instanz und Schlüssel zusammenpassen.
        /// </remarks>
        public override async Task<OwnAccessCheck> VerifyAsync(ProviderAccessSelection access,
            CancellationToken cancellationToken = default)
        {
            var connection = await PayrexxConnection.ForAsync(access, api.Options, credentials, cancellationToken);
            try
            {
                await api.GetAsync<JsonElement>(connection, "SignatureCheck/", cancellationToken);
                return new OwnAccessCheck(true, $"Payrexx accepted the key for instance '{connection.Instance}'.");
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Payrexx rejected the own access {access.AccessId} (instance {connection.Instance}) of tenant {access.TenantId}: {ex.OutlineException()}",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
                return new OwnAccessCheck(false, ex.Message);
            }
        }
    }
}
