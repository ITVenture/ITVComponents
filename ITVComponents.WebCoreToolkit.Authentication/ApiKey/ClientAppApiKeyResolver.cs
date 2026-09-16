using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Authentication.ApiKey.Models;
using ITVComponents.WebCoreToolkit.Security.ClientApps;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Authentication.ApiKey
{
    /// <summary>
    /// Loest einen API-Schluessel gegen die <b>ClientApp-Zugaenge</b> auf - gehasht, mit Mandant, Ablauf
    /// und Widerruf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Alternative zu <see cref="DefaultApiKeyUserResolver"/>, der den Schluessel im <b>Klartext</b>
    /// gegen <c>Users.UserName</c> vergleicht. Fuer einen API-Schluessel ist das dieselbe Klasse von
    /// Angriffsflaeche wie ein Klartextpasswort - nur faellt es nicht auf, weil niemand ihn je tippt.
    /// </para>
    /// <para>
    /// <b>Aufbau des Schluessels:</b> <c>&lt;ClientKey&gt;.&lt;Label&gt;.&lt;Geheimnis&gt;</c>. Die ersten
    /// beiden Teile sind indizierte Nachschlagefelder, der dritte wird gegen den gespeicherten Hash
    /// geprueft. Der Klartext steht nirgends.
    /// </para>
    /// <para>
    /// <b>Registrierung:</b> ueber <c>UseClientAppApiKeyResolver()</c>, und zwar <b>nach</b> der
    /// WebPart-Konfiguration. <c>UseDefaultApiKeyResolver()</c> wird aus <c>WebPartInit</c> unbedingt
    /// gerufen, sobald ein Host API-Key-Auth konfiguriert - per <c>AddTransient</c>, nicht <c>TryAdd</c>.
    /// Wer frueher registriert, wird still ueberschrieben.
    /// </para>
    /// </remarks>
    public class ClientAppApiKeyResolver : IGetApiKeyQuery
    {
        private readonly IClientAppAccessQuery accessQuery;
        private readonly ILogger<ClientAppApiKeyResolver> logger;

        public ClientAppApiKeyResolver(IClientAppAccessQuery accessQuery, ILogger<ClientAppApiKeyResolver> logger)
        {
            this.accessQuery = accessQuery;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ApiKeyInfo> Execute(string providedApiKey, string authenticationScheme)
        {
            if (string.IsNullOrWhiteSpace(providedApiKey))
            {
                return null;
            }

            // Dreiteilig, und das Geheimnis ist der REST - nicht der dritte Teil. Base64Url enthaelt zwar
            // keinen Punkt, aber sich darauf zu verlassen hiesse, das Format der Schluesselerzeugung in
            // dieser Klasse mitzufuehren.
            var parts = providedApiKey.Split('.', 3);
            if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0])
                                  || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
            {
                // Sehr wahrscheinlich ein Schluessel fuer einen ANDEREN Resolver - kein Grund fuer eine
                // Fehlermeldung, aber eine Spur, wenn jemand sucht, warum sein Geraet nicht hereinkommt.
                logger.LogDebug(
                    "An api-key was presented that does not have the client-app shape '<clientKey>.<label>.<secret>'; this resolver does not handle it.");
                return null;
            }

            var access = await accessQuery.ResolveAsync(parts[0], parts[1]);
            if (access == null)
            {
                // Bewusst ohne Unterscheidung nach aussen: wer raet, soll nicht erfahren, ob die Kennung
                // existiert. Im Protokoll steht sie, damit eine echte Fehlersuche moeglich bleibt.
                logger.LogWarning(
                    "No valid client-app access for client-key {ClientKey} and label {Label} - unknown, revoked, expired, or the application is switched off.",
                    parts[0], parts[1]);
                return null;
            }

            if (!SecretHasher.Verify(parts[2], access.SecretHash))
            {
                logger.LogWarning(
                    "The secret presented for client-app access {Label} (client-key {ClientKey}) does not match.",
                    parts[1], parts[0]);
                return null;
            }

            await accessQuery.MarkUsedAsync(access.ClientAppAccessId);

            // GEMEINSAM mit dem Maschinen-Token gebaut. Diese Stelle hat die Ansprueche einmal selbst
            // zusammengesetzt und dabei den Zugangs-Anspruch vergessen - das Geraet kam herein, bekam
            // seinen Mandanten und hatte KEINE EINZIGE Berechtigung. Wer hier etwas ergaenzt, ergaenzt
            // es in ClientAppIdentity, damit der Token-Weg es mitbekommt.
            var claims = ClientAppIdentity.BuildClaims(parts[0], access, logger);

            logger.LogDebug("Client-app access {Label} authenticated for tenant {Tenant} (machine: {IsMachine}).",
                access.Label, access.TenantName, access.IsMachine);

            // Der GEFUNDENE Bezeichner, nicht der vorgelegte Schluessel - er wird als ClaimTypes.Name
            // gesetzt. Die RECHTE haengen dagegen am Zugangs-Anspruch oben, nicht am Namen.
            return new ApiKeyInfo(access.Label, DateTime.UtcNow, claims);
        }
    }
}
