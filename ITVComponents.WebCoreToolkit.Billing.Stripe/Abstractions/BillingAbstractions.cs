using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions
{
    /// <summary>Creates a Stripe Checkout session for a tenant subscribing to a base plan plus optional add-ons.</summary>
    public interface IStripeCheckoutSessionFactory
    {
        /// <summary>
        /// Creates a subscription-mode Checkout session and returns its hosted URL. The base
        /// <paramref name="planId"/> and each add-on become a line-item on one subscription. The tenant id is
        /// stamped into subscription metadata so the webhook can attribute the resulting subscription.
        /// </summary>
        Task<string> CreateCheckoutSessionAsync(int tenantId, int planId, IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, CancellationToken cancellationToken = default);
    }

    /// <summary>Creates a Stripe customer-portal session so the tenant can self-serve plan/add-on/payment changes.</summary>
    public interface IStripeBillingPortalFactory
    {
        /// <summary>Creates a billing-portal session for the tenant's Stripe customer and returns its URL.</summary>
        Task<string?> CreatePortalSessionAsync(int tenantId, string returnUrl, CancellationToken cancellationToken = default);
    }

    /// <summary>Verifies and processes Stripe webhook events into the local subscription mirror + feature provisioning.</summary>
    public interface IStripeWebhookHandler
    {
        /// <summary>
        /// Verifies the signature and processes the event payload. Updates the local
        /// <c>TenantSubscription</c> + items and reconciles the tenant's entitled features.
        /// </summary>
        Task HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Pushes locally-authored <c>Plan</c>/<c>AddOn</c> definitions to Stripe as Product + Price and writes the
    /// provider identifiers back. Provider prices are immutable — an amount/interval change creates a NEW price
    /// and archives the old one.
    /// </summary>
    public interface IPlanSynchronizer
    {
        /// <summary>Creates/updates the Stripe Product+Price for the plan and persists ProviderProductId/PriceId.</summary>
        Task SyncPlanAsync(int planId, CancellationToken cancellationToken = default);

        /// <summary>Creates/updates the Stripe Product+Price for the add-on and persists ProviderProductId/PriceId.</summary>
        Task SyncAddOnAsync(int addOnId, CancellationToken cancellationToken = default);
    }
}
