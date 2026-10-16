using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Anlegen, Prüfen, Erneuern und Abschalten eigener Zugänge der Mandanten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ein neues Konto ist eine neue Zeile mit einem neuen Dienstnamen.</b> Die Geheimnisse liegen unter
    /// <c>payments-{anbieter}-{zufall}</c>. Ein fester Name je Anbieter hätte beim Kontowechsel die Geheimnisse des
    /// alten Kontos überschrieben, und dessen Verkäufe liessen sich danach nicht mehr erstatten.
    /// </para>
    /// <para>
    /// <b>Erneuern ist etwas anderes als Wechseln:</b> ein neuer Schlüssel für DASSELBE Konto ersetzt die
    /// Geheimnisse der bestehenden Zeile. Dort ist das richtig, weil der alte Schlüssel ohnehin nicht mehr gilt.
    /// </para>
    /// </remarks>
    public class ProviderAccessAdministration<TContext> : IProviderAccessAdministration
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IProviderCredentialStore? credentials;
        private readonly Dictionary<string, IOwnAccessProvider> providers;

        /// <summary>Initializes a new instance of the <see cref="ProviderAccessAdministration{TContext}"/> class.</summary>
        public ProviderAccessAdministration(IDbContextFactory<TContext> dbFactory, IEnumerable<IOwnAccessProvider> providers,
            IProviderCredentialStore? credentials = null)
        {
            this.dbFactory = dbFactory;
            this.credentials = credentials;
            this.providers = new Dictionary<string, IOwnAccessProvider>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in providers)
            {
                this.providers.TryAdd(provider.Key, provider);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetProviders() => [.. providers.Keys.OrderBy(k => k)];

        /// <inheritdoc />
        public IReadOnlyList<OwnAccessField> DescribeFields(string provider) => Resolve(provider).DescribeFields();

        /// <inheritdoc />
        public async Task<IReadOnlyList<OwnAccessInfo>> GetAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.TenantProviderAccesses.AsNoTracking()
                .Where(a => a.TenantId == tenantId)
                .OrderBy(a => a.Provider).ThenBy(a => a.Disabled).ThenByDescending(a => a.Created)
                .ToListAsync(cancellationToken);
            return [.. rows.Select(Describe)];
        }

        /// <inheritdoc />
        public async Task<int> CreateAsync(int tenantId, string provider, string displayName,
            IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
        {
            var owner = Resolve(provider);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new TenantPaymentException(PaymentErrorCodes.InvalidSettings, "An own access needs a name.");
            }

            var store = RequireStore();
            var draft = owner.Compose(values);
            var key = ProviderAccessRouter<TContext>.Normalize(owner.Key);
            var connectionName = $"payments-{key}-{Guid.NewGuid():N}";

            // Erst die Geheimnisse, dann die Zeile: eine Zeile ohne Geheimnisse wäre ein aktiver Zugang, an dem
            // jeder Vorgang scheitert. Umgekehrt bleibt nach einem Fehler höchstens ein Dienst ohne Zeile liegen.
            foreach (var secret in draft.Secrets)
            {
                await store.SaveSecretAsync(tenantId, connectionName + secret.ConnectionSuffix, secret.AuthenticationType,
                    secret.ClientId, secret.Secret, cancellationToken);
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var previous = await db.TenantProviderAccesses
                .Where(a => a.TenantId == tenantId && a.Provider == key && !a.Disabled)
                .ToListAsync(cancellationToken);
            foreach (var old in previous)
            {
                // Abschalten, nicht löschen: seine Verkäufe werden weiter darüber erstattet.
                old.Disabled = true;
                old.Updated = DateTime.UtcNow;
                LogEnvironment.LogEvent(
                    $"Own access {old.TenantProviderAccessId} ({key}/{old.ProviderAccountId}) of tenant {tenantId} was disabled because a new one is being connected. Its sales stay refundable over it.",
                    LogSeverity.Report, ProviderAccessRouter<TContext>.LogContext);
            }

            var row = new TenantProviderAccess
            {
                TenantId = tenantId,
                Provider = key,
                ConnectionName = connectionName,
                ProviderAccountId = draft.ProviderAccountId,
                DisplayName = displayName.Trim(),
                SettingsJson = draft.SettingsJson,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };
            db.TenantProviderAccesses.Add(row);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                LogEnvironment.LogEvent(
                    $"The own access '{displayName}' ({key}/{draft.ProviderAccountId}) of tenant {tenantId} could not be saved. The secrets stored under '{connectionName}' are orphaned and can be removed: {ex.OutlineException()}",
                    LogSeverity.Error, ProviderAccessRouter<TContext>.LogContext);
                throw;
            }

            await VerifyAsync(tenantId, row.TenantProviderAccessId, cancellationToken);
            return row.TenantProviderAccessId;
        }

        /// <inheritdoc />
        public async Task RenewSecretsAsync(int tenantId, int accessId, IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default)
        {
            var row = await LoadAsync(tenantId, accessId, tracked: false, cancellationToken);
            var owner = Resolve(row.Provider);
            var store = RequireStore();
            foreach (var secret in owner.ComposeSecrets(values))
            {
                await store.SaveSecretAsync(tenantId, row.ConnectionName + secret.ConnectionSuffix, secret.AuthenticationType,
                    secret.ClientId, secret.Secret, cancellationToken);
            }

            LogEnvironment.LogEvent(
                $"The secrets of own access {accessId} ({row.Provider}/{row.ProviderAccountId}) of tenant {tenantId} were renewed.",
                LogSeverity.Report, ProviderAccessRouter<TContext>.LogContext);
            await VerifyAsync(tenantId, accessId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task SetDisabledAsync(int tenantId, int accessId, bool disabled, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var row = await db.TenantProviderAccesses.FirstOrDefaultAsync(a => a.TenantProviderAccessId == accessId, cancellationToken);
            EnsureOwnership(row, tenantId, accessId);
            if (!disabled && await db.TenantProviderAccesses.AnyAsync(a => a.TenantId == tenantId && a.Provider == row!.Provider
                                                                          && !a.Disabled && a.TenantProviderAccessId != accessId, cancellationToken))
            {
                // Zwei aktive Zugänge beim selben Anbieter lehnt die Weiche ab - also gar nicht erst zulassen.
                throw new TenantPaymentException(PaymentErrorCodes.InvalidSettings,
                    $"Another own access at '{row!.Provider}' is active. Disable it first.");
            }

            row!.Disabled = disabled;
            row.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<OwnAccessCheck> VerifyAsync(int tenantId, int accessId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var row = await db.TenantProviderAccesses.FirstOrDefaultAsync(a => a.TenantProviderAccessId == accessId, cancellationToken);
            EnsureOwnership(row, tenantId, accessId);

            OwnAccessCheck result;
            try
            {
                result = await Resolve(row!.Provider).VerifyAsync(ProviderAccessSelection.Own(row), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Eine Prüfung, die scheitert, IST das Ergebnis - sie gehört an die Zeile und ins Protokoll.
                LogEnvironment.LogEvent(
                    $"Verifying own access {accessId} ({row!.Provider}/{row.ProviderAccountId}) of tenant {tenantId} failed: {ex.OutlineException()}",
                    LogSeverity.Warning, ProviderAccessRouter<TContext>.LogContext);
                result = new OwnAccessCheck(false, ex.Message);
            }

            if (result.Success)
            {
                row!.LastVerifiedUtc = DateTime.UtcNow;
                row.LastVerificationError = null;
            }
            else
            {
                row!.LastVerificationError = Shorten(result.Message ?? "The access was rejected.");
            }

            row.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return result;
        }

        /// <inheritdoc />
        public async Task<OwnAccessCheck> SetUpWebhooksAsync(int tenantId, int accessId, string publicBaseUrl,
            CancellationToken cancellationToken = default)
        {
            var row = await LoadAsync(tenantId, accessId, tracked: false, cancellationToken);
            var owner = Resolve(row.Provider);
            if (!owner.CanSetUpWebhooks || string.IsNullOrWhiteSpace(owner.WebhookPath))
            {
                return new OwnAccessCheck(false, $"'{row.Provider}' cannot set up its notifications from here.");
            }

            var url = $"{publicBaseUrl.TrimEnd('/')}/{owner.WebhookPath.Trim('/')}/{row.TenantProviderAccessId}";
            try
            {
                var result = await owner.SetUpWebhooksAsync(ProviderAccessSelection.Own(row), url, cancellationToken);
                LogEnvironment.LogEvent(
                    $"Setting up the notifications of own access {accessId} ({row.Provider}/{row.ProviderAccountId}) of tenant {tenantId} at '{url}': {(result.Success ? "done" : "failed")}. {result.Message}",
                    result.Success ? LogSeverity.Report : LogSeverity.Warning, ProviderAccessRouter<TContext>.LogContext);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogEnvironment.LogEvent(
                    $"Setting up the notifications of own access {accessId} ({row.Provider}) of tenant {tenantId} at '{url}' failed: {ex.OutlineException()}",
                    LogSeverity.Error, ProviderAccessRouter<TContext>.LogContext);
                return new OwnAccessCheck(false, ex.Message);
            }
        }

        private OwnAccessInfo Describe(TenantProviderAccess row)
        {
            providers.TryGetValue(row.Provider, out var owner);
            return new OwnAccessInfo
            {
                AccessId = row.TenantProviderAccessId,
                Provider = row.Provider,
                DisplayName = row.DisplayName,
                ProviderAccountId = row.ProviderAccountId,
                Disabled = row.Disabled,
                LastVerifiedUtc = row.LastVerifiedUtc,
                LastVerificationError = row.LastVerificationError,
                WebhookPath = string.IsNullOrWhiteSpace(owner?.WebhookPath)
                    ? null
                    : $"/{owner.WebhookPath.Trim('/')}/{row.TenantProviderAccessId}",
                CanSetUpWebhooks = owner?.CanSetUpWebhooks ?? false
            };
        }

        private async Task<TenantProviderAccess> LoadAsync(int tenantId, int accessId, bool tracked, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var query = tracked ? db.TenantProviderAccesses : db.TenantProviderAccesses.AsNoTracking();
            var row = await query.FirstOrDefaultAsync(a => a.TenantProviderAccessId == accessId, cancellationToken);
            EnsureOwnership(row, tenantId, accessId);
            return row!;
        }

        private IOwnAccessProvider Resolve(string provider)
            => providers.TryGetValue(provider.Trim(), out var owner)
                ? owner
                : throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    $"No own access can be set up for '{provider}'. Known: {(providers.Count == 0 ? "none" : string.Join(", ", providers.Keys))}.");

        private IProviderCredentialStore RequireStore()
            => credentials ?? throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                "No IProviderCredentialStore is registered (AddFlatProviderCredentialStore / AddHierarchyProviderCredentialStore); secrets cannot be stored.");

        /// <summary>Dieselbe Regel wie bei den Terminals: niemand dreht am Zugang eines fremden Mandanten.</summary>
        private static void EnsureOwnership(TenantProviderAccess? row, int tenantId, int accessId)
        {
            if (row == null)
            {
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound, $"Own access {accessId} does not exist.");
            }

            if (row.TenantId != tenantId)
            {
                LogEnvironment.LogEvent(
                    $"Tenant {tenantId} tried to change own access {accessId}, which belongs to tenant {row.TenantId}. Refused.",
                    LogSeverity.Error, ProviderAccessRouter<TContext>.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                    $"Own access {accessId} does not belong to tenant {tenantId}.");
            }
        }

        private static string Shorten(string message) => message.Length <= 1024 ? message : message[..1024];
    }
}
