using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Ein Zahlungsanbieter unter seinem Namen — die Einheit, zwischen denen die Weiche wählt.
    /// </summary>
    /// <remarks>
    /// Jedes Anbieter-Paket registriert genau einen davon. Erst dadurch können mehrere nebeneinander
    /// bestehen: ohne diese Klammer gewönne bei zwei registrierten <see cref="ITenantSaleService"/>
    /// stillschweigend der zuletzt eingetragene, und Zahlungen liefen über einen anderen Anbieter als
    /// gedacht — ohne dass irgendwo etwas fehlschlägt.
    /// </remarks>
    public interface IPaymentProviderAdapter
    {
        /// <summary>
        /// Der Name, unter dem dieser Anbieter angesprochen wird: <c>stripe</c>, <c>payrexx</c>,
        /// <c>wallee</c>. Kleingeschrieben, und er steht so in der Datenbank — Umbenennen heisst
        /// migrieren.
        /// </summary>
        string Key { get; }

        /// <summary>Verkäufe und Erstattungen dieses Anbieters.</summary>
        ITenantSaleService Sales { get; }

        /// <summary>Konto-Anbindung dieses Anbieters.</summary>
        ITenantPaymentAccountService Accounts { get; }

        /// <summary>
        /// Ob dieser Anbieter eine Zahlungsseite ausstellen kann. <c>false</c> bei einem Anbieter, der hier nur
        /// steht, damit seine Terminalverkaeufe erstattet werden koennen (SumUp).
        /// </summary>
        /// <remarks>
        /// Die Weiche zaehlt beim "genau einer registriert" nur Anbieter, die das koennen. Sonst machte ein
        /// zusaetzlich angebundenes Terminal aus einem Host mit EINEM Online-Anbieter einen mit zweien - und
        /// jeder Online-Verkauf scheiterte an der Frage, welcher gemeint ist.
        /// </remarks>
        bool SupportsOnlineSales => true;
    }

    /// <summary>
    /// Sucht zu einem Mandanten oder einem Verkauf den zuständigen Anbieter heraus.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Zwei verschiedene Fragen, zwei verschiedene Quellen</b> — und das ist der Kern:
    /// </para>
    /// <list type="bullet">
    /// <item>Für einen NEUEN Vorgang gilt, was am Konto des Mandanten steht.</item>
    /// <item>Für einen BESTEHENDEN Verkauf gilt, was auf seiner Zeile eingefroren wurde. Wechselt ein
    /// Mandant den Anbieter, bleiben seine alten Verkäufe beim alten erstattbar. Läse man auch hier das
    /// Konto, ginge die Erstattung an den neuen Anbieter — und träfe dort entweder nichts oder eine
    /// fremde Transaktion.</item>
    /// </list>
    /// </remarks>
    public class PaymentProviderRouter<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IReadOnlyList<IPaymentProviderAdapter> adapters;
        private readonly string? defaultKey;

        /// <summary>Initializes a new instance of the <see cref="PaymentProviderRouter{TContext}"/> class.</summary>
        /// <param name="dbFactory">Zugang zur Ablage</param>
        /// <param name="adapters">alle registrierten Anbieter</param>
        /// <param name="defaultKey">
        /// der Anbieter der Plattform. Leer heisst: der einzige registrierte — und wenn es mehrere gibt,
        /// ist das ein Konfigurationsfehler, kein Zufallsentscheid.
        /// </param>
        public PaymentProviderRouter(IDbContextFactory<TContext> dbFactory,
            IEnumerable<IPaymentProviderAdapter> adapters, string? defaultKey)
        {
            this.dbFactory = dbFactory;
            this.adapters = adapters.ToList();
            this.defaultKey = string.IsNullOrWhiteSpace(defaultKey) ? null : defaultKey.Trim().ToLowerInvariant();

            var duplicate = this.adapters.GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                // Zwei Anbieter unter demselben Namen: welcher gewaenne, waere Registrierungsreihenfolge -
                // also Zufall. Lieber beim Start scheitern als bei der ersten Zahlung raten.
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    $"Two payment providers are registered under the name '{duplicate.Key}'.");
            }
        }

        /// <summary>Alle registrierten Anbieter - fuer Diagnose und Verwaltungsmasken.</summary>
        public IReadOnlyList<IPaymentProviderAdapter> Adapters => adapters;

        /// <summary>Der Anbieter, der fuer NEUE Vorgaenge dieses Mandanten gilt.</summary>
        /// <remarks>
        /// Das Plattform-Konto entscheidet, wenn es eines gibt - dort hat der Mandant seinen Anbieter gewaehlt.
        /// Ohne Plattform-Konto gilt der Anbieter, bei dem er einen eigenen Zugang hinterlegt hat
        /// (<see cref="TenantProviderAccess"/>), sofern dieser Anbieter hier fuer den Online-Verkauf
        /// angemeldet ist. Sonst fiele ein Mandant, der nur sein eigenes Konto mitbringt, auf den Anbieter
        /// der Plattform - und dort gibt es fuer ihn kein Konto.
        /// </remarks>
        public async Task<IPaymentProviderAdapter> ForTenantAsync(int tenantId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.AsNoTracking().Where(a => a.TenantId == tenantId)
                .Select(a => new { a.Provider }).FirstOrDefaultAsync(cancellationToken);
            if (account != null)
            {
                return Resolve(account.Provider, $"tenant {tenantId}");
            }

            var own = (await db.TenantProviderAccesses.AsNoTracking()
                    .Where(a => a.TenantId == tenantId && !a.Disabled)
                    .Select(a => a.Provider).Distinct().ToListAsync(cancellationToken))
                .Where(p => adapters.Any(a => a.SupportsOnlineSales && string.Equals(a.Key, p, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            switch (own.Count)
            {
                case 1:
                    return Resolve(own[0], $"tenant {tenantId} (own access)");
                case > 1:
                    // Zwei eigene Zugaenge bei zwei Anbietern und kein Plattform-Konto, das waehlt: raten hiesse,
                    // Geld ueber einen Anbieter zu leiten, den der Mandant fuer den Online-Verkauf nie bestimmt hat.
                    throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                        $"Tenant {tenantId} has own accesses at several providers ({string.Join(", ", own)}) and no payout account naming one for online sales.");
                default:
                    return Resolve(null, $"tenant {tenantId}");
            }
        }

        /// <summary>
        /// Der Anbieter, der DIESEN Verkauf abgewickelt hat - unabhaengig davon, was inzwischen am Konto
        /// steht.
        /// </summary>
        public async Task<IPaymentProviderAdapter> ForSaleAsync(int tenantSaleId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.AsNoTracking().Where(s => s.TenantSaleId == tenantSaleId)
                .Select(s => new { s.Provider, s.TenantId }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound, $"Sale {tenantSaleId} not found.");

            if (!string.IsNullOrWhiteSpace(sale.Provider))
            {
                return Resolve(sale.Provider, $"sale {tenantSaleId}");
            }

            // Altbestand: Zeilen von vor der Einfuehrung dieses Feldes. Die stammen alle vom damals
            // einzigen Anbieter, also entscheidet der Vorgabewert - aber sichtbar, denn spaetestens beim
            // zweiten Anbieter ist diese Annahme falsch.
            LogEnvironment.LogEvent(
                $"Sale {tenantSaleId} (tenant {sale.TenantId}) carries no provider on its row; falling back to the default. Rows written before the provider column existed are expected here — a NEW row without it is a bug.",
                LogSeverity.Warning, "TenantPayments");
            return Resolve(null, $"sale {tenantSaleId}");
        }

        private IPaymentProviderAdapter Resolve(string? key, string what)
        {
            key = string.IsNullOrWhiteSpace(key) ? defaultKey : key.Trim().ToLowerInvariant();

            if (key != null)
            {
                return adapters.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))
                       ?? throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                           $"The payment provider '{key}' is configured for {what} but not registered. Registered: {Describe()}.");
            }

            var online = adapters.Where(a => a.SupportsOnlineSales).ToList();
            return online.Count switch
            {
                1 => online[0],
                0 => throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    "No payment provider for online sales is registered."),
                // Mehrere registriert und keiner benannt: hier zu raten hiesse, Geld ueber einen Anbieter
                // zu leiten, den niemand gewaehlt hat.
                _ => throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    $"Several payment providers are registered ({Describe()}) but neither {what} nor the configuration names one.")
            };
        }

        private string Describe() => adapters.Count == 0 ? "none" : string.Join(", ", adapters.Select(a => a.Key));
    }
}
