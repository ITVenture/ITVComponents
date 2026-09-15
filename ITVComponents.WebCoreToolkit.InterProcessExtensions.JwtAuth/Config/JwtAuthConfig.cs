using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth.Config
{
    /// <summary>
    /// Die Konfiguration eines Bearer-Anmeldewegs fuer einen Hub-Client.
    /// </summary>
    /// <remarks>
    /// Bis PRE239 trug diese Klasse <b>nur</b> <see cref="Name"/> - es gab also keine Stelle, an der ein
    /// Konsument haette hinterlegen koennen, <i>wo</i> ein Token zu holen ist und <i>womit</i>. Der Name
    /// allein genuegt, um eine Konfiguration zu finden, nicht um sie zu benutzen.
    /// </remarks>
    public class JwtAuthConfig
    {
        /// <summary>Der Name, unter dem diese Konfiguration gefunden wird.</summary>
        public string Name { get; set; }

        /// <summary>
        /// Die vollstaendige Adresse des Token-Endpunkts.
        /// </summary>
        /// <remarks>
        /// Nur fuer den mitgelieferten Weg noetig. Wer dem <c>JwtAuthInit</c> eine eigene
        /// <see cref="ITokenSource"/> mitgibt, laesst das Feld leer - dann kommt das Token von dort.
        /// </remarks>
        public string TokenEndpoint { get; set; }

        /// <summary>
        /// Der API-Schluessel, mit dem sich der Client am Token-Endpunkt ausweist - in der Form
        /// <c>&lt;ClientKey&gt;.&lt;Label&gt;.&lt;Geheimnis&gt;</c>, wie ihn die Geraete-Kopplung ausgibt.
        /// </summary>
        /// <remarks>
        /// <b>Das ist ein Geheimnis.</b> Es gehoert in eine verschluesselte Einstellung und nicht in eine
        /// eingecheckte Konfigurationsdatei.
        /// </remarks>
        public string ApiKey { get; set; }

        /// <summary>
        /// Wie lange vor dem Ablauf erneuert wird, in Sekunden.
        /// </summary>
        /// <remarks>
        /// Der Abstand deckt die Laufzeit des Aufrufs und einen Uhrenversatz zwischen Client und Server
        /// ab. Zu klein gewaehlt, laeuft das Token waehrend des Aufrufs ab, den es begleiten sollte.
        /// </remarks>
        public int RenewBeforeSeconds { get; set; } = 60;

        /// <summary>
        /// Wie lange auf den Token-Endpunkt gewartet wird, in Sekunden.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
