using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Extensions
{
    /// <summary>
    /// Hängt den Empfänger für Payrexx-Benachrichtigungen ein.
    /// </summary>
    public static class PayrexxEndpointExtensions
    {
        /// <summary>
        /// Legt den Endpunkt an, den Payrexx ruft, wenn sich der Zustand einer Transaktion ändert.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Die Adresse muss im Payrexx-Portal unter „Webhooks" hinterlegt werden und <b>von aussen
        /// erreichbar</b> sein. Sie ist anonym — Payrexx meldet sich nicht an; wer die Meldung geschickt
        /// hat, sagt allein die Signatur.
        /// </para>
        /// <para>
        /// <b>Innerhalb von 20 Sekunden</b> muss eine Antwort stehen, sonst gilt der Versuch als
        /// gescheitert. Ein Beobachter, der lange arbeitet, gehört deshalb hinter eine Warteschlange und
        /// nicht in den Aufrufpfad.
        /// </para>
        /// </remarks>
        /// <param name="endpoints">die Routen-Sammlung</param>
        /// <param name="webhookPath">der Pfad, den Payrexx ruft</param>
        public static IEndpointRouteBuilder MapPayrexxWebhook<TContext>(this IEndpointRouteBuilder endpoints,
            string webhookPath = "/billing/payrexx/webhook")
            where TContext : DbContext, IPaymentsContext
        {
            endpoints.MapPost(webhookPath, async (HttpRequest request, PayrexxWebhookHandler<TContext> handler,
                CancellationToken cancellationToken) =>
            {
                // Der ROHE Rumpf, ungeparst: ueber genau diese Zeichen laeuft die Signatur. Wer ihn erst
                // durch die Modellbindung schickt und danach wieder zusammensetzt, prueft eine andere
                // Zeichenfolge und bekommt eine Abweichung, die nach einem falschen Schluessel aussieht.
                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signature = request.Headers["X-Webhook-Signature"].ToString();

                try
                {
                    await handler.HandleAsync(payload, signature, request.ContentType, cancellationToken);
                    return Results.Ok();
                }
                catch (PayrexxWebhookRejectedException ex)
                {
                    LogEnvironment.LogEvent(
                        $"A Payrexx notification was rejected (HTTP 400). Payrexx will retry. Reason: {ex.OutlineException()}",
                        LogSeverity.Warning, PayrexxApiClient.LogContext);

                    // Ablehnen, nicht bestaetigen: Payrexx wiederholt bis zu zehnmal ueber mehrere Tage.
                    // Eine bestaetigte und verworfene Meldung waere dagegen endgueltig weg, und mit ihr
                    // die Nachricht, dass bezahlt wurde.
                    return Results.BadRequest();
                }
                catch (Exception ex)
                {
                    // Auch hier ablehnen. Der Unterschied zum Fall darueber: dort war die Meldung falsch,
                    // hier waren WIR es - und genau dann ist die Wiederholung die Rettung.
                    LogEnvironment.LogEvent(
                        $"A Payrexx notification could not be processed (HTTP 500). Payrexx will retry: {ex.OutlineException()}",
                        LogSeverity.Error, PayrexxApiClient.LogContext);
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }
            }).AllowAnonymous();

            return endpoints;
        }
    }
}
