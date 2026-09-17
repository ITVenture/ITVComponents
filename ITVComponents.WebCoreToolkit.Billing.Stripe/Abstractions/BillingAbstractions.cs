using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions
{
    /// <summary>Verifies and processes Stripe webhook events into the local subscription mirror + feature provisioning.</summary>
    public interface IStripeWebhookHandler
    {
        /// <summary>
        /// Verifies the signature and processes the event payload. Updates the local
        /// <c>TenantSubscription</c> + items and reconciles the tenant's entitled features.
        /// </summary>
        Task HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);
    }

}
