using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions
{
    /// <summary>
    /// Hands the payments branch what the rest of the application already knows about a tenant, so the provider
    /// can be told instead of the tenant being asked again.
    /// <para>
    /// An abstraction rather than a direct read, for the same reason <c>IPaymentFeatureGate</c> is one: the
    /// answer lives in the onboarding model, and the payments branch stays free of it. The implementation that
    /// reads a billing profile sits in <c>EntityFramework.Billing.TenantSecurity</c>, where onboarding and
    /// billing already meet.
    /// </para>
    /// <para>
    /// Registering one is OPTIONAL and nothing depends on it: without a provider the account is created from
    /// what the payout tab holds, exactly as before, and the provider's hosted form asks for the rest.
    /// </para>
    /// </summary>
    public interface ITenantIdentityProvider
    {
        /// <summary>
        /// What is known about this tenant, or null when nothing is. Must not throw for an unknown tenant -
        /// creating a payout account has to stay possible for a tenant that has no billing profile at all.
        /// </summary>
        Task<TenantIdentity?> GetIdentityAsync(int tenantId, CancellationToken cancellationToken = default);
    }
}
