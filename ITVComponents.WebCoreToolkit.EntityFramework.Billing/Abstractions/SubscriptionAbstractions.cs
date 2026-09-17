using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions
{
    /// <summary>Creates the hosted subscription checkout for a tenant taking a base plan plus optional add-ons.</summary>
    public interface ISubscriptionCheckoutFactory
    {
        /// <summary>
        /// Creates the subscription checkout and returns its hosted URL. The base
        /// <paramref name="planId"/> and each add-on become a line-item on one subscription. The tenant id is
        /// stamped into subscription metadata so the webhook can attribute the resulting subscription. A
        /// subscription is single-currency: the plan and every add-on must have a price in
        /// <paramref name="currency"/> (null = the configured default currency).
        /// </summary>
        Task<string> CreateCheckoutSessionAsync(int tenantId, int planId, IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, string? currency = null, CancellationToken cancellationToken = default);
    }

    /// <summary>Creates the provider's self-service portal so the tenant can change plan, add-ons and payment method.</summary>
    public interface IBillingPortalFactory
    {
        /// <summary>Creates a portal session for the tenant's customer record at the provider and returns its URL.</summary>
        Task<string?> CreatePortalSessionAsync(int tenantId, string returnUrl, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Pushes locally-authored <c>Plan</c>/<c>AddOn</c> definitions to the provider as its product/price pair and
    /// writes the provider identifiers back. Provider prices are immutable — an amount/interval change creates a NEW price
    /// and archives the old one.
    /// </summary>
    public interface IPlanSynchronizer
    {
        /// <summary>Creates/updates the provider's product+price for the plan and persists ProviderProductId/PriceId.</summary>
        Task SyncPlanAsync(int planId, CancellationToken cancellationToken = default);

        /// <summary>Creates/updates the provider's product+price for the add-on and persists ProviderProductId/PriceId.</summary>
        Task SyncAddOnAsync(int addOnId, CancellationToken cancellationToken = default);
    }
}
