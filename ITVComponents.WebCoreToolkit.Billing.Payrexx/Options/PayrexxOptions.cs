using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Options
{
    /// <summary>
    /// Was nur Payrexx etwas sagt. Alles, was der PLATTFORM gehoert - Provision, Gebuehrenerlass, Waehrung,
    /// Ablaufzeit der Zahlungsseite - steht weiterhin im neutralen <c>TenantPayments</c>-Setting und wird
    /// von hier NICHT wiederholt.
    /// </summary>
    /// <remarks>
    /// Eigenes GlobalSetting statt eines Unterobjekts in <c>TenantPaymentsOptions</c>: ein Betrieb, der
    /// Payrexx fuehrt, soll kein Stripe-Setting pflegen muessen und umgekehrt. Die neutrale Klammer ist
    /// das gemeinsame Setting, nicht diese Datei.
    /// </remarks>
    [SettingName("PayrexxPayments")]
    public class PayrexxOptions
    {
        /// <summary>
        /// Der Instanzname - der erste Teil der Adresse, unter der der Betrieb bei Payrexx laeuft
        /// (<c>https://{Instance}.payrexx.com</c>). Reist bei JEDEM Aufruf als Query-Parameter mit und ist
        /// deshalb kein Geheimnis.
        /// </summary>
        public string Instance { get; set; } = string.Empty;

        /// <summary>
        /// Das API-Geheimnis. Geht als <c>X-API-KEY</c> im Kopf mit - der von Payrexx empfohlene Weg.
        /// </summary>
        /// <remarks>
        /// Die Alternative waere eine HMAC-SHA256-Signatur ueber die sortierten Parameter. Sie ist nicht
        /// gebaut: sie schuetzt gegen nichts, was TLS nicht schon abdeckt, und sie ist eine ergiebige
        /// Fehlerquelle - eine abweichende Sortierung oder Kodierung faellt erst als "signature invalid"
        /// auf, ohne zu sagen, welcher Parameter es war.
        /// </remarks>
        public string ApiSecret { get; set; } = string.Empty;

        /// <summary>
        /// Die Basis-Adresse der HAENDLER-API samt Version. Versionierbar gehalten, weil Payrexx die
        /// Version in den Pfad schreibt und neue Felder mit neuen Versionen kommen.
        /// </summary>
        /// <remarks>
        /// <b>Im White-Label-Betrieb ist das NICHT api.payrexx.com.</b> Die Plattform-Dokumentation sagt es
        /// ausdruecklich: Haendler und Plattform benutzen fuer die Haendler-API die Domain der Plattform,
        /// also <c>api.meine-plattform.ch</c>. Wer den Vorgabewert stehen laesst, redet mit der falschen
        /// Stelle - und bekommt eine Antwort, die nach einem Rechteproblem aussieht.
        /// </remarks>
        public string ApiBaseUrl { get; set; } = "https://api.payrexx.com/v1.0/";

        /// <summary>
        /// Die Basis-Adresse der SERVICE-API - der Plattform-Seite (White-Label / Marktplatz), ueber die
        /// Haendler angelegt und Erstattungen ausgeloest werden.
        /// </summary>
        /// <remarks>
        /// Ohne Version: die ist bei Payrexx <b>nicht einheitlich</b> (der Haendler liegt unter v2.3, die
        /// Identitaetspruefung unter v2.0) und wird darum je Aufruf mitgegeben.
        /// </remarks>
        public string ServiceApiBaseUrl { get; set; } = "https://api.payrexx.com";

        /// <summary>
        /// Die Kennung der Plattform. Geht als <c>X-PLATFORM</c> mit und ist bei JEDEM Aufruf der
        /// Service-API Pflicht.
        /// </summary>
        /// <remarks>
        /// Fehlt sie, antwortet die API nicht mit "Kopfzeile fehlt", sondern so, als waere der Schluessel
        /// falsch - und die Suche beginnt beim Geheimnis statt bei dieser Einstellung.
        /// </remarks>
        public string PlatformKey { get; set; } = string.Empty;

        /// <summary>
        /// Zahlungsarten, die auf der Zahlungsseite erscheinen sollen (Payrexx: <c>pm</c>), z.B.
        /// <c>twint</c>, <c>visa</c>, <c>mastercard</c>, <c>post_finance_card</c>. Leer = alles, was der
        /// Betrieb freigeschaltet hat.
        /// </summary>
        /// <remarks>
        /// Der Grund, warum es diese Einstellung ueberhaupt gibt: TWINT kostet in der Schweiz rund einen
        /// Prozentpunkt weniger als eine Karte. Wer das nutzen will, stellt es hier voran - und wer nichts
        /// einstellt, bekommt weiterhin alles.
        /// </remarks>
        public string[] PaymentMeans { get; set; } = [];

        /// <summary>
        /// Das Geheimnis, mit dem eingehende Benachrichtigungen geprueft werden - sofern der Betrieb eines
        /// hinterlegt hat. Leer heisst: es wird NICHT geprueft, und genau das protokolliert der
        /// Webhook-Weg dann auch, statt stillschweigend jedem zu glauben.
        /// </summary>
        public string WebhookSecret { get; set; } = string.Empty;
    }
}
