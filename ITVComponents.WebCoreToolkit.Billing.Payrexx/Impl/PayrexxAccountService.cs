using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Models;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Das Konto eines Mandanten bei Payrexx: anlegen, Stand lesen. Bei Payrexx ist das ein eigener
    /// <b>Händler</b> mit eigener Instanz unter der Plattform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Der grosse Unterschied zu Stripe:</b> es gibt kein vom Anbieter gehostetes Onboarding-Formular,
    /// durch das man den Mandanten schickt. Der Händler wird per API angelegt und bekommt eine Mail; die
    /// Identitätsprüfung läuft danach in seiner eigenen Payrexx-Oberfläche. <see cref="StartOnboardingAsync"/>
    /// legt darum an und gibt die Adresse dieser Oberfläche zurück — nicht die eines Formulars, das wir
    /// steuern.
    /// </para>
    /// <para>
    /// <b>Die Unterschrift des Mandanten holen wir nicht ein.</b> Das tut Payrexx beim ersten Anmelden
    /// selbst. Wer hier eine Zustimmung erwartet, wie Stripe sie im Connect-Onboarding einsammelt, sucht
    /// vergeblich.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Plattform-Konto gelaufen.</b> Pfade und Feldnamen stammen aus der
    /// Service-API-Dokumentation; was dort offen bleibt, steht unten als Frage und nicht als Vermutung.
    /// </para>
    /// </remarks>
    public class PayrexxAccountService<TContext> : ITenantPaymentAccountService
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly PayrexxServiceApiClient serviceApi;
        private readonly PaymentsRuntime runtime;

        /// <summary>Initializes a new instance of the <see cref="PayrexxAccountService{TContext}"/> class.</summary>
        public PayrexxAccountService(IDbContextFactory<TContext> dbFactory, PayrexxServiceApiClient serviceApi,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates)
        {
            this.dbFactory = dbFactory;
            this.serviceApi = serviceApi;
            runtime = new PaymentsRuntime(settings, featureGates.FirstOrDefault());
        }

        /// <inheritdoc />
        public async Task<TenantPaymentAccountStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken);
            return account == null ? null : ToStatus(account);
        }

        /// <inheritdoc />
        public async Task<string> StartOnboardingAsync(int tenantId, string returnUrl, string refreshUrl,
            string? email = null, string? country = null, CancellationToken cancellationToken = default)
        {
            runtime.EnsureEnabled();
            await runtime.EnsureFeatureAsync(tenantId, cancellationToken);

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken);
            if (account != null && !string.IsNullOrEmpty(account.ProviderAccountId))
            {
                // Schon angelegt. Ein zweiter Aufruf darf KEINEN zweiten Haendler erzeugen - der waere
                // fachlich ein zweiter Laden, mit eigener Instanz und eigener Pruefung.
                return InstanceUrl(account.ProviderAccountId);
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                // Pflichtfeld der API, und keines, das sich erraten liesse: es wird die Adresse des
                // Verwalter-Kontos beim Anbieter.
                throw new TenantPaymentException(PaymentErrorCodes.InvalidAmount,
                    $"Tenant {tenantId} cannot be onboarded to Payrexx without an e-mail address — it becomes the administrator account.");
            }

            var subdomain = BuildSubdomain(tenantId, email);
            PayrexxMerchant merchant;
            try
            {
                merchant = await serviceApi.PostAsync<PayrexxMerchant>("v2.3", "merchant", new Dictionary<string, object?>
                {
                    ["subdomain"] = subdomain,
                    ["email"] = email,
                    ["language"] = "de",
                    // Marktplatz-Vorgabe: eingeschraenkt gefuehrt. Ein Haendler, der die volle Oberflaeche
                    // bekommt, kann Dinge aendern, fuer die die Plattform geradesteht.
                    ["restricted"] = true,
                    // Unser Weg zurueck. Die Kennung des Anbieters merken wir uns zwar unten, aber diese
                    // Richtung braucht es, wenn jemand beim Anbieter auf einen Haendler schaut und wissen
                    // will, zu welchem Mandanten er gehoert.
                    ["reference"] = $"tenant:{tenantId}"
                }, cancellationToken) ?? throw new PayrexxApiException("Payrexx accepted the merchant but returned nothing.");
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not create a Payrexx merchant for tenant {tenantId} (subdomain '{subdomain}'): {ex.OutlineException()}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }

            // Das Passwort aus der Antwort wird NICHT angefasst - weder protokolliert noch gespeichert.
            // Es geht den Haendler an, und Payrexx schickt es ihm per Mail.
            if (merchant.Account is { Created: true })
            {
                LogEnvironment.LogEvent(
                    $"Payrexx created merchant {merchant.Id} ('{merchant.Subdomain}') for tenant {tenantId} together with a fresh administrator account; the credentials went to the merchant by mail.",
                    LogSeverity.Report, PayrexxApiClient.LogContext);
            }

            account ??= new TenantPaymentAccount { TenantId = tenantId, Created = DateTime.UtcNow };
            // Die INSTANZ ist die Kennung, mit der spaeter gearbeitet wird - nicht die laufende Nummer.
            account.ProviderAccountId = merchant.Subdomain ?? merchant.Id.ToString();
            // Die Entitaet heisst DashboardType, das Status-Objekt AccountType - beide meinen dasselbe.
            // Bei Payrexx traegt es die Fuehrungsart des Haendlers.
            account.DashboardType = merchant.Restricted ? "restricted" : "full";
            account.Country = country;
            account.DetailsSubmitted = false;
            // Fail-closed: bis die Pruefung durch ist, verkauft hier niemand. RefreshAsync hebt das an,
            // sobald der Anbieter es bestaetigt.
            account.ChargesEnabled = false;
            account.PayoutsEnabled = false;
            account.Updated = DateTime.UtcNow;
            if (account.TenantPaymentAccountId == 0)
            {
                db.TenantPaymentAccounts.Add(account);
            }

            await db.SaveChangesAsync(cancellationToken);
            return InstanceUrl(account.ProviderAccountId);
        }

        /// <inheritdoc />
        public async Task<TenantPaymentAccountStatus> RefreshAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken)
                          ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                              $"Tenant {tenantId} has no Payrexx merchant yet.");

            try
            {
                // ACHTUNG: die Pruefung liegt unter v2.0, der Haendler unter v2.3. Innerhalb DERSELBEN
                // Service-API. Wer die Version aus der Zeile darueber kopiert, bekommt einen 404, der wie
                // ein fehlender Haendler aussieht.
                var verification = await serviceApi.GetAsync<PayrexxVerification>("v2.0",
                    $"merchant/{account.ProviderAccountId}/verification", cancellationToken);

                var approved = string.Equals(verification?.Status, "approved", StringComparison.OrdinalIgnoreCase);
                account.DetailsSubmitted = string.Equals(verification?.VerificationDocument, "PROVIDED", StringComparison.OrdinalIgnoreCase);
                account.ChargesEnabled = approved;
                // OFFENE FRAGE fuer das Testkonto: die Dokumentation trennt Einnahmen und Auszahlungen
                // nicht wie Stripe. Bis das geklaert ist, gilt die Pruefung fuer beides - das ist die
                // vorsichtige Richtung, denn RequirePayoutsEnabled sperrt damit hoechstens zu frueh statt
                // zu spaet.
                account.PayoutsEnabled = approved;
                account.DisabledReason = approved ? null : verification?.Status;
                account.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read the verification state of Payrexx merchant '{account.ProviderAccountId}' (tenant {tenantId}): {ex.OutlineException()}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }

            return ToStatus(account);
        }

        /// <inheritdoc />
        public Task<string?> CreateDashboardLinkAsync(int tenantId, CancellationToken cancellationToken = default)
            // Es gibt keinen Einmal-Link wie bei Stripe: der Haendler hat bei Payrexx ein eigenes Konto mit
            // eigener Anmeldung. Die Adresse seiner Instanz ist alles, was wir anbieten koennen - und ein
            // Link, der zur Anmeldemaske fuehrt, ist ehrlicher als gar keiner.
            => GetStatusAsync(tenantId, cancellationToken)
                .ContinueWith(t => string.IsNullOrEmpty(t.Result?.ProviderAccountId)
                    ? null
                    : InstanceUrl(t.Result!.ProviderAccountId), cancellationToken);

        /// <summary>
        /// Der Name der Instanz. Muss ueber die ganze Plattform eindeutig sein und darf nur enthalten, was
        /// in einen Hostnamen passt.
        /// </summary>
        /// <remarks>
        /// Die Mandanten-Nummer ist bewusst dabei: ohne sie kollidieren zwei Laeden mit aehnlichem Namen,
        /// und die Kollision faellt erst beim Anlegen auf - also dann, wenn der Mandant schon wartet.
        /// </remarks>
        private static string BuildSubdomain(int tenantId, string email)
        {
            var stem = new string(email.Split('@')[0].ToLowerInvariant()
                .Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray()).Trim('-');
            if (stem.Length > 24)
            {
                stem = stem[..24];
            }

            return string.IsNullOrEmpty(stem) ? $"tenant-{tenantId}" : $"{stem}-{tenantId}";
        }

        private static string InstanceUrl(string instance)
            => instance.Contains('.') ? $"https://{instance}" : $"https://{instance}.payrexx.com";

        private static TenantPaymentAccountStatus ToStatus(TenantPaymentAccount account) => new()
        {
            TenantId = account.TenantId,
            ProviderAccountId = account.ProviderAccountId ?? string.Empty,
            AccountType = account.DashboardType ?? string.Empty,
            Country = account.Country,
            DefaultCurrency = account.DefaultCurrency,
            ChargesEnabled = account.ChargesEnabled,
            PayoutsEnabled = account.PayoutsEnabled,
            DetailsSubmitted = account.DetailsSubmitted,
            Disconnected = account.Disconnected,
            DisabledReason = account.DisabledReason,
            CanSell = account.ChargesEnabled && !account.Disconnected,
            Updated = account.Updated
        };
    }
}
