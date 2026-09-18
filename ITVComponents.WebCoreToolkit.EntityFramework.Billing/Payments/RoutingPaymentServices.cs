using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Der Verkaufsdienst, der je Mandant an den zuständigen Anbieter weiterleitet.
    /// </summary>
    /// <remarks>
    /// Wird anstelle eines einzelnen Anbieters als <see cref="ITenantSaleService"/> registriert, sobald
    /// mehr als einer im Spiel ist. Für den Aufrufer ändert sich nichts — er sieht denselben Vertrag.
    /// </remarks>
    public class RoutingTenantSaleService<TContext> : ITenantSaleService
        where TContext : DbContext, IPaymentsContext
    {
        private readonly PaymentProviderRouter<TContext> router;

        /// <summary>Initializes a new instance of the <see cref="RoutingTenantSaleService{TContext}"/> class.</summary>
        public RoutingTenantSaleService(PaymentProviderRouter<TContext> router)
        {
            this.router = router;
        }

        /// <inheritdoc />
        public async Task<SaleResult> CreateSaleAsync(SaleRequest request, CancellationToken cancellationToken = default)
        {
            // Ein NEUER Verkauf: es gilt, was am Konto des Mandanten steht.
            var provider = await router.ForTenantAsync(request.TenantId, cancellationToken);
            return await provider.Sales.CreateSaleAsync(request, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<SaleResult> GetSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default)
        {
            var provider = await router.ForSaleAsync(tenantSaleId, cancellationToken);
            return await provider.Sales.GetSaleAsync(tenantSaleId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<SaleResult?> FindByReferenceAsync(int tenantId, string externalReference,
            CancellationToken cancellationToken = default)
        {
            var provider = await router.ForTenantAsync(tenantId, cancellationToken);
            return await provider.Sales.FindByReferenceAsync(tenantId, externalReference, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<RefundResult> RefundSaleAsync(int tenantSaleId, long? amountMinor, string? reason,
            bool? refundApplicationFee = null, CancellationToken cancellationToken = default)
        {
            // Der BESTEHENDE Verkauf entscheidet, nicht das Konto: nach einem Anbieterwechsel ist ein
            // alter Verkauf weiterhin beim alten zu erstatten.
            var provider = await router.ForSaleAsync(tenantSaleId, cancellationToken);
            return await provider.Sales.RefundSaleAsync(tenantSaleId, amountMinor, reason, refundApplicationFee, cancellationToken);
        }
    }

    /// <summary>
    /// Die Konto-Anbindung, die je Mandant an den zuständigen Anbieter weiterleitet.
    /// </summary>
    public class RoutingTenantPaymentAccountService<TContext> : ITenantPaymentAccountService
        where TContext : DbContext, IPaymentsContext
    {
        private readonly PaymentProviderRouter<TContext> router;

        /// <summary>Initializes a new instance of the <see cref="RoutingTenantPaymentAccountService{TContext}"/> class.</summary>
        public RoutingTenantPaymentAccountService(PaymentProviderRouter<TContext> router)
        {
            this.router = router;
        }

        /// <inheritdoc />
        public async Task<TenantPaymentAccountStatus?> GetStatusAsync(int tenantId,
            CancellationToken cancellationToken = default)
        {
            var provider = await router.ForTenantAsync(tenantId, cancellationToken);
            return await provider.Accounts.GetStatusAsync(tenantId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string> StartOnboardingAsync(int tenantId, string returnUrl, string refreshUrl,
            string? email = null, string? country = null, CancellationToken cancellationToken = default)
        {
            var provider = await router.ForTenantAsync(tenantId, cancellationToken);
            return await provider.Accounts.StartOnboardingAsync(tenantId, returnUrl, refreshUrl, email, country, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TenantPaymentAccountStatus> RefreshAsync(int tenantId,
            CancellationToken cancellationToken = default)
        {
            var provider = await router.ForTenantAsync(tenantId, cancellationToken);
            return await provider.Accounts.RefreshAsync(tenantId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string?> CreateDashboardLinkAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            var provider = await router.ForTenantAsync(tenantId, cancellationToken);
            return await provider.Accounts.CreateDashboardLinkAsync(tenantId, cancellationToken);
        }
    }

    /// <summary>
    /// Ein Anbieter unter seinem Namen, zusammengesetzt aus seinen beiden Diensten.
    /// </summary>
    /// <remarks>
    /// Damit muss kein Anbieter-Paket eine eigene Klasse dafür schreiben — die Registrierung reicht.
    /// </remarks>
    public sealed class PaymentProviderAdapter : IPaymentProviderAdapter
    {
        /// <summary>Initializes a new instance of the <see cref="PaymentProviderAdapter"/> class.</summary>
        public PaymentProviderAdapter(string key, ITenantSaleService sales, ITenantPaymentAccountService accounts)
        {
            Key = key.Trim().ToLowerInvariant();
            Sales = sales;
            Accounts = accounts;
        }

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public ITenantSaleService Sales { get; }

        /// <inheritdoc />
        public ITenantPaymentAccountService Accounts { get; }
    }

    /// <summary>
    /// Ein Konto-Dienst für einen Anbieter, der keine Selbstanmeldung kennt.
    /// </summary>
    /// <remarks>
    /// Sagt bei jedem Aufruf, WARUM es hier nichts zu tun gibt, statt dass der Aufrufer auf einen nicht
    /// registrierten Dienst läuft. Der Unterschied ist der zwischen einer Erklärung und einem
    /// Auflösungsfehler tief im Aufruf.
    /// <para>
    /// <see cref="GetStatusAsync"/> antwortet trotzdem aus der Ablage: was ein Verwalter dort von Hand
    /// eingetragen hat — die Raum- oder Kontokennung des Anbieters — soll die Maske anzeigen können.
    /// </para>
    /// </remarks>
    public sealed class UnsupportedTenantPaymentAccountService : ITenantPaymentAccountService
    {
        private readonly string providerKey;
        private readonly string reason;

        /// <summary>Initializes a new instance of the <see cref="UnsupportedTenantPaymentAccountService"/> class.</summary>
        public UnsupportedTenantPaymentAccountService(string providerKey, string reason)
        {
            this.providerKey = providerKey;
            this.reason = reason;
        }

        /// <inheritdoc />
        public Task<TenantPaymentAccountStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<TenantPaymentAccountStatus?>(null);

        /// <inheritdoc />
        public Task<string> StartOnboardingAsync(int tenantId, string returnUrl, string refreshUrl,
            string? email = null, string? country = null, CancellationToken cancellationToken = default)
            => throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                $"Provider '{providerKey}' has no self-service onboarding. {reason}");

        /// <inheritdoc />
        public Task<TenantPaymentAccountStatus> RefreshAsync(int tenantId, CancellationToken cancellationToken = default)
            => throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                $"Provider '{providerKey}' has no account state to refresh. {reason}");

        /// <inheritdoc />
        public Task<string?> CreateDashboardLinkAsync(int tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }
}
