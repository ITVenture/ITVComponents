using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Extensions
{
    /// <summary>
    /// Hängt den Empfänger für wallee-Zustandsmeldungen ein.
    /// </summary>
    public static class WalleeEndpointExtensions
    {
        /// <summary>
        /// Legt den Endpunkt an, den ein wallee-Webhook-Listener ruft.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Im Portal von wallee braucht es dazu <b>zwei Einträge</b>: eine Webhook-URL mit dieser Adresse
        /// und je einen Listener darauf — einen für <c>Transaction</c>, einen für <c>Refund</c>. Fehlt der
        /// zweite, kommen Erstattungen, die im Portal ausgelöst wurden, nie in den lokalen Büchern an.
        /// </para>
        /// <para>
        /// Am Listener sollte „payload signing and state" eingeschaltet sein. Das ist beides zugleich:
        /// die Signatur, ohne die der Endpunkt jedem glauben müsste, und der Zustand, der je Meldung
        /// einen Rückruf zu wallee spart.
        /// </para>
        /// </remarks>
        /// <param name="endpoints">die Routen-Sammlung</param>
        /// <param name="webhookPath">der Pfad, den wallee ruft</param>
        public static IEndpointRouteBuilder MapWalleeWebhook<TContext>(this IEndpointRouteBuilder endpoints,
            string webhookPath = "/billing/wallee/webhook")
            where TContext : DbContext, IPaymentsContext
        {
            endpoints.MapPost(webhookPath, async (HttpRequest request, WalleeWebhookHandler<TContext> handler,
                CancellationToken cancellationToken) =>
            {
                // Der ROHE Rumpf: ueber genau diese Zeichen laeuft die Signatur.
                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signature = request.Headers["x-signature"].ToString();

                try
                {
                    await handler.HandleAsync(payload, signature, cancellationToken);
                    return Results.Ok();
                }
                catch (WalleeWebhookRejectedException ex)
                {
                    LogEnvironment.LogEvent(
                        $"A wallee notification was rejected (HTTP 400). wallee will retry. Reason: {ex.OutlineException()}",
                        LogSeverity.Warning, WalleeRuntime.LogContext);

                    // Ablehnen statt bestaetigen: eine angenommene und verworfene Meldung waere endgueltig
                    // weg, und mit ihr die Nachricht, dass bezahlt wurde.
                    return Results.BadRequest();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"A wallee notification could not be processed (HTTP 500). wallee will retry: {ex.OutlineException()}",
                        LogSeverity.Error, WalleeRuntime.LogContext);
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }
            }).AllowAnonymous();

            // Das eigene wallee-Konto eines Mandanten meldet an seine eigene Adresse: {webhookPath}/{Kennung seines
            // Zugangs}. Webhook-URL und die beiden Listener legt der Mandant in SEINEM Space an; am Listener muss
            // "payload signing" an sein - hier wird immer geprueft.
            endpoints.MapPost($"{webhookPath.TrimEnd('/')}/{{accessId:int}}", async (int accessId, HttpRequest request,
                WalleeWebhookHandler<TContext> handler, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signature = request.Headers["x-signature"].ToString();

                try
                {
                    await handler.HandleOwnAsync(accessId, payload, signature, cancellationToken);
                    return Results.Ok();
                }
                catch (WalleeWebhookRejectedException ex)
                {
                    LogEnvironment.LogEvent(
                        $"A wallee notification for the own access {accessId} was rejected (HTTP 400). wallee will retry. Reason: {ex.OutlineException()}",
                        LogSeverity.Warning, WalleeRuntime.LogContext);
                    return Results.BadRequest();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"A wallee notification for the own access {accessId} could not be processed (HTTP 500). wallee will retry: {ex.OutlineException()}",
                        LogSeverity.Error, WalleeRuntime.LogContext);
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }
            }).AllowAnonymous();

            return endpoints;
        }
    }
}
