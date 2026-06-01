using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions
{
    /// <summary>
    /// Decoupling seam between Billing and the entitlement system. The Stripe (or future) service-layer calls
    /// this after a subscription change with the union of all feature keys the tenant is currently entitled to
    /// (base plan ∪ active add-ons) and the active billing period. An implementation translates the feature
    /// keys into concrete activations for the active period and revokes keys that are no longer entitled.
    /// <para>
    /// The default toolkit implementation lives in the adapter lib
    /// <c>ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity</c>, which maps each key onto a
    /// <c>TenantFeatureActivation</c> (start/end = billing period) and tracks its own grants in a
    /// <c>BillingFeatureGrant</c> side-table so it never touches manually-granted activations. Consumers
    /// without the toolkit tenant model can supply their own implementation.
    /// </para>
    /// </summary>
    public interface IFeatureProvisioner
    {
        /// <summary>
        /// Reconciles the tenant's billing-granted features to exactly <paramref name="featureKeys"/> for the
        /// window [<paramref name="periodStart"/>, <paramref name="periodEnd"/>]. An empty
        /// <paramref name="featureKeys"/> set revokes all billing-granted features for the tenant.
        /// </summary>
        /// <param name="tenantId">Logical tenant identifier (matches <see cref="Models.TenantSubscription.TenantId"/>).</param>
        /// <param name="featureKeys">Union of all entitled feature keys; empty to revoke.</param>
        /// <param name="periodStart">Start of the active billing period; null = open start.</param>
        /// <param name="periodEnd">End of the active billing period; null = open end.</param>
        Task SyncAsync(int tenantId, IReadOnlyCollection<string> featureKeys, DateTime? periodStart, DateTime? periodEnd, CancellationToken cancellationToken = default);
    }
}
