using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using WalleeConfiguration = Wallee.Client.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Impl
{
    /// <summary>
    /// Das Gemeinsame aller wallee-Dienste: die Anmeldung und die Protokoll-Kategorie.
    /// </summary>
    /// <remarks>
    /// <b>Bewusst NICHT generisch.</b> Vorher hingen diese beiden an <c>WalleeSaleService&lt;TContext&gt;</c>,
    /// und das hat sich sofort gerächt: dessen Typparameter verlangt einen <c>IPaymentsContext</c> (Achse B),
    /// während die Abo-Seite mit einem <c>IBillingContext</c> arbeitet (Achse A). Der Zugriff auf eine
    /// statische Hilfe zwang damit eine Einschränkung auf, die fachlich nichts mit ihr zu tun hat.
    /// <para>
    /// Allgemeiner: statische Helfer an einer generischen Klasse erben deren Typregeln, ohne sie zu
    /// brauchen. Wer so etwas bemerkt, zieht es heraus, statt die Einschränkung weiterzureichen.
    /// </para>
    /// </remarks>
    internal static class WalleeRuntime
    {
        /// <summary>
        /// Die Protokoll-Kategorie. Dieselbe wie bei den übrigen Anbietern — wer nach Zahlungen sucht,
        /// soll nicht wissen müssen, welcher gerade verdrahtet ist.
        /// </summary>
        public const string LogContext = "TenantPayments";

        /// <summary>
        /// Die Anmeldung für jeden Dienst dieses Anbieters.
        /// </summary>
        /// <remarks>
        /// Die Raum-Kennung steckt NICHT darin — wallee verlangt sie als ersten Parameter jedes einzelnen
        /// Aufrufs. Nur Benutzer und Schlüssel gehören hierher.
        /// </remarks>
        public static WalleeConfiguration Configure(WalleeOptions options)
        {
            var configuration = new WalleeConfiguration(options.ApplicationUserId, options.ApplicationUserKey);
            if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl))
            {
                configuration.BasePath = options.ApiBaseUrl;
            }

            return configuration;
        }
    }
}
