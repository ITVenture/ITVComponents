using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace ITVComponents.WebCoreToolkit.Authentication.ApiKey.Models
{
    /// <summary>
    /// Das Ergebnis einer Schluessel-Aufloesung.
    /// </summary>
    public class ApiKeyInfo
    {
        public ApiKeyInfo(string key, DateTime created)
            : this(key, created, null)
        {
        }

        public ApiKeyInfo(string key, DateTime created, IReadOnlyList<Claim> additionalClaims)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Created = created;
            AdditionalClaims = additionalClaims ?? Array.Empty<Claim>();
        }

        /// <summary>
        /// Der Name, unter dem der Aufrufer angemeldet wird.
        /// </summary>
        /// <remarks>
        /// <b>Achtung, Doppelrolle:</b> beim Standard-Resolver ist das derselbe Wert, der hereingereicht
        /// wurde - beim <c>ApiKeyAuthenticationHandler</c> wird er aber als <c>ClaimTypes.Name</c> gesetzt,
        /// und <b>nicht</b> der vom Aufrufer vorgelegte Schluessel.
        /// <para>
        /// Darauf beruht jeder Resolver, der den Klartext nicht speichern will: er hasht den vorgelegten
        /// Schluessel, schlaegt ihn nach und gibt hier den <b>gefundenen Bezeichner</b> zurueck. Wer der
        /// Signatur vertraut und den Eingabewert durchreicht, speichert den Schluessel im Klartext, ohne
        /// es zu merken.
        /// </para>
        /// </remarks>
        public string Key { get; }

        public DateTime Created { get; }

        /// <summary>
        /// Weitere Ansprueche, die der Anmeldung mitgegeben werden - etwa
        /// <c>ClaimTypes.FixedUserScope</c> fuer den Mandanten.
        /// </summary>
        /// <remarks>
        /// Nachtraeglich ergaenzt: der Handler setzte zuvor ausschliesslich <c>ClaimTypes.Name</c>, ein per
        /// Schluessel angemeldetes Geraet hatte also keinen Mandantenkontext. Der zweiargumentige
        /// Konstruktor bleibt, damit bestehende Umsetzungen von <c>IGetApiKeyQuery</c> unveraendert
        /// uebersetzen.
        /// </remarks>
        public IReadOnlyList<Claim> AdditionalClaims { get; }
    }
}
