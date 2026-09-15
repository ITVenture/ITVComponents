using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Authentication.ApiKey.Options
{
    public class WebPartOptions
    {
        public string AuthenticationType { get; set; } = ApiKeyAuthenticationOptions.DefaultScheme;

        /// <summary>
        /// Loest API-Schluessel gegen die <b>ClientApp-Zugaenge</b> auf statt gegen die Benutzertabelle.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Der Standard-Resolver vergleicht den Schluessel im KLARTEXT</b> gegen <c>Users.UserName</c> -
        /// er steht damit lesbar in der Benutzertabelle. Fuer einen API-Schluessel ist das dieselbe Klasse
        /// von Angriffsflaeche wie ein Klartextpasswort, nur faellt es nicht auf, weil niemand ihn je
        /// tippt.
        /// </para>
        /// <para>
        /// Mit dieser Einstellung laeuft die Aufloesung stattdessen ueber
        /// <c>&lt;ClientKey&gt;.&lt;Label&gt;.&lt;Geheimnis&gt;</c>: zwei indizierte Nachschlagefelder, das
        /// Geheimnis gegen einen Hash. Dazu kommen Mandant, Ablauf und Widerruf, die der Standard-Weg
        /// allesamt nicht kennt.
        /// </para>
        /// <para>
        /// <b>Vorgabe aus</b>, weil sie eine Umsetzung von <c>IClientAppAccessQuery</c> voraussetzt - die
        /// EF-Schicht bringt sie mit, ein Host ohne sie bekaeme sonst einen Aufloesungsfehler beim ersten
        /// Aufruf.
        /// </para>
        /// </remarks>
        public bool UseClientAppResolver { get; set; }
    }
}
