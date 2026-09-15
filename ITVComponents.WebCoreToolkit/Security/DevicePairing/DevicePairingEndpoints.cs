using System;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Security.DevicePairing
{
    /// <summary>Was das Geraet beim Anlegen schickt.</summary>
    public class StartPairingModel
    {
        /// <summary>die oeffentliche Kennung der Anwendung</summary>
        public string ClientKey { get; set; }

        /// <summary>wie sich das Geraet nennt</summary>
        public string DeviceLabel { get; set; }
    }

    /// <summary>Was das Geraet beim Nachfragen schickt.</summary>
    public class PollPairingModel
    {
        /// <summary>der Geraetecode aus dem Anlegen</summary>
        public string DeviceCode { get; set; }
    }

    /// <summary>Was die Maske beim Bestaetigen schickt.</summary>
    public class ConfirmPairingModel
    {
        /// <summary>der abgetippte Benutzercode</summary>
        public string UserCode { get; set; }
    }

    /// <summary>
    /// Die Endpunkte des Kopplungs-Ablaufs.
    /// </summary>
    public static class DevicePairingEndpoints
    {
        /// <summary>
        /// Haengt die vier Endpunkte der Geraete-Kopplung ein.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b><c>start</c> und <c>poll</c> laufen anonym</b> - das Geraet hat noch keine Identitaet, das
        /// ist ja der Punkt der Uebung. Ihre Absicherung liegt woanders: <c>start</c> braucht eine
        /// gueltige Anwendungs-Kennung, <c>poll</c> den Geraetecode, der selbst das Geheimnis ist, und
        /// dazu kommen Ablauf und Abfragebremse.
        /// </para>
        /// <para>
        /// <b><c>describe</c> und <c>confirm</c> verlangen ein Recht</b> (aus
        /// <see cref="DevicePairingOptions.ConfirmPermission"/>) und den Mandanten der Anwendung - die
        /// Pruefung der Mandantengrenze macht der Dienst.
        /// </para>
        /// </remarks>
        /// <param name="builder">die Anwendung, in die eingehaengt wird</param>
        /// <param name="prefix">der Pfad-Praefix; Vorgabe <c>/DevicePairing</c></param>
        /// <returns>die Anwendung</returns>
        public static WebApplication UseDevicePairingEndpoints(this WebApplication builder,
            string prefix = "/DevicePairing")
        {
            builder.MapPost($"{prefix}/start", StartAsync)
                .AllowAnonymous()
                .Accepts<StartPairingModel>("application/json")
                .Produces<PairingRequest>(contentType: "application/json");

            builder.MapPost($"{prefix}/poll", PollAsync)
                .AllowAnonymous()
                .Accepts<PollPairingModel>("application/json")
                .Produces<PairingResult>(contentType: "application/json");

            builder.MapPost($"{prefix}/describe", DescribeAsync)
                .Accepts<ConfirmPairingModel>("application/json")
                .Produces<PairingPreview>(contentType: "application/json");

            builder.MapPost($"{prefix}/confirm", ConfirmAsync)
                .Accepts<ConfirmPairingModel>("application/json")
                .Produces<PairingConfirmation>(contentType: "application/json");

            return builder;
        }

        private static async Task<IResult> StartAsync([FromBody] StartPairingModel model,
            [FromServices] IDevicePairingService pairing, CancellationToken ct)
        {
            var result = await pairing.StartAsync(model?.ClientKey, model?.DeviceLabel, ct);
            // Kein Unterschied nach aussen zwischen "gibt es nicht" und "abgeschaltet": wer raet, soll
            // nicht erfahren, welche Kennungen existieren. Im Protokoll steht der Grund.
            return result == null ? Results.NotFound() : Results.Ok(result);
        }

        private static async Task<IResult> PollAsync([FromBody] PollPairingModel model,
            [FromServices] IDevicePairingService pairing, CancellationToken ct)
        {
            var result = await pairing.PollAsync(model?.DeviceCode, ct);
            return Results.Ok(result);
        }

        private static async Task<IResult> DescribeAsync([FromBody] ConfirmPairingModel model,
            [FromServices] IDevicePairingService pairing,
            [FromServices] IServiceProvider services, CancellationToken ct)
        {
            if (!MayConfirm(services, out var permission))
            {
                return Forbid(services, permission, "describe");
            }

            return Results.Ok(await pairing.DescribeAsync(model?.UserCode, ct));
        }

        private static async Task<IResult> ConfirmAsync([FromBody] ConfirmPairingModel model,
            [FromServices] IDevicePairingService pairing,
            [FromServices] IServiceProvider services, CancellationToken ct)
        {
            if (!MayConfirm(services, out var permission))
            {
                return Forbid(services, permission, "confirm");
            }

            var result = await pairing.ConfirmAsync(model?.UserCode, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }

        private static bool MayConfirm(IServiceProvider services, out string permission)
        {
            permission = services.GetService<IGlobalSettings<DevicePairingOptions>>()?.Value?.ConfirmPermission
                         ?? new DevicePairingOptions().ConfirmPermission;
            return services.VerifyUserPermissions(new[] { permission });
        }

        private static IResult Forbid(IServiceProvider services, string permission, string what)
        {
            // Eine Abweisung ohne Spur ist genau die Sorte Fehler, die als "die Maske tut nichts"
            // gemeldet wird.
            services.GetService<ILoggerFactory>()?.CreateLogger("DevicePairingEndpoints")
                .LogWarning("A pairing {Operation} was refused: the permission {Permission} is missing.",
                    what, permission);
            return Results.Forbid();
        }
    }
}
