using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Options
{
    /// <summary>
    /// Was nur SumUp etwas sagt - und nur das, was der PLATTFORM gehoert.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hier steht kein Haendler-Zugang.</b> Bei SumUp fuehrt die Plattform kein Konto fuer ihre Mandanten:
    /// jeder bringt sein eigenes mit (<c>TenantProviderAccess</c>), und dessen Schluessel liegt verschluesselt
    /// beim Mandanten. Was hier steht, ist die Kennung der Plattform als Partner.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen die SumUp-Sandbox gelaufen.</b> Gebaut nach der Doku und der OpenAPI-Beschreibung
    /// (Stand Oktober 2026).
    /// </para>
    /// </remarks>
    [SettingName("SumUpPayments")]
    public class SumUpOptions
    {
        /// <summary>Die Basis der API, ohne Version - die steht je Aufruf im Pfad, und sie ist nicht einheitlich.</summary>
        public string ApiBaseUrl { get; set; } = "https://api.sumup.com";

        /// <summary>
        /// Die Anwendungskennung, an die der Affiliate Key gebunden ist (<c>affiliate.app_id</c>).
        /// </summary>
        public string AffiliateAppId { get; set; } = string.Empty;

        /// <summary>
        /// Der Affiliate Key der Plattform. Fuer Zahlungen an einem Geraet ist er Pflicht, und EINER gilt fuer alle
        /// Haendler: er ordnet ihre Umsaetze der Plattform als Partner zu.
        /// </summary>
        /// <remarks>Kein Anmeldemittel - er berechtigt zu nichts, er wird nur mitgeschickt.</remarks>
        public string AffiliateKey { get; set; } = string.Empty;

        /// <summary>
        /// Die oeffentliche HTTPS-Adresse, an die SumUp das Ergebnis eines Geraete-Vorgangs meldet
        /// (<c>return_url</c>) - die volle Adresse des Endpunkts aus <c>MapSumUpWebhook</c>.
        /// </summary>
        /// <remarks>
        /// Leer heisst: keine Meldungen. Dann bleibt nur das Nachfragen der Kasse und der Beleg-Sweeper - das
        /// funktioniert, ist aber langsamer.
        /// </remarks>
        public string? WebhookUrl { get; set; }

        /// <summary>Wie lange auf eine Antwort von SumUp gewartet wird, in Sekunden.</summary>
        public int RequestTimeoutSeconds { get; set; } = 30;
    }
}
