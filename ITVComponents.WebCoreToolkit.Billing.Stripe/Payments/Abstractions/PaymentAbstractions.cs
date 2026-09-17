using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Abstractions
{
    /// <summary>
    /// Verifies and processes events of the CONNECT webhook endpoint (its own endpoint with its own signing
    /// secret — see the endpoint extension).
    /// </summary>
    public interface IStripeConnectWebhookHandler
    {
        Task HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);
    }

}
