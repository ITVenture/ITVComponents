using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Options
{
    /// <summary>
    /// Was nur wallee etwas sagt. Alles, was der PLATTFORM gehoert - Provision, Gebuehrenerlass, Waehrung,
    /// Ablaufzeit - steht im neutralen <c>TenantPayments</c>-Setting und wird hier NICHT wiederholt.
    /// </summary>
    [SettingName("WalleePayments")]
    public class WalleeOptions
    {
        /// <summary>
        /// Der Raum (<i>Space</i>), in dem gearbeitet wird. Bei wallee ist der Raum die Klammer um Konten,
        /// Transaktionen und Abonnemente - praktisch jeder Aufruf traegt ihn als ersten Parameter.
        /// </summary>
        public long SpaceId { get; set; }

        /// <summary>
        /// Die Kennung des <b>Anwendungsbenutzers</b> - nicht die eines Menschen. wallee unterscheidet das
        /// streng: ein Anwendungsbenutzer hat keinen Zugang zur Oberflaeche und meldet sich nur ueber die
        /// Signatur an.
        /// </summary>
        public long ApplicationUserId { get; set; }

        /// <summary>
        /// Der Schluessel des Anwendungsbenutzers. Geht NIE als Kopfzeile mit, sondern signiert jede
        /// Anfrage (MAC ueber Version, Benutzer, Zeitstempel, Methode und Pfad) - das uebernimmt das SDK.
        /// </summary>
        /// <remarks>
        /// Weil der Zeitstempel mitsigniert wird, laeuft eine Anfrage in einen Authentifizierungsfehler,
        /// wenn die Uhr des Servers zu weit abweicht. Die Meldung spricht dann von einer unglueltigen
        /// Signatur und nicht von der Zeit - ein Fehler, der ohne diesen Hinweis teuer ist.
        /// </remarks>
        public string ApplicationUserKey { get; set; } = string.Empty;

        /// <summary>
        /// Die Adresse der API. Nur zu setzen, wenn nicht die oeffentliche Umgebung gemeint ist.
        /// </summary>
        public string? ApiBaseUrl { get; set; }

        /// <summary>
        /// Die Zahlungsarten (bei wallee: <i>payment methods</i>), die auf der Zahlungsseite erscheinen
        /// sollen. Leer = alles, was im Raum freigeschaltet ist.
        /// </summary>
        /// <remarks>
        /// Derselbe Hebel wie bei Payrexx: TWINT kostet in der Schweiz rund einen Prozentpunkt weniger als
        /// eine Karte.
        /// </remarks>
        public long[] PaymentMethodConfigurations { get; set; } = [];

        /// <summary>
        /// Die Sprache, unter der Namen und Beschreibungen bei wallee abgelegt werden.
        /// </summary>
        /// <remarks>
        /// wallee fuehrt diese Texte als Woerterbuch Sprache-zu-Text, nicht als schlichte Zeichenkette -
        /// und zwar bei Produktversion, Komponentengruppe und Komponente, NICHT aber beim Produkt selbst.
        /// Ein Schluessel, den wallee nicht kennt, faellt erst beim Anlegen auf.
        /// </remarks>
        public string DefaultLanguage { get; set; } = "en-US";

        /// <summary>
        /// Ob die Signatur eingehender Benachrichtigungen geprueft wird. Vorgabe: ja.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Hier steht kein Geheimnis, und das ist kein Versehen.</b> wallee signiert anders als die
        /// uebrigen Anbieter: nicht mit einem gemeinsamen Schluessel, sondern mit einem privaten
        /// (SHA256withECDSA). Die Kopfzeile <c>x-signature</c> nennt die Kennung des zugehoerigen
        /// oeffentlichen Schluessels, und der wird bei wallee abgeholt - es gibt also nichts zu
        /// hinterlegen.
        /// </para>
        /// <para>
        /// Abschalten laesst es sich trotzdem, fuer den Fall, dass am Listener die Signierung nicht
        /// eingeschaltet ist (wallee fuehrt sie als Wahlmoeglichkeit). Dann kommt keine Kopfzeile, und
        /// eine Pruefung wuerde jede Meldung ablehnen. Der Webhook-Weg protokolliert das ungeprueft
        /// Angenommene aber jedes Mal - wer diesen Endpunkt kennt, koennte sonst beliebige Verkaeufe als
        /// bezahlt melden.
        /// </para>
        /// </remarks>
        public bool VerifyWebhookSignatures { get; set; } = true;
    }
}
