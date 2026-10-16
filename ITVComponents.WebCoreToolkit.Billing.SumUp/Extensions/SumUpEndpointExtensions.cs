using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Extensions
{
    /// <summary>
    /// Hängt den Empfänger für SumUp-Meldungen ein.
    /// </summary>
    public static class SumUpEndpointExtensions
    {
        /// <summary>
        /// Legt den Endpunkt an, den SumUp ruft, wenn ein Geräte-Vorgang zu Ende ist.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Die volle, <b>von aussen erreichbare HTTPS-Adresse</b> gehört nach <c>SumUpPayments:WebhookUrl</c>: SumUp
        /// bekommt sie mit jedem Checkout (<c>return_url</c>), im Dashboard wird nichts eingetragen. Der Endpunkt
        /// ist anonym - SumUp meldet sich nicht an und signiert nicht; darum wird der Inhalt nicht geglaubt,
        /// sondern nachgefragt.
        /// </para>
        /// <para>
        /// Ablehnen statt bestätigen, wenn der Verkauf (noch) nicht zu finden ist oder etwas schiefging: SumUp
        /// wiederholt bei jeder Antwort ausser 200.
        /// </para>
        /// </remarks>
        public static IEndpointRouteBuilder MapSumUpWebhook<TContext>(this IEndpointRouteBuilder endpoints,
            string webhookPath = "/billing/sumup/webhook")
            where TContext : DbContext, IPaymentsContext
        {
            endpoints.MapPost(webhookPath, async (HttpRequest request, SumUpWebhookHandler<TContext> handler,
                CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(cancellationToken);

                try
                {
                    return await handler.HandleAsync(body, cancellationToken) switch
                    {
                        SumUpWebhookOutcome.Processed => Results.Ok(),
                        SumUpWebhookOutcome.UnknownSale => Results.NotFound(),
                        _ => Results.BadRequest()
                    };
                }
                catch (Exception ex)
                {
                    // Ablehnen: hier waren WIR es, und die Wiederholung von SumUp ist die Rettung.
                    LogEnvironment.LogEvent(
                        $"A SumUp notification could not be processed (HTTP 500). SumUp will retry: {ex.OutlineException()}",
                        LogSeverity.Error, SumUpApiClient.LogContext);
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }
            }).AllowAnonymous();

            return endpoints;
        }
    }
}
