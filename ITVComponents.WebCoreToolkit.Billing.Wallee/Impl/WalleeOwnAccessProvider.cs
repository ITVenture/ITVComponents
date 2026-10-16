using System.Globalization;
using System.Text.Json;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using Wallee.Client;
using Wallee.Model;
using Wallee.Service;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Impl
{
    /// <summary>
    /// Was wallee zum Einrichten eines eigenen Kontos eines Mandanten beiträgt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anders als bei Payrexx lassen sich die Meldungen hier von aussen einrichten: mit dem Anwendungsbenutzer des
    /// Mandanten werden in SEINEM Space eine Webhook-URL und die zwei Listener angelegt (Transaction, Refund).
    /// <b>"payload signing and state" setzt das SDK nicht</b> - es laesst sich nur lesen. Ist es danach aus, sagt
    /// das Ergebnis, dass der Mandant es im Portal einschalten muss: ohne Signatur verwirft der Endpunkt jede Meldung.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Konto gelaufen.</b> Die Kennungen der Entitäten (Transaction
    /// 1472041829003, Refund 1472041839405) stammen aus der wallee-Doku.
    /// </para>
    /// </remarks>
    public sealed class WalleeOwnAccessProvider : OwnAccessProviderBase
    {
        private const string SpaceField = "spaceId";
        private const string UserField = "applicationUserId";
        private const string KeyField = "applicationUserKey";
        private const string ApiBaseUrlField = "apiBaseUrl";

        /// <summary>Die Entität "Transaction" bei wallee.</summary>
        private const long TransactionEntity = 1472041829003;

        /// <summary>Die Entität "Refund" bei wallee.</summary>
        private const long RefundEntity = 1472041839405;

        private readonly IGlobalSettings<WalleeOptions> settings;
        private readonly IProviderCredentialStore? credentials;

        /// <summary>Initializes a new instance of the <see cref="WalleeOwnAccessProvider"/> class.</summary>
        public WalleeOwnAccessProvider(IGlobalSettings<WalleeOptions> settings, IProviderCredentialStore? credentials = null)
        {
            this.settings = settings;
            this.credentials = credentials;
        }

        /// <inheritdoc />
        public override string Key => "wallee";

        /// <inheritdoc />
        public override string? WebhookPath => "/billing/wallee/webhook";

        /// <inheritdoc />
        public override bool CanSetUpWebhooks => true;

        /// <inheritdoc />
        protected override IReadOnlyList<OwnAccessField> Fields { get; } =
        [
            new()
            {
                Name = SpaceField, Label = "Space-Id", Kind = SettingKind.Number, Required = true,
                HelpText = "Die Nummer des Space in Ihrem wallee-Konto, in dem kassiert wird."
            },
            new()
            {
                Name = UserField, Label = "Anwendungsbenutzer (Id)", Kind = SettingKind.Number, Required = true,
                PartOfCredential = true,
                HelpText = "Ein Anwendungsbenutzer Ihres Kontos mit Rechten auf Transaktionen, Erstattungen, Terminals und Webhooks."
            },
            new()
            {
                Name = KeyField, Label = "Schlüssel des Anwendungsbenutzers", Kind = SettingKind.Text, Required = true,
                Secret = true,
                HelpText = "Wird nur beim Anlegen des Anwendungsbenutzers angezeigt."
            },
            new()
            {
                Name = ApiBaseUrlField, Label = "API-Adresse", Kind = SettingKind.Text,
                HelpText = "Leer: die Vorgabe von wallee."
            }
        ];

        /// <inheritdoc />
        protected override OwnAccessDraft Build(IReadOnlyDictionary<string, string?> values)
        {
            var space = Number(values, SpaceField);
            var baseUrl = Value(values, ApiBaseUrlField);
            return new OwnAccessDraft
            {
                ProviderAccountId = space.ToString(CultureInfo.InvariantCulture),
                SettingsJson = baseUrl == null ? null : JsonSerializer.Serialize(new WalleeOwnAccessSettings { ApiBaseUrl = baseUrl }),
                Secrets = BuildSecrets(values)
            };
        }

        /// <inheritdoc />
        protected override IReadOnlyList<OwnAccessSecret> BuildSecrets(IReadOnlyDictionary<string, string?> values)
            => [new(string.Empty, ExternalServiceAuthenticationType.Basic,
                Number(values, UserField).ToString(CultureInfo.InvariantCulture), Required(values, KeyField))];

        /// <inheritdoc />
        public override async Task<OwnAccessCheck> VerifyAsync(ProviderAccessSelection access,
            CancellationToken cancellationToken = default)
        {
            var connection = await WalleeConnection.ForAsync(access, settings.Value, null, credentials, cancellationToken);
            try
            {
                var space = await Task.Run(() => new SpacesService(connection.Configuration).GetSpacesId(connection.SpaceId),
                    cancellationToken).ConfigureAwait(false);
                return new OwnAccessCheck(true, $"wallee accepted the application user for space {connection.SpaceId} ('{space.Name}').");
            }
            catch (Exception ex) when (ex is ApiException or ArgumentException)
            {
                // ArgumentException: das SDK lehnt einen falsch kodierten Schluessel schon beim Anmelden ab.
                LogEnvironment.LogEvent(
                    $"wallee rejected the own access {access.AccessId} (space {connection.SpaceId}) of tenant {access.TenantId}: {ex.OutlineException()}",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
                return new OwnAccessCheck(false, ex.Message);
            }
        }

        /// <inheritdoc />
        public override async Task<OwnAccessCheck> SetUpWebhooksAsync(ProviderAccessSelection access, string webhookUrl,
            CancellationToken cancellationToken = default)
        {
            var connection = await WalleeConnection.ForAsync(access, settings.Value, null, credentials, cancellationToken);
            try
            {
                var url = await Task.Run(() => new WebhookURLsService(connection.Configuration).PostWebhooksUrls(connection.SpaceId,
                    new WebhookUrlCreate { Name = "ITVComponents payments", Url = webhookUrl, State = CreationEntityState.ACTIVE }),
                    cancellationToken).ConfigureAwait(false);

                var listeners = new WebhookListenersService(connection.Configuration);
                var transactions = await Task.Run(() => listeners.PostWebhooksListeners(connection.SpaceId, Listener("Transactions", TransactionEntity,
                        ["AUTHORIZED", "COMPLETED", "FULFILL", "FAILED", "DECLINE", "VOIDED"], url.Id)),
                    cancellationToken).ConfigureAwait(false);
                var refunds = await Task.Run(() => listeners.PostWebhooksListeners(connection.SpaceId, Listener("Refunds", RefundEntity,
                        ["SUCCESSFUL", "FAILED"], url.Id)),
                    cancellationToken).ConfigureAwait(false);

                // "payload signing and state" laesst sich mit dem SDK weder beim Anlegen noch danach setzen - nur
                // lesen. Ohne die Signatur lehnt der Endpunkt eines eigenen Zugangs jede Meldung ab; das muss der
                // Mandant also wissen, und zwar jetzt und nicht erst, wenn die erste Zahlung nicht gebucht wird.
                if (!transactions.EnablePayloadSignatureAndState || !refunds.EnablePayloadSignatureAndState)
                {
                    LogEnvironment.LogEvent(
                        $"The wallee listeners {transactions.Id} and {refunds.Id} of own access {access.AccessId} were created without payload signing; notifications are rejected until it is switched on in the wallee portal.",
                        LogSeverity.Warning, WalleeRuntime.LogContext);
                    return new OwnAccessCheck(false,
                        $"Webhook URL {url.Id} and the listeners {transactions.Id} (Transaction) and {refunds.Id} (Refund) were created in space {connection.SpaceId}, but without payload signing. Switch on \"payload signing and state\" at both listeners in the wallee portal - until then every notification is rejected.");
                }

                return new OwnAccessCheck(true, $"Webhook URL {url.Id} and two signed listeners were created in space {connection.SpaceId}.");
            }
            catch (Exception ex) when (ex is ApiException or ArgumentException)
            {
                LogEnvironment.LogEvent(
                    $"The notifications of own access {access.AccessId} could not be set up in wallee space {connection.SpaceId}: {ex.OutlineException()}",
                    LogSeverity.Error, WalleeRuntime.LogContext);
                return new OwnAccessCheck(false, ex.Message);
            }
        }

        private static WebhookListenerCreate Listener(string name, long entity, List<string> states, long urlId)
            => new()
            {
                Name = $"ITVComponents payments: {name}",
                Entity = entity,
                EntityStates = states,
                Url = urlId,
                NotifyEveryChange = false,
                State = CreationEntityState.ACTIVE
            };

        private static long Number(IReadOnlyDictionary<string, string?> values, string name)
            => long.TryParse(Required(values, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0
                ? number
                : throw new TenantPaymentException(PaymentErrorCodes.InvalidSettings, $"'{name}' must be a positive number.");
    }
}
