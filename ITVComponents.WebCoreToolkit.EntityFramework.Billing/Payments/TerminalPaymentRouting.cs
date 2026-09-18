using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Ein Terminal-Weg unter seinem Namen.
    /// </summary>
    public interface ITerminalProviderAdapter
    {
        /// <summary>Der Name: <c>stripe</c>, <c>payrexx</c>, <c>wallee</c>, <c>agent</c>.</summary>
        string Key { get; }

        /// <summary>Der Dienst dahinter.</summary>
        ITerminalPaymentService Terminals { get; }
    }

    /// <summary>Ein Weg unter seinem Namen, ohne dass ein Paket dafür eine Klasse schreiben muss.</summary>
    public sealed class TerminalProviderAdapter : ITerminalProviderAdapter
    {
        /// <summary>Initializes a new instance of the <see cref="TerminalProviderAdapter"/> class.</summary>
        public TerminalProviderAdapter(string key, ITerminalPaymentService terminals)
        {
            Key = key.Trim().ToLowerInvariant();
            Terminals = terminals;
        }

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public ITerminalPaymentService Terminals { get; }
    }

    /// <summary>
    /// Der Terminaldienst, der je Gerät an den zuständigen Weg weiterleitet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hier entscheidet das Gerät, nicht das Konto</b> — anders als bei den Online-Verkäufen. Genau
    /// dafür ist Achse C gemacht: derselbe Mandant kann im Laden ein Payrexx-Terminal stehen haben und
    /// online über Stripe abrechnen, und an einer zweiten Filiale hängt ein wallee-Gerät am Kassen-PC.
    /// </para>
    /// <para>
    /// Bei einem <b>laufenden</b> Vorgang entscheidet dagegen die Verkaufszeile: dort ist der Anbieter
    /// eingefroren. Wird ein Gerät auf einen anderen Weg umgestellt, während eine Zahlung läuft, muss
    /// die Frage „wurde die Karte belastet?" trotzdem an den gestellt werden, der sie ausgelöst hat.
    /// </para>
    /// </remarks>
    public class RoutingTerminalPaymentService<TContext> : ITerminalPaymentService, ITerminalProviderCatalog
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly Dictionary<string, ITerminalProviderAdapter> adapters;

        /// <summary>Initializes a new instance of the <see cref="RoutingTerminalPaymentService{TContext}"/> class.</summary>
        public RoutingTerminalPaymentService(IDbContextFactory<TContext> dbFactory,
            IEnumerable<ITerminalProviderAdapter> adapters)
        {
            this.dbFactory = dbFactory;
            this.adapters = new Dictionary<string, ITerminalProviderAdapter>(StringComparer.OrdinalIgnoreCase);
            foreach (var adapter in adapters)
            {
                if (!this.adapters.TryAdd(adapter.Key, adapter))
                {
                    // Zwei Wege unter demselben Namen: welcher gewaenne, haenge an der Registrierungs-
                    // reihenfolge. Das ist kein Zustand, in dem man Geld bewegt.
                    throw new InvalidOperationException(
                        $"Two terminal providers are registered under the name '{adapter.Key}'. Give them distinct names.");
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Liest selbst aus der Ablage statt weiterzuleiten: die Liste geht über ALLE Wege, und ein
        /// einzelner Anbieter kennt nur seine eigenen Geräte.
        /// </remarks>
        public async Task<IReadOnlyList<TerminalInfo>> GetTerminalsAsync(int tenantId, bool includeDisabled = false,
            CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var query = db.TenantPaymentTerminals.AsNoTracking().Where(t => t.TenantId == tenantId);
            if (!includeDisabled)
            {
                query = query.Where(t => t.Enabled);
            }

            return await query.OrderBy(t => t.DisplayName)
                .Select(t => new TerminalInfo(t.TenantPaymentTerminalId, t.DisplayName, t.Provider,
                    t.ProviderTerminalId, t.Enabled))
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TerminalStatus> GetTerminalStatusAsync(int tenantId, int terminalId,
            CancellationToken cancellationToken = default)
            => await (await ForTerminalAsync(terminalId, cancellationToken))
                .GetTerminalStatusAsync(tenantId, terminalId, cancellationToken);

        /// <inheritdoc />
        public async Task<TerminalSaleResult> StartPaymentAsync(TerminalSaleRequest request,
            CancellationToken cancellationToken = default)
            => await (await ForTerminalAsync(request.TerminalId, cancellationToken))
                .StartPaymentAsync(request, cancellationToken);

        /// <inheritdoc />
        public async Task<TerminalSaleResult> GetPaymentAsync(int tenantSaleId, CancellationToken cancellationToken = default)
            => await (await ForSaleAsync(tenantSaleId, cancellationToken)).GetPaymentAsync(tenantSaleId, cancellationToken);

        /// <inheritdoc />
        public async Task<TerminalSaleResult> CancelPaymentAsync(int tenantSaleId, CancellationToken cancellationToken = default)
            => await (await ForSaleAsync(tenantSaleId, cancellationToken)).CancelPaymentAsync(tenantSaleId, cancellationToken);

        /// <inheritdoc />
        /// <remarks>
        /// Die Weiche selbst hat keine eigenen Felder — sie leitet weiter. Dass diese Methode hier
        /// überhaupt steht, liegt am gemeinsamen Vertrag; gefragt wird über den Katalog mit dem Namen
        /// des Weges.
        /// </remarks>
        public IReadOnlyList<TerminalSettingDescriptor> DescribeSettings() => [];

        /// <inheritdoc />
        public bool HasDeviceSettings => false;

        /// <inheritdoc />
        public Task<IReadOnlyList<TerminalSettingDescriptor>> DescribeDeviceSettingsAsync(
            string? configurationJson, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TerminalSettingDescriptor>>([]);

        /// <inheritdoc />
        public IReadOnlyList<TerminalProviderInfo> GetProviders()
            => [.. adapters.Values.Select(a => new TerminalProviderInfo(a.Key, a.Terminals.HasDeviceSettings))];

        /// <inheritdoc />
        IReadOnlyList<TerminalSettingDescriptor> ITerminalProviderCatalog.DescribeSettings(string providerKey)
            => Resolve(providerKey, "a new terminal").DescribeSettings();

        /// <inheritdoc />
        public Task<IReadOnlyList<TerminalSettingDescriptor>> DescribeDeviceSettingsAsync(string providerKey,
            string? configurationJson, CancellationToken cancellationToken = default)
            => Resolve(providerKey, "a new terminal").DescribeDeviceSettingsAsync(configurationJson, cancellationToken);

        /// <summary>Der Weg, über den dieses Gerät abrechnet.</summary>
        private async Task<ITerminalPaymentService> ForTerminalAsync(int terminalId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var provider = await db.TenantPaymentTerminals.AsNoTracking()
                .Where(t => t.TenantPaymentTerminalId == terminalId)
                .Select(t => t.Provider)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                    $"Terminal {terminalId} is not registered.");
            }

            return Resolve(provider, $"terminal {terminalId}");
        }

        /// <summary>Der Weg, der DIESEN Vorgang ausgelöst hat — eingefroren auf der Verkaufszeile.</summary>
        private async Task<ITerminalPaymentService> ForSaleAsync(int tenantSaleId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var provider = await db.TenantSales.AsNoTracking()
                .Where(s => s.TenantSaleId == tenantSaleId)
                .Select(s => s.Provider)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound,
                    $"Sale {tenantSaleId} does not name the provider that handled it.");
            }

            return Resolve(provider, $"sale {tenantSaleId}");
        }

        private ITerminalPaymentService Resolve(string provider, string what)
        {
            if (adapters.TryGetValue(provider.Trim(), out var adapter))
            {
                return adapter.Terminals;
            }

            // Kein Ausweichen auf irgendeinen anderen: der falsche Weg liefe entweder ins Leere oder -
            // schlimmer - auf ein fremdes Geraet.
            var known = adapters.Count == 0 ? "none" : string.Join(", ", adapters.Keys);
            LogEnvironment.LogEvent(
                $"No terminal provider named '{provider}' is registered (known: {known}), asked for {what}.",
                LogSeverity.Error, TenantSaleWebhookSink<TContext>.LogContext);
            throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                $"No terminal provider named '{provider}' is registered. Known providers: {known}.");
        }
    }
}
