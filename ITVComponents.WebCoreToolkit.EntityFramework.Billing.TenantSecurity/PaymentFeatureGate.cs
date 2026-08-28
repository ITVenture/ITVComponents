using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity
{
    /// <summary>
    /// Default <see cref="IPaymentFeatureGate"/> for the toolkit tenant-security model: answers whether a tenant
    /// currently holds the payments feature by asking the database for an active
    /// <c>TenantFeatureActivation</c>.
    /// <para>
    /// By TENANT ID and explicitly NOT through the ambient security scope. The sale service is called from
    /// anonymous shop requests and background runs where there is no scope at all, and a scope-based check would
    /// then either fail or, worse, silently answer for the wrong tenant.
    /// </para>
    /// <para>
    /// Nothing special has to happen for a subscription to switch this on: a plan or add-on carrying the feature
    /// key runs through the existing <see cref="BillingFeatureProvisioner{TContext,TTenant,TActivation}"/> and
    /// produces the activation this gate reads. Axis A unlocks axis B without a line of extra logic.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The host <see cref="DbContext"/> (must map Features and the activation type).</typeparam>
    /// <typeparam name="TTenant">The tenant model the activation is bound to.</typeparam>
    /// <typeparam name="TActivation">The concrete <c>TenantFeatureActivation</c> derivative.</typeparam>
    public class PaymentFeatureGate<TContext, TTenant, TActivation> : IPaymentFeatureGate
        where TContext : DbContext
        where TTenant : Tenant
        where TActivation : TenantFeatureActivation<TTenant>, new()
    {
        /// <summary>The feature key that unlocks receiving payments from end customers.</summary>
        public const string FeatureKey = "StripePayments";

        private readonly IToolkitContextFactory contextFactory;

        public PaymentFeatureGate(IToolkitContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        /// <inheritdoc />
        public async Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            using var lease = contextFactory.Lease<TContext>();
            var db = lease.Context;
            var now = DateTime.UtcNow;

            // Query filters are ignored on purpose: we run outside any ambient tenant scope and address the
            // tenant explicitly, exactly like the provisioner that wrote these rows.
            var feature = await db.Set<Feature>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.FeatureName == FeatureKey, cancellationToken);
            if (feature is not { Enabled: true })
            {
                return false;
            }

            return await db.Set<TActivation>().IgnoreQueryFilters()
                .AnyAsync(a => a.TenantId == tenantId
                               && a.FeatureId == feature.FeatureId
                               && (a.ActivationStart == null || a.ActivationStart <= now)
                               && (a.ActivationEnd == null || a.ActivationEnd >= now), cancellationToken);
        }
    }
}
