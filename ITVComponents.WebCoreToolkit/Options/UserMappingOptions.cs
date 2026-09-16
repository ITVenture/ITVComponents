using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class UserMappingOptions
    {
        /// <summary>
        /// <b>Wird nicht mehr ausgewertet.</b>
        /// </summary>
        /// <remarks>
        /// Die Wicklung eines Anwendungs-Zugangs zu <c>##APPUSER##&lt;Label&gt;#</c> entsteht heute immer,
        /// wenn der Anspruch <c>ClaimTypes.ClientAppAccess</c> vorliegt - und der liegt nur vor, wo ein
        /// Host Anwendungs-Zugaenge bewusst eingeschaltet hat. Ein zweiter Schalter schuetzte vor nichts.
        /// <para>
        /// Er richtete sogar Schaden an: gesetzt wurde er an genau EINER Stelle, im <b>Bearer</b>-Zweig
        /// von <c>WebPartInit</c>. Wer sich per <c>X-Api-Key</c> anmeldete und kein Bearer konfiguriert
        /// hatte, stand damit ohne Maschinen-Rechte da, obwohl der Anspruch korrekt gesetzt war - und der
        /// Befund sah aus wie ein Rechteproblem.
        /// </para>
        /// <para>
        /// Die Eigenschaft bleibt bestehen, damit vorhandene Konfiguration und vorhandener Code weiter
        /// uebersetzen; sie steuert nur nichts mehr.
        /// </para>
        /// </remarks>
        public bool MapApplicationId { get; set; }
    }
}
