using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions
{
    /// <summary>
    /// Decides whether a tenant is entitled to receive payments (feature <c>StripePayments</c>).
    /// <para>
    /// This is NOT the same check the views do. A view asks the current security scope; the sale service can be
    /// called from a background run or an anonymous shop request where there is no scope at all. It therefore
    /// asks by TENANT ID against the database — the same seam as <see cref="IFeatureProvisioner"/>, implemented
    /// in the tenant-security adapter library.
    /// </para>
    /// When no gate is registered the feature counts as NOT active: fail-closed, because the failure mode of the
    /// other choice is taking money you were not entitled to take a commission on.
    /// </summary>
    public interface IPaymentFeatureGate
    {
        /// <summary>True when the tenant currently holds the payments feature.</summary>
        Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default);
    }
}
