using System.Linq;
using System.Security.Claims;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Entscheidet bei <b>jedem</b> Zugriff auf das Sicherheits-Repository neu, ob die laufende Anfrage
    /// innerhalb einer Freigabe laeuft, und legt in dem Fall die Sicht der Freigabe obenauf.
    /// <para>
    /// Vorher fiel diese Entscheidung einmal, beim Bauen des Repositories - und damit zum falschen
    /// Zeitpunkt. Der Prinzipal einer Freigabe entsteht mitten in der Pipeline; beim anonymen Zugriff
    /// faellt die erste Aufloesung sogar <em>in</em> die Anmeldung hinein, weil deren Schema selbst ein
    /// Repository braucht (zum Entschluesseln des Zugangs-Tokens). Was dort entschieden wurde, galt fuer
    /// die ganze Anfrage: eine undekorierte Sicht, obwohl der Prinzipal einen Satz spaeter alles hatte,
    /// was es braucht. Sichtbar wurde das als 404, weil ohne die Freigabe-Sicht kein Mandant zulaessig
    /// ist und die Mandanten-Middleware das Segment folglich nicht abtrennt.
    /// </para>
    /// </summary>
    internal sealed class LateAssetView
    {
        private readonly IContextUserProvider userProvider;

        private ClaimsPrincipal builtFor;
        private ISecurityRepository builtOver;
        private ISecurityRepository built;

        /// <summary>
        /// Initializes a new instance of the <see cref="LateAssetView"/> class.
        /// </summary>
        /// <param name="userProvider">liefert den Benutzer der laufenden Anfrage - bei jedem Aufruf neu</param>
        public LateAssetView(IContextUserProvider userProvider)
        {
            this.userProvider = userProvider;
        }

        /// <summary>
        /// Liefert die Sicht, die fuer den aktuellen Stand der Anfrage gilt.
        /// </summary>
        /// <param name="current">die oberste Sicht des Stapels</param>
        /// <returns>die Sicht der Freigabe, oder unveraendert <paramref name="current"/></returns>
        public ISecurityRepository Resolve(ISecurityRepository current)
        {
            var user = userProvider?.User;
            var authUser = user?.Identities.FirstOrDefault(n => n.IsAuthenticated);
            if (authUser?.HasClaim(n => n.Type == ClaimTypes.FixedUserScope) != true)
            {
                // Keine Freigabe im Spiel - und nichts gemerkt, damit die naechste Frage wieder frei ist.
                return current;
            }

            if (current is AssetSecurityRepository)
            {
                // Ein anderer Weg (IsLegitSharedAssetPath) hat die Sicht bereits aufgesetzt, mit den
                // Angaben der Freigabe selbst. Die ist genauer als eine, die nur die Claims kennt.
                return current;
            }

            if (!ReferenceEquals(builtFor, user) || !ReferenceEquals(builtOver, current))
            {
                builtFor = user;
                builtOver = current;
                built = new AssetSecurityRepository(user, current);
            }

            return built;
        }
    }
}
