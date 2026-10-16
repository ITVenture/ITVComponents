using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
// Alias, weil ITVComponents.WebCoreToolkit.Configuration ein NAMENSRAUM ist und den Typ verdeckt.
using WalleeConfiguration = Wallee.Client.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Impl
{
    /// <summary>
    /// Mit welchem wallee-Konto und in welchem Space ein Aufruf arbeitet: dem der Plattform oder dem eigenen
    /// eines Mandanten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ein eigener Zugang ist ein Space im EIGENEN wallee-Konto des Mandanten, mit einem Anwendungsbenutzer
    /// dieses Kontos. Die Space-Id steht in <c>TenantProviderAccess.ProviderAccountId</c>. Im ExternalService
    /// steht der Anwendungsbenutzer als <c>Basic</c> mit der Benutzer-Id als ClientId und seinem Schlüssel als
    /// Geheimnis.
    /// </para>
    /// <para>
    /// Kein <c>record</c>: dessen <c>ToString()</c> schriebe die Anmeldung aus.
    /// </para>
    /// </remarks>
    internal sealed class WalleeConnection
    {
        private WalleeConnection(long spaceId, Func<WalleeConfiguration> configuration, int? accessId, long[] paymentMethodConfigurations)
        {
            SpaceId = spaceId;
            // Erst beim ersten Aufruf bauen: das SDK prueft den Schluessel schon im Konstruktor, und eine Meldung soll
            // zuerst an dem scheitern, was an IHR falsch ist - nicht an einer Anmeldung, die sie vielleicht nie braucht.
            this.configuration = new Lazy<WalleeConfiguration>(configuration);
            AccessId = accessId;
            PaymentMethodConfigurations = paymentMethodConfigurations;
        }

        /// <summary>Der Space - wallee verlangt ihn bei jedem Aufruf.</summary>
        public long SpaceId { get; }

        /// <summary>Die Anmeldung des SDK.</summary>
        public WalleeConfiguration Configuration => configuration.Value;

        private readonly Lazy<WalleeConfiguration> configuration;

        /// <summary>Der eigene Zugang; leer bei der Plattform.</summary>
        public int? AccessId { get; }

        /// <summary>Ob es das Konto eines Mandanten ist.</summary>
        public bool IsOwnAccess => AccessId != null;

        /// <summary>Die erlaubten Zahlungsmittel-Konfigurationen; leer = alle des Space.</summary>
        public long[] PaymentMethodConfigurations { get; }

        /// <summary>
        /// Das Konto der Plattform. Der Space ist der des Mandanten, wenn einer genannt ist (Plattform-Konto mit
        /// einem Space je Mandant), sonst der aus den Einstellungen.
        /// </summary>
        public static WalleeConnection Platform(WalleeOptions options, string? accountId)
        {
            long space;
            if (!string.IsNullOrWhiteSpace(accountId) && long.TryParse(accountId, out var named) && named > 0)
            {
                space = named;
            }
            else if (options.SpaceId > 0)
            {
                space = options.SpaceId;
            }
            else
            {
                throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                    "Neither the tenant nor the 'WalleePayments' setting names a wallee space to work in.");
            }

            return new WalleeConnection(space, () => WalleeRuntime.Configure(options), null, options.PaymentMethodConfigurations ?? []);
        }

        /// <summary>Die Verbindung fuer einen Vorgang - die der Plattform oder die eigene des Mandanten.</summary>
        /// <param name="access">der gewaehlte Zugang</param>
        /// <param name="options">die Einstellungen der Plattform</param>
        /// <param name="platformAccountId">beim Plattform-Zugang: der Space des Mandanten, falls bekannt</param>
        /// <param name="credentials">der Zugangsdaten-Speicher, falls registriert</param>
        /// <param name="cancellationToken">bricht den Vorgang ab</param>
        /// <exception cref="TenantPaymentException">
        /// <c>OwnAccessUnavailable</c>, wenn ein eigener Zugang gilt, aber nicht verwendbar ist. Auf die Plattform
        /// wird dann NICHT ausgewichen.
        /// </exception>
        public static async Task<WalleeConnection> ForAsync(ProviderAccessSelection access, WalleeOptions options,
            string? platformAccountId, IProviderCredentialStore? credentials, CancellationToken cancellationToken)
        {
            if (!access.IsOwnAccess)
            {
                return Platform(options, platformAccountId);
            }

            if (credentials == null)
            {
                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    $"Tenant {access.TenantId} uses its own wallee account, but no IProviderCredentialStore is registered (AddFlatProviderCredentialStore / AddHierarchyProviderCredentialStore).");
            }

            if (!long.TryParse(access.ProviderAccountId, out var space) || space <= 0 || string.IsNullOrWhiteSpace(access.ConnectionName))
            {
                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    $"The own wallee access {access.AccessId} of tenant {access.TenantId} names no numeric space id ('{access.ProviderAccountId}').");
            }

            var credential = await credentials.GetCredentialAsync(access.TenantId, access.ConnectionName, cancellationToken);
            if (credential.AuthenticationType != ExternalServiceAuthenticationType.Basic
                || !long.TryParse(credential.ClientId, out var userId) || userId <= 0)
            {
                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    $"The own wallee access {access.AccessId} of tenant {access.TenantId} needs an application user as 'Basic' credential (user id as client id, its key as secret).");
            }

            var settings = access.GetSettings<WalleeOwnAccessSettings>();
            var secret = credential.Secret;
            var baseUrl = settings?.ApiBaseUrl;
            return new WalleeConnection(space, () =>
            {
                var configuration = new WalleeConfiguration(userId, secret);
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    configuration.BasePath = baseUrl;
                }

                return configuration;
            }, access.AccessId, settings?.PaymentMethodConfigurations ?? []);
        }

        /// <inheritdoc />
        public override string ToString()
            => IsOwnAccess ? $"wallee space {SpaceId} (own access {AccessId})" : $"wallee space {SpaceId} (platform)";
    }

    /// <summary>
    /// Die nicht-geheimen Angaben eines eigenen wallee-Zugangs (<c>TenantProviderAccess.SettingsJson</c>).
    /// </summary>
    public sealed class WalleeOwnAccessSettings
    {
        /// <summary>Eine abweichende API-Adresse; leer heisst die Vorgabe des SDK.</summary>
        public string? ApiBaseUrl { get; set; }

        /// <summary>Die erlaubten Zahlungsmittel-Konfigurationen des Space.</summary>
        public long[]? PaymentMethodConfigurations { get; set; }
    }
}
