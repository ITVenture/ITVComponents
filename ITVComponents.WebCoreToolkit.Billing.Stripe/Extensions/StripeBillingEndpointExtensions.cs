using System.IO;
using System.Threading;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Extensions
{
    public static class StripeBillingEndpointExtensions
    {
        /// <summary>
        /// Maps the Stripe webhook receiver (default <c>POST /billing/webhook</c>). This endpoint must be
        /// reachable by Stripe and is intentionally unauthenticated — it verifies the event signature itself.
        /// Checkout and portal sessions are created server-side via <see cref="IStripeCheckoutSessionFactory"/>
        /// / <see cref="IStripeBillingPortalFactory"/> from the UI (redirect to the returned URL).
        /// </summary>
        public static IEndpointRouteBuilder MapStripeBillingEndpoints(this IEndpointRouteBuilder endpoints, string webhookPath = "/billing/webhook")
        {
            endpoints.MapPost(webhookPath, async (HttpRequest request, IStripeWebhookHandler handler, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signature = request.Headers["Stripe-Signature"].ToString();
                try
                {
                    await handler.HandleAsync(payload, signature, cancellationToken);
                    return Results.Ok();
                }
                catch (StripeException ex)
                {
                    // Almost always a signature-verification failure in EventUtility.ConstructEvent: the
                    // configured Billing:Stripe:WebhookSecret (whsec_...) does not match the secret of the
                    // endpoint that produced the event (a Stripe-CLI `stripe listen` secret differs from the
                    // dashboard endpoint secret), or a proxy stripped/rewrote the body or the Stripe-Signature
                    // header. Log it so it is diagnosable in the SystemLog — the 400 alone told nobody why.
                    LogEnvironment.LogEvent(
                        $"Stripe webhook rejected (HTTP 400). {(string.IsNullOrEmpty(signature) ? "No Stripe-Signature header was present. " : string.Empty)}"
                        + $"This is typically a WebhookSecret mismatch or a modified request body/signature. Error: {ex.Message}",
                        LogSeverity.Warning, "StripeWebhook");

                    // Signature mismatch / malformed payload — tell Stripe to retry rather than 200-and-drop.
                    return Results.BadRequest();
                }
            }).AllowAnonymous();

            return endpoints;
        }
    }
}
