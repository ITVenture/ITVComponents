using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Authentication.OpenId.JWT;
using ITVComponents.WebCoreToolkit.Authentication.OpenId.Options;
using ITVComponents.WebCoreToolkit.Security.ClientApps;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Authentication.OpenId.Handlers
{
    /// <summary>Die Antwort des Token-Endpunkts.</summary>
    /// <param name="Token">der Bearer, ohne Praefix</param>
    /// <param name="ExpiresUtc">wann er ablaeuft - damit der Client rechtzeitig erneuern kann</param>
    public sealed record ClientAppTokenResult(string Token, DateTime ExpiresUtc);

    /// <summary>
    /// Tauscht einen Geraete-Schluessel gegen einen Bearer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Der Endpunkt, den es bisher an keinem Ende gab.</b> Das Toolkit kannte nur
    /// <c>/UserToken/Refresh</c> - also den Fall "ein Benutzer erneuert sein Token". Eine MASCHINE hatte
    /// keinen Weg, ueberhaupt an ein erstes zu kommen.
    /// </para>
    /// <para>
    /// <b>Kein Refresh-Token.</b> Das ist Absicht: das Geraet haelt ein langlebiges Geheimnis und kann
    /// jederzeit ein neues Token holen. Ein Refresh-Token waere ein zweites Geheimnis mit eigener Ablage,
    /// eigenem Widerruf und eigenem Ablauf - alles doppelt, ohne dass es etwas koennte, was der
    /// Geraeteschluessel nicht schon kann. Refresh-Token bleiben die Sache der
    /// BENUTZER-Delegation (<c>IApplicationTokenService</c>).
    /// </para>
    /// </remarks>
    public static class ClientAppTokenHandler
    {
        /// <summary>
        /// Nimmt den Schluessel aus <c>X-Api-Key</c> entgegen und gibt ein Token zurueck.
        /// </summary>
        public static async Task<IResult> IssueToken(HttpContext context,
            [FromServices] IClientAppAccessQuery accessQuery,
            [FromServices] IJwtService jwtService,
            [FromServices] IOptions<JwtGeneratorOptions> options,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger("ClientAppTokenHandler");
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var header)
                || string.IsNullOrWhiteSpace(header.ToString()))
            {
                logger.LogDebug("A token was requested without an X-Api-Key header.");
                return Results.Unauthorized();
            }

            var parts = header.ToString().Split('.', 3);
            if (parts.Length != 3)
            {
                logger.LogWarning(
                    "A token was requested with a key that does not have the shape '<clientKey>.<label>.<secret>'.");
                return Results.Unauthorized();
            }

            var access = await accessQuery.ResolveAsync(parts[0], parts[1], ct);
            if (access == null || !SecretHasher.Verify(parts[2], access.SecretHash))
            {
                // Nach aussen kein Unterschied zwischen "kennen wir nicht", "widerrufen" und "falsches
                // Geheimnis" - wer raet, soll daraus nichts lernen. Im Protokoll steht die Kennung.
                logger.LogWarning(
                    "A token was refused for client-key {ClientKey} and label {Label}: unknown, revoked, expired, switched off, or the secret does not match.",
                    parts[0], parts[1]);
                return Results.Unauthorized();
            }

            await accessQuery.MarkUsedAsync(access.ClientAppAccessId, ct);

            // Der Name allein traegt die Rechte NICHT - gewickelt wird allein der Zugangs-Anspruch, und
            // den baut ClientAppIdentity gemeinsam fuer alle Wege. Ohne ihn liefe das Geraet hier in
            // dieselbe Falle wie frueher der API-Schluessel-Weg: angemeldet, im Mandanten, rechtelos.
            var claims = new List<Claim> { new Claim(System.Security.Claims.ClaimTypes.Name, access.Label) };
            claims.AddRange(ClientAppIdentity.BuildClaims(parts[0], access, logger));
            var identity = new ClaimsIdentity(claims, "ClientApp");
            var principal = new ClaimsPrincipal(identity);

            var token = jwtService.GetJwtTokenFor(principal, parts[0]);
            var expires = DateTime.UtcNow.AddMinutes(options.Value.TokenDuration);

            logger.LogInformation(
                "A token was issued for client-app access {Label} in tenant {Tenant}; it expires {Expires}.",
                access.Label, access.TenantName, expires);
            return Results.Ok(new ClientAppTokenResult(token, expires));
        }
    }
}
