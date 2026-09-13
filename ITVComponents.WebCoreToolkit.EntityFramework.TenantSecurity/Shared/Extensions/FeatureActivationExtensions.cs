using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions
{
    /// <summary>
    /// Why a tenant does, or does not, hold a feature. The bool alone cannot say which of the three
    /// possible NOs it is, and the distinction is what a caller needs in its log: a missing catalogue
    /// entry is a deployment mistake, a missing activation is a business fact.
    /// </summary>
    public enum FeatureActivationState
    {
        /// <summary>There is no feature by that name in the catalogue at all.</summary>
        Unknown,

        /// <summary>The feature exists, is not on for everyone, and this tenant has no currently valid activation.</summary>
        NotActivatedForTenant,

        /// <summary>Not on for everyone, but this tenant has a currently valid activation.</summary>
        ActivatedForTenant,

        /// <summary>The catalogue row carries <c>Enabled</c>, which means the feature is on for EVERY tenant.</summary>
        EnabledForEveryone
    }

    /// <summary>
    /// The one definition of "does this tenant hold this feature".
    /// <para>
    /// <b>The rule:</b> <c>Features.Enabled</c> does NOT mean "the master switch is on" — it means
    /// <i>"this feature is on for EVERY tenant"</i>. A <c>TenantFeatureActivation</c> is the second,
    /// tenant-bound road to the same yes. The two are joined by OR, never by AND: a module that is sold
    /// per tenant is configured as <c>Enabled = false</c> plus one activation per buyer, and demanding
    /// both would make exactly that configuration impossible.
    /// </para>
    /// <para>
    /// Every reader of feature entitlement resolves it this way — <c>DbSecurityRepository.GetFeatures</c>
    /// in both the flat and the hierarchical flavour (and therefore <c>VerifyActivatedFeatures</c>,
    /// <c>SecureView</c>, the navigation builder and the shared-asset provider), and every scope-free
    /// caller through <see cref="GetFeatureStateForTenantAsync{TTenant,TActivation}"/>. Restating the
    /// rule somewhere else is how it drifts; change it here or nowhere.
    /// </para>
    /// <para>
    /// Activations are strictly tenant-local: they are NOT inherited along the tenant tree. The global
    /// query filter for activations is the same expression in the flat and the hierarchical model and is
    /// the only tenant-bound one without a <c>CurrentTenantTree</c> branch, so a child tenant does not
    /// see its parent's activation.
    /// </para>
    /// </summary>
    public static class FeatureActivationExtensions
    {
        /// <summary>
        /// Combines the two roads to a yes. Kept as a named method rather than an inline <c>||</c> so that
        /// every place that decides feature entitlement is one search away from every other.
        /// </summary>
        /// <param name="enabledForEveryone">the <c>Enabled</c> column of the catalogue row</param>
        /// <param name="tenantHasValidActivation">whether the tenant has an activation valid right now</param>
        /// <returns>a value indicating whether the feature is on for the tenant in question</returns>
        public static bool IsFeatureEnabled(bool enabledForEveryone, bool tenantHasValidActivation)
            => enabledForEveryone || tenantHasValidActivation;

        /// <summary>
        /// Indicates whether the given state is a yes. Both roads lead there; the state only remembers which.
        /// </summary>
        /// <param name="state">the resolved state</param>
        /// <returns>a value indicating whether the feature is on</returns>
        public static bool IsEnabled(this FeatureActivationState state)
            => state is FeatureActivationState.EnabledForEveryone or FeatureActivationState.ActivatedForTenant;

        /// <summary>
        /// Resolves a single feature for ONE tenant, addressed by id and explicitly NOT through the ambient
        /// security scope.
        /// <para>
        /// Query filters are ignored on purpose. A caller that names the tenant id has already decided which
        /// tenant it means — it runs from an anonymous request, a background job or a webhook where there is
        /// no scope at all, and letting the ambient filter join in would either yield nothing or, worse,
        /// answer silently for a different tenant. This is the same reason the provisioner writes these rows
        /// with <c>IgnoreQueryFilters</c>.
        /// </para>
        /// <para>
        /// The name is matched case-insensitively, like <c>VerifyActivatedFeatures</c> does in memory. A raw
        /// comparison survives SQL Server, whose default collation is case-insensitive, and then silently
        /// answers "no feature by that name" on PostgreSQL. The catalogue is a handful of rows, so losing the
        /// index seek costs nothing.
        /// </para>
        /// </summary>
        /// <typeparam name="TTenant">the tenant model the activation is bound to</typeparam>
        /// <typeparam name="TActivation">the concrete <c>TenantFeatureActivation</c> derivative of the host model</typeparam>
        /// <param name="db">a context that maps <see cref="Feature"/> and <typeparamref name="TActivation"/></param>
        /// <param name="featureName">the feature to resolve</param>
        /// <param name="tenantId">the tenant to resolve it for</param>
        /// <param name="moment">the point in time the activation window is measured against; defaults to now (UTC)</param>
        /// <param name="cancellationToken">a token to cancel the reads</param>
        /// <returns>the resolved state, including WHY it is a no</returns>
        public static async Task<FeatureActivationState> GetFeatureStateForTenantAsync<TTenant, TActivation>(
            this DbContext db, string featureName, int tenantId, DateTime? moment = null,
            CancellationToken cancellationToken = default)
            where TTenant : Tenant
            where TActivation : TenantFeatureActivation<TTenant>
        {
            var at = moment ?? DateTime.UtcNow;
            var lowered = featureName?.ToLower();
            var feature = await db.Set<Feature>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.FeatureName.ToLower() == lowered, cancellationToken);
            if (feature == null)
            {
                return FeatureActivationState.Unknown;
            }

            if (feature.Enabled)
            {
                return FeatureActivationState.EnabledForEveryone;
            }

            var activated = await db.Set<TActivation>().IgnoreQueryFilters()
                .AnyAsync(a => a.TenantId == tenantId
                               && a.FeatureId == feature.FeatureId
                               && (a.ActivationStart == null || a.ActivationStart <= at)
                               && (a.ActivationEnd == null || a.ActivationEnd >= at), cancellationToken);

            return activated
                ? FeatureActivationState.ActivatedForTenant
                : FeatureActivationState.NotActivatedForTenant;
        }

        /// <summary>
        /// The plain yes/no of <see cref="GetFeatureStateForTenantAsync{TTenant,TActivation}"/>, for callers
        /// that have nothing to say about the reason.
        /// </summary>
        /// <typeparam name="TTenant">the tenant model the activation is bound to</typeparam>
        /// <typeparam name="TActivation">the concrete <c>TenantFeatureActivation</c> derivative of the host model</typeparam>
        /// <param name="db">a context that maps <see cref="Feature"/> and <typeparamref name="TActivation"/></param>
        /// <param name="featureName">the feature to resolve</param>
        /// <param name="tenantId">the tenant to resolve it for</param>
        /// <param name="moment">the point in time the activation window is measured against; defaults to now (UTC)</param>
        /// <param name="cancellationToken">a token to cancel the reads</param>
        /// <returns>a value indicating whether the feature is on for that tenant</returns>
        public static async Task<bool> IsFeatureEnabledForTenantAsync<TTenant, TActivation>(
            this DbContext db, string featureName, int tenantId, DateTime? moment = null,
            CancellationToken cancellationToken = default)
            where TTenant : Tenant
            where TActivation : TenantFeatureActivation<TTenant>
            => (await db.GetFeatureStateForTenantAsync<TTenant, TActivation>(featureName, tenantId, moment,
                cancellationToken)).IsEnabled();
    }
}
