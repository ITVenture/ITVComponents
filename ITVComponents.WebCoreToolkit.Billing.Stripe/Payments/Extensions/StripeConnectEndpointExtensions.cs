using System.IO;
using System.Threading;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Extensions
{
    public static class StripeConnectEndpointExtensions
    {
        /// <summary>Default path of the payout-account page the return endpoints redirect to.</summary>
        public const string DefaultManagePath = "/Account/Manage/Payments";

        /// <summary>
        /// Maps the connect webhook receiver plus the two endpoints the provider sends the tenant back to after
        /// the hosted onboarding.
        /// <para>
        /// A SECOND webhook endpoint with its own signing secret, next to the platform one: the provider treats
        /// events on connected accounts as a separate subscription with a separate secret, and folding them into
        /// one endpoint would mean trying both secrets on every request and never knowing which was meant.
        /// </para>
        /// </summary>
        /// <param name="endpoints">The route builder.</param>
        /// <param name="webhookPath">Path of the connect webhook. Must be reachable by the provider.</param>
        /// <param name="returnPath">Path the provider returns the tenant to after the onboarding.</param>
        /// <param name="refreshPath">Path the provider calls when the onboarding link has expired.</param>
        /// <param name="managePath">The payout-account page both return endpoints redirect to.</param>
        public static IEndpointRouteBuilder MapStripeConnectEndpoints(this IEndpointRouteBuilder endpoints,
            string webhookPath = "/billing/connect/webhook",
            string returnPath = "/billing/connect/return",
            string refreshPath = "/billing/connect/refresh",
            string managePath = DefaultManagePath)
        {
            endpoints.MapPost(webhookPath, async (HttpRequest request, IStripeConnectWebhookHandler handler, CancellationToken cancellationToken) =>
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
                    // Nearly always a signature mismatch: the CONNECT endpoint has its own whsec_ and it is a
                    // different one from the platform endpoint's. Configuring the platform secret here is the
                    // single most likely mistake, so the message says so instead of leaving a bare 400.
                    LogEnvironment.LogEvent(
                        $"Stripe connect webhook rejected (HTTP 400). {(string.IsNullOrEmpty(signature) ? "No Stripe-Signature header was present. " : string.Empty)}"
                        + $"Note that StripePayments:ConnectWebhookSecret is a DIFFERENT secret from the platform webhook secret. Error: {ex.Message}",
                        LogSeverity.Warning, "StripeConnect");

                    // Tell the provider to retry rather than accepting and dropping the event.
                    return Results.BadRequest();
                }
            }).AllowAnonymous();

            // Both return paths only redirect. They deliberately do NOT create a new onboarding link themselves:
            // the provider calls them in the tenant's browser without any proof of who that is, and an endpoint
            // that hands out an onboarding link for a tenant id from the query string would hand out access to
            // someone else's payout account. The page behind it is authenticated and tenant-scoped, and asks
            // for a fresh link from there.
            endpoints.MapGet(returnPath, (HttpContext context) => Results.Redirect(TenantAware(context, managePath, "return")))
                .AllowAnonymous();

            endpoints.MapGet(refreshPath, (HttpContext context) => Results.Redirect(TenantAware(context, managePath, "refresh")))
                .AllowAnonymous();

            return endpoints;
        }

        /// <summary>
        /// Builds the redirect target INSIDE the current tenant. The tenant prefix middleware moves the tenant
        /// segment into <c>PathBase</c> before routing, so a root-absolute path would land the tenant in the
        /// wrong tenant — the same trap as BUG-PRE187 and the tenant URL guard.
        /// </summary>
        private static string TenantAware(HttpContext context, string managePath, string marker)
        {
            var basePath = context.Request.PathBase.HasValue ? context.Request.PathBase.Value!.TrimEnd('/') : string.Empty;
            var path = managePath.StartsWith('/') ? managePath : "/" + managePath;
            return $"{basePath}{path}?connect={marker}";
        }
    }
}
