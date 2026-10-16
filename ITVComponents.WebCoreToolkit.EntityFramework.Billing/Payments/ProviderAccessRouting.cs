using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Ueber welchen Zugang ein Vorgang bei einem Anbieter laeuft: den der Plattform oder einen eigenen des
    /// Mandanten (<see cref="TenantProviderAccess"/>).
    /// </summary>
    /// <remarks>
    /// Traegt nur nicht-geheime Angaben. Die Geheimnisse holt der Anbieter selbst ueber
    /// <see cref="Abstractions.IProviderCredentialStore"/> mit <see cref="ConnectionName"/> - erst in dem Moment,
    /// in dem er sie braucht, damit sie nicht in einem Objekt herumliegen, das protokolliert werden koennte.
    /// </remarks>
    public sealed class ProviderAccessSelection
    {
        private static readonly JsonSerializerOptions SettingsJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private ProviderAccessSelection(int tenantId, string provider, TenantProviderAccess? access)
        {
            TenantId = tenantId;
            Provider = provider;
            AccessId = access?.TenantProviderAccessId;
            ConnectionName = access?.ConnectionName;
            ProviderAccountId = access?.ProviderAccountId;
            SettingsJson = access?.SettingsJson;
            Disabled = access?.Disabled ?? false;
        }

        /// <summary>Der Zugang der Plattform.</summary>
        public static ProviderAccessSelection Platform(int tenantId, string provider) => new(tenantId, provider, null);

        /// <summary>Ein eigener Zugang des Mandanten.</summary>
        public static ProviderAccessSelection Own(TenantProviderAccess access) => new(access.TenantId, access.Provider, access);

        /// <summary>Der Mandant des Vorgangs.</summary>
        public int TenantId { get; }

        /// <summary>Der Anbieter, kleingeschrieben.</summary>
        public string Provider { get; }

        /// <summary>Die Zeile des eigenen Zugangs; leer beim Plattform-Zugang.</summary>
        public int? AccessId { get; }

        /// <summary>Ob ein eigener Zugang des Mandanten gilt.</summary>
        public bool IsOwnAccess => AccessId != null;

        /// <summary>Der ExternalService mit den Geheimnissen; leer beim Plattform-Zugang.</summary>
        public string? ConnectionName { get; }

        /// <summary>Wie der eigene Zugang beim Anbieter heisst; leer beim Plattform-Zugang.</summary>
        public string? ProviderAccountId { get; }

        /// <summary>Die weiteren nicht-geheimen Angaben als JSON.</summary>
        public string? SettingsJson { get; }

        /// <summary>
        /// Der Zugang ist abgeschaltet. Kommt nur bei einem BESTEHENDEN Verkauf vor - der wird auf dem Konto
        /// erstattet und nachgefragt, auf dem er lief, auch wenn der Mandant inzwischen gewechselt hat.
        /// </summary>
        public bool Disabled { get; }

        /// <summary>Liest <see cref="SettingsJson"/> in das Modell des Anbieters; leer, wenn nichts dasteht.</summary>
        public T? GetSettings<T>() where T : class
            => string.IsNullOrWhiteSpace(SettingsJson) ? null : JsonSerializer.Deserialize<T>(SettingsJson, SettingsJsonOptions);

        /// <inheritdoc />
        public override string ToString()
            => IsOwnAccess ? $"own access {AccessId} ({Provider}/{ProviderAccountId}) of tenant {TenantId}" : $"platform access ({Provider}) for tenant {TenantId}";
    }

    /// <summary>
    /// Wo eine Zahlungsmeldung herkommt: von welchem Anbieter und ueber welchen Zugang.
    /// </summary>
    /// <param name="Provider">der Anbieter</param>
    /// <param name="TenantProviderAccessId">der eigene Zugang, oder leer fuer den der Plattform</param>
    public readonly record struct SaleSearchScope(string Provider, int? TenantProviderAccessId)
    {
        /// <summary>Meldungen an den Endpunkt der Plattform.</summary>
        public static SaleSearchScope Platform(string provider) => new(provider, null);
    }

    /// <summary>
    /// Sucht zu einem Vorgang den Zugang heraus - die zweite Weiche neben <see cref="PaymentProviderRouter{TContext}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dieselben zwei Fragen mit denselben zwei Quellen wie beim Anbieter: fuer einen NEUEN Vorgang gilt der
    /// aktive eigene Zugang des Mandanten bei diesem Anbieter (sonst die Plattform); fuer einen BESTEHENDEN
    /// Verkauf gilt, was auf ihm eingefroren wurde.
    /// </para>
    /// <para>
    /// <b>Nie stillschweigend auf die Plattform ausweichen.</b> Ist der eingefrorene Zugang eines Verkaufs
    /// nicht mehr auffindbar, ist das ein Fehler und kein Anlass, es mit dem Plattform-Zugang zu versuchen:
    /// die Erstattung traefe dort nichts oder - schlimmer - eine fremde Transaktion.
    /// </para>
    /// </remarks>
    public sealed class ProviderAccessRouter<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        /// <summary>Protokoll-Kategorie der Zugangswahl.</summary>
        public const string LogContext = "TenantPayments";

        private readonly IDbContextFactory<TContext> dbFactory;

        /// <summary>Initializes a new instance of the <see cref="ProviderAccessRouter{TContext}"/> class.</summary>
        public ProviderAccessRouter(IDbContextFactory<TContext> dbFactory)
        {
            this.dbFactory = dbFactory;
        }

        /// <summary>Der Zugang fuer einen NEUEN Vorgang dieses Mandanten bei diesem Anbieter.</summary>
        public async Task<ProviderAccessSelection> ForNewAsync(int tenantId, string provider, CancellationToken cancellationToken)
        {
            var key = Normalize(provider);
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var active = await db.TenantProviderAccesses.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.Provider == key && !a.Disabled)
                .ToListAsync(cancellationToken);

            switch (active.Count)
            {
                case 0:
                    return ProviderAccessSelection.Platform(tenantId, key);
                case 1:
                    return ProviderAccessSelection.Own(active[0]);
                default:
                    // Zwei aktive Zugaenge: welcher gewaenne, waere die Reihenfolge der Abfrage - also Zufall,
                    // und das Geld ginge womoeglich aufs falsche Konto. Lieber den Vorgang ablehnen.
                    LogEnvironment.LogEvent(
                        $"Tenant {tenantId} has {active.Count} active own accesses at '{key}' ({string.Join(", ", active.Select(a => a.TenantProviderAccessId))}); only one may be active. The operation is refused until one is disabled.",
                        LogSeverity.Error, LogContext);
                    throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                        $"Tenant {tenantId} has several active own accesses at '{key}'.");
            }
        }

        /// <summary>Der Zugang, ueber den DIESER Verkauf lief - unabhaengig davon, was inzwischen gilt.</summary>
        public async Task<ProviderAccessSelection> ForSaleAsync(TenantSale sale, CancellationToken cancellationToken)
        {
            var key = Normalize(sale.Provider ?? string.Empty);
            if (sale.TenantProviderAccessId is not { } accessId)
            {
                return ProviderAccessSelection.Platform(sale.TenantId, key);
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var access = await db.TenantProviderAccesses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantProviderAccessId == accessId, cancellationToken);
            if (access == null || access.TenantId != sale.TenantId)
            {
                LogEnvironment.LogEvent(
                    $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) ran over own access {accessId}, which {(access == null ? "no longer exists" : $"belongs to tenant {access.TenantId}")}. It is NOT retried over the platform access.",
                    LogSeverity.Error, LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    $"The own access {accessId} of sale {sale.TenantSaleId} is not available.");
            }

            if (access.Disabled)
            {
                LogEnvironment.LogDebugEvent(
                    $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) is handled over its disabled own access {accessId} - the account it was paid on.",
                    LogSeverity.Report);
            }

            return ProviderAccessSelection.Own(access);
        }

        /// <summary>
        /// Die Anbieter, bei denen dieser Mandant einen aktiven eigenen Zugang hat.
        /// </summary>
        public async Task<IReadOnlyList<string>> ActiveProvidersAsync(int tenantId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return await db.TenantProviderAccesses.AsNoTracking()
                .Where(a => a.TenantId == tenantId && !a.Disabled)
                .Select(a => a.Provider).Distinct().ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Lehnt einen Verkauf ueber einen eigenen Zugang ab, wenn die Anbindung damit nicht umgehen kann.
        /// </summary>
        /// <remarks>
        /// Die Alternative waere, den Vorgang mit den Zugangsdaten der Plattform auszufuehren - beim Anlegen
        /// ginge das Geld dann auf das Plattform-Konto, beim Erstatten an ein Konto, auf dem die Zahlung nie lag.
        /// </remarks>
        /// <param name="access">der gewaehlte Zugang</param>
        /// <param name="supportsOwnAccess">ob die Anbindung einen eigenen Zugang verwenden kann</param>
        /// <param name="providerKey">der Anbieter, fuers Protokoll</param>
        /// <param name="what">der Vorgang, fuers Protokoll</param>
        /// <param name="requiresOwnAccess">
        /// ob die Anbindung NUR mit einem eigenen Zugang arbeitet - bei Anbietern ohne Plattform-Konto (SumUp).
        /// Ohne eigenen Zugang wird dann abgelehnt, statt mit einer Frage nach dem Plattform-Konto zu scheitern,
        /// die es dort gar nicht gibt.
        /// </param>
        public static void EnsureSupported(ProviderAccessSelection access, bool supportsOwnAccess, string providerKey, string what,
            bool requiresOwnAccess = false)
        {
            if (requiresOwnAccess && !access.IsOwnAccess)
            {
                LogEnvironment.LogEvent(
                    $"{what} is refused: '{providerKey}' only works with the tenant's own access, and tenant {access.TenantId} has none there.",
                    LogSeverity.Warning, LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    $"'{providerKey}' needs the tenant's own access, and tenant {access.TenantId} has none.");
            }

            if (!access.IsOwnAccess || supportsOwnAccess)
            {
                return;
            }

            LogEnvironment.LogEvent(
                $"{what} is refused: tenant {access.TenantId} uses its own access {access.AccessId} at '{providerKey}', and this connection cannot work with own accesses yet. It is NOT run over the platform access.",
                LogSeverity.Error, LogContext);
            throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                $"The '{providerKey}' connection does not support the tenant's own access yet.");
        }

        /// <summary>Wie ein Anbietername abgelegt und verglichen wird.</summary>
        public static string Normalize(string provider) => provider.Trim().ToLowerInvariant();
    }
}
