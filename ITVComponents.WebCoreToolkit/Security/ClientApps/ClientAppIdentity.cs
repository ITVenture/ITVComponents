using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Security.ClientApps
{
    /// <summary>
    /// Baut die Ansprueche, die eine <b>Anwendungs-Identitaet</b> ausmachen - fuer alle Wege, die eine
    /// solche Identitaet herstellen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es gibt mehr als einen Weg, an dem sich eine Anwendung anmeldet (Maschinen-Token ueber
    /// <c>JwtTokenService</c>, API-Schluessel ueber <c>ClientAppApiKeyResolver</c>), aber nur <b>eine</b>
    /// Identitaet dahinter. Dass diese Wege die Ansprueche einzeln zusammengesetzt haben, war die Ursache
    /// eines Fehlers, der teuer zu finden war: der API-Schluessel-Weg setzte
    /// <see cref="ITVComponents.WebCoreToolkit.ClaimTypes.ClientAppAccess"/> nicht, und damit hatte jedes
    /// per <c>X-Api-Key</c> angemeldete Geraet seinen Mandanten, aber <b>keine einzige Berechtigung</b> -
    /// ohne Fehler, ohne Hinweis, mit erfolgreicher Anmeldung im Protokoll.
    /// </para>
    /// <para>
    /// <b>Warum der Zugangs-Anspruch die Rechte traegt:</b> die Benutzer-Mapper
    /// (<c>SimpleUserNameMapper</c>, <c>User2GroupsMapper</c>) wickeln <b>ausschliesslich</b> aus diesem
    /// Anspruch das Label <c>##APPUSER##&lt;Label&gt;#</c>, und <c>DbSecurityRepository</c> erkennt einen
    /// Maschinen-Zugang <b>ausschliesslich</b> an dieser Wicklung. Der Bezeichner als
    /// <c>ClaimTypes.Name</c> reicht dafuer <b>nicht</b> - der Leseweg schaut dort nicht hin.
    /// </para>
    /// <para>
    /// Wer einen weiteren Anmeldeweg baut, ruft diese Stelle. Sonst entsteht dieselbe Abweichung neu.
    /// </para>
    /// </remarks>
    public static class ClientAppIdentity
    {
        /// <summary>
        /// Baut die Ansprueche fuer einen aufgeloesten Zugang.
        /// </summary>
        /// <param name="clientKey">die oeffentliche Kennung der Anwendung</param>
        /// <param name="access">der bereits geprüfte Zugang</param>
        /// <param name="logger">ein Protokollant, optional</param>
        /// <returns>die Ansprueche der Anwendungs-Identitaet</returns>
        public static List<Claim> BuildClaims(string clientKey, ClientAppAccessInfo access, ILogger logger = null)
        {
            return BuildClaims(clientKey, access?.Label, access?.TenantName, logger);
        }

        /// <summary>
        /// Baut die Ansprueche aus den Einzelteilen - fuer Wege, die keinen
        /// <see cref="ClientAppAccessInfo"/> in der Hand haben.
        /// </summary>
        /// <param name="clientKey">die oeffentliche Kennung der Anwendung</param>
        /// <param name="accessLabel">
        /// der Bezeichner des Zugangs. <b>Dieser Wert traegt die Rechte</b> - fehlt er, hat die Identitaet
        /// keine.
        /// </param>
        /// <param name="userScope">der Mandant, in dem die Identitaet laeuft</param>
        /// <param name="logger">ein Protokollant, optional</param>
        /// <returns>die Ansprueche der Anwendungs-Identitaet</returns>
        public static List<Claim> BuildClaims(string clientKey, string accessLabel, string userScope,
            ILogger logger = null)
        {
            // Voll ausgeschrieben: ITVComponents.WebCoreToolkit.ClaimTypes verdeckt hier die gleichnamige
            // Klasse aus System.Security.Claims, und der Fehler, den die Verwechslung ausloest, zeigt in
            // die falsche Richtung.
            var retVal = new List<Claim>();
            if (!string.IsNullOrEmpty(userScope))
            {
                retVal.Add(new Claim(WebCoreToolkit.ClaimTypes.FixedUserScope, userScope));
            }
            else
            {
                // Ohne Mandant laeuft die Identitaet mandantenlos und sieht die Daten keines Ladens. Das
                // ist kein Abbruch - aber es ist nie beabsichtigt.
                logger?.LogWarning(
                    "No tenant scope was resolved for client-app {ClientKey} (access {Label}); the identity will run without a tenant.",
                    clientKey, accessLabel);
            }

            if (!string.IsNullOrEmpty(clientKey))
            {
                retVal.Add(new Claim(WebCoreToolkit.ClaimTypes.ClientAppId, clientKey));
            }

            if (!string.IsNullOrEmpty(accessLabel))
            {
                retVal.Add(new Claim(WebCoreToolkit.ClaimTypes.ClientAppAccess, accessLabel));
            }
            else
            {
                // new Claim(type, null) WIRFT - deshalb die Pruefung. Wichtiger ist aber die Meldung: eine
                // Identitaet ohne diesen Anspruch ist RECHTELOS, und das faellt sonst erst beim ersten
                // verweigerten Zugriff auf, wo es wie ein Datenproblem aussieht.
                logger?.LogWarning(
                    "No client-app access label was resolved for application {ClientKey}; the identity is built without an access claim and will not resolve any machine permissions.",
                    clientKey);
            }

            return retVal;
        }
    }
}
