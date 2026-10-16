using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Impl
{
    /// <summary>
    /// Was SumUp zum Einrichten des Kontos eines Mandanten beiträgt.
    /// </summary>
    /// <remarks>
    /// Keine Webhook-Adresse zum Eintragen: SumUp bekommt sie mit jedem Checkout (<c>return_url</c>).
    /// </remarks>
    public sealed class SumUpOwnAccessProvider : OwnAccessProviderBase
    {
        private const string MerchantField = "merchantCode";
        private const string ApiKeyField = "apiKey";

        private readonly SumUpApiClient api;
        private readonly IProviderCredentialStore? credentials;

        /// <summary>Initializes a new instance of the <see cref="SumUpOwnAccessProvider"/> class.</summary>
        public SumUpOwnAccessProvider(SumUpApiClient api, IProviderCredentialStore? credentials = null)
        {
            this.api = api;
            this.credentials = credentials;
        }

        /// <inheritdoc />
        public override string Key => SumUpAccess.Key;

        /// <inheritdoc />
        protected override IReadOnlyList<OwnAccessField> Fields { get; } =
        [
            new()
            {
                Name = MerchantField, Label = "Händlercode", Kind = SettingKind.Text, Required = true,
                HelpText = "Im SumUp-Dashboard unter Profil, beginnt meist mit M."
            },
            new()
            {
                Name = ApiKeyField, Label = "API-Schlüssel", Kind = SettingKind.Text, Required = true, Secret = true,
                HelpText = "Im SumUp-Dashboard unter Entwickler → API-Schlüssel (sup_sk_…). Am besten ein eingeschränkter Schlüssel."
            }
        ];

        /// <inheritdoc />
        protected override OwnAccessDraft Build(IReadOnlyDictionary<string, string?> values)
            => new() { ProviderAccountId = Required(values, MerchantField), Secrets = BuildSecrets(values) };

        /// <inheritdoc />
        protected override IReadOnlyList<OwnAccessSecret> BuildSecrets(IReadOnlyDictionary<string, string?> values)
            => [new(string.Empty, ExternalServiceAuthenticationType.BearerToken, null, Required(values, ApiKeyField))];

        /// <inheritdoc />
        /// <remarks>Die Geräteliste: harmlos, und sie braucht genau das Recht, das das Kassieren auch braucht.</remarks>
        public override async Task<OwnAccessCheck> VerifyAsync(ProviderAccessSelection access,
            CancellationToken cancellationToken = default)
        {
            if (credentials == null)
            {
                return new OwnAccessCheck(false, "No IProviderCredentialStore is registered.");
            }

            var connection = await SumUpAccess.ConnectAsync(access, credentials, cancellationToken);
            try
            {
                await api.SendAsync<object>(HttpMethod.Get, SumUpAccess.Readers(connection.MerchantCode), null,
                    connection.Credential, cancellationToken);
                return new OwnAccessCheck(true, $"SumUp accepted the key for merchant {connection.MerchantCode}.");
            }
            catch (SumUpApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"SumUp rejected the own access {access.AccessId} (merchant {connection.MerchantCode}) of tenant {access.TenantId}: {ex.OutlineException()}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return new OwnAccessCheck(false, ex.Message);
            }
        }
    }

    /// <summary>Die Kopplung eines Solo über den Code, den er anzeigt.</summary>
    public sealed class SumUpTerminalPairing<TContext> : ITerminalPairing
        where TContext : DbContext, IPaymentsContext
    {
        private readonly SumUpTerminalPaymentService<TContext> terminals;

        /// <summary>Initializes a new instance of the <see cref="SumUpTerminalPairing{TContext}"/> class.</summary>
        public SumUpTerminalPairing(SumUpTerminalPaymentService<TContext> terminals)
        {
            this.terminals = terminals;
        }

        /// <inheritdoc />
        public string Key => SumUpAccess.Key;

        /// <inheritdoc />
        public async Task<string> PairAsync(int tenantId, string pairingCode, string displayName,
            CancellationToken cancellationToken = default)
            => (await terminals.PairReaderAsync(tenantId, pairingCode, displayName, cancellationToken)).ProviderTerminalId;
    }
}
