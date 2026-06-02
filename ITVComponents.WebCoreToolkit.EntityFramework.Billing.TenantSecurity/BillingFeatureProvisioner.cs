using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity
{
    /// <summary>
    /// Default <see cref="IFeatureProvisioner"/> for the toolkit tenant-security model. Reconciles a tenant's
    /// billing-granted features into <c>TenantFeatureActivation</c> rows whose activation window equals the
    /// billing period, and tracks ownership in <see cref="BillingFeatureGrant"/> so manually-granted
    /// activations are never touched.
    /// </summary>
    /// <typeparam name="TContext">The host <see cref="DbContext"/> (must map Features, the activation type and BillingFeatureGrant).</typeparam>
    /// <typeparam name="TTenant">The tenant model the activation is bound to.</typeparam>
    /// <typeparam name="TActivation">The concrete <c>TenantFeatureActivation</c> derivative (e.g. FlatTenantFeatureActivation).</typeparam>
    public class BillingFeatureProvisioner<TContext, TTenant, TActivation> : IFeatureProvisioner
        where TContext : DbContext
        where TTenant : Tenant
        where TActivation : TenantFeatureActivation<TTenant>, new()
    {
        private readonly TContext db;

        public BillingFeatureProvisioner(TContext db)
        {
            this.db = db;
        }

        /// <inheritdoc />
        public async Task SyncAsync(int tenantId, IReadOnlyCollection<string> featureKeys, DateTime? periodStart, DateTime? periodEnd, CancellationToken cancellationToken = default)
        {
            var keySet = new HashSet<string>(featureKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            // Billing-owned grants for this tenant (ignore the tenant global filter: we run outside an ambient
            // tenant scope, e.g. from a provider webhook, and address the tenant explicitly).
            var grants = await db.Set<BillingFeatureGrant>().IgnoreQueryFilters()
                .Where(g => g.TenantId == tenantId)
                .ToListAsync(cancellationToken);
            var grantByKey = grants.ToDictionary(g => g.FeatureKey, StringComparer.OrdinalIgnoreCase);

            // Resolve entitled keys against the feature catalog (by name). Unknown keys are skipped.
            var features = keySet.Count == 0
                ? new List<Feature>()
                : await db.Set<Feature>().IgnoreQueryFilters()
                    .Where(f => keySet.Contains(f.FeatureName))
                    .ToListAsync(cancellationToken);
            var featureByKey = features.ToDictionary(f => f.FeatureName, StringComparer.OrdinalIgnoreCase);

            var newGrants = new List<(TActivation activation, string key, int featureId)>();

            // 1) Add or refresh activations for currently-entitled keys.
            foreach (var key in keySet)
            {
                if (!featureByKey.TryGetValue(key, out var feature))
                {
                    // No matching feature in the catalog — nothing to activate.
                    continue;
                }

                if (grantByKey.TryGetValue(key, out var grant))
                {
                    var activation = await db.Set<TActivation>().IgnoreQueryFilters()
                        .FirstOrDefaultAsync(a => a.TenantFeatureActivationId == grant.TenantFeatureActivationId, cancellationToken);
                    if (activation != null)
                    {
                        activation.ActivationStart = periodStart;
                        activation.ActivationEnd = periodEnd;
                        continue;
                    }

                    // Grant points at an activation that no longer exists (manually removed): drop the stale
                    // grant and recreate below.
                    db.Set<BillingFeatureGrant>().Remove(grant);
                }

                var fresh = new TActivation
                {
                    TenantId = tenantId,
                    FeatureId = feature.FeatureId,
                    ActivationStart = periodStart,
                    ActivationEnd = periodEnd
                };
                db.Set<TActivation>().Add(fresh);
                newGrants.Add((fresh, key, feature.FeatureId));
            }

            // 2) Revoke activations for keys no longer entitled (only billing-owned ones).
            foreach (var grant in grants)
            {
                if (keySet.Contains(grant.FeatureKey))
                {
                    continue;
                }

                var activation = await db.Set<TActivation>().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a => a.TenantFeatureActivationId == grant.TenantFeatureActivationId, cancellationToken);
                if (activation != null)
                {
                    db.Set<TActivation>().Remove(activation);
                }

                db.Set<BillingFeatureGrant>().Remove(grant);
            }

            // Persist new activations (assigns their ids), then record the owning grants.
            await db.SaveChangesAsync(cancellationToken);

            foreach (var (activation, key, featureId) in newGrants)
            {
                db.Set<BillingFeatureGrant>().Add(new BillingFeatureGrant
                {
                    TenantId = tenantId,
                    FeatureKey = key,
                    FeatureId = featureId,
                    TenantFeatureActivationId = activation.TenantFeatureActivationId
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
