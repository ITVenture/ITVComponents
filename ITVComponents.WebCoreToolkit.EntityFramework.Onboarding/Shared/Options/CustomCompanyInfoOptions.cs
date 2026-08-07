using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options
{
    /// <summary>
    /// Welche Zusatzangaben-Module in der Firmendaten-Erfassung mitlaufen.
    /// </summary>
    /// <remarks>
    /// Bewusst eine Namensliste und kein Suchen nach allen Plugins, die den Vertrag erfuellen: der
    /// Plugin-Bestand hat keinen Typ-Index, und die Liste gibt zugleich die Reihenfolge der Reiter vor.
    /// <para>
    /// Die genannten Plugins muessen GLOBAL sein (kein Tenant): waehrend der Anlage gibt es den Tenant,
    /// zu dem die Angaben gehoeren, noch gar nicht.
    /// </para>
    /// </remarks>
    [SettingName("CustomCompanyInfo")]
    public class CustomCompanyInfoOptions
    {
        /// <summary>
        /// Die eindeutigen Namen der Plugins, in der Reihenfolge, in der ihre Reiter erscheinen sollen.
        /// Leer = keine Zusatzangaben, die Erfassung sieht aus wie bisher.
        /// </summary>
        public string[] Handlers { get; set; } = new string[0];
    }
}
