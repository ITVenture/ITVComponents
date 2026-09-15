using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.ClientApps
{
    /// <summary>
    /// Schlaegt den Zugang einer Anwendung nach - das, was ein Geraet beim Anmelden vorlegt.
    /// </summary>
    /// <remarks>
    /// Liegt im Kern und nicht in der EF-Schicht, weil der Anmelde-Rand
    /// (<c>ITVComponents.WebCoreToolkit.Authentication</c>) die EF-Schicht nicht kennt und auch nicht
    /// kennen soll - dasselbe Muster wie bei <see cref="ISecurityRepository"/>.
    /// </remarks>
    public interface IClientAppAccessQuery
    {
        /// <summary>
        /// Sucht den Zugang zu Kennung und Bezeichner und prueft, ob er ueberhaupt noch gilt.
        /// </summary>
        /// <remarks>
        /// Die Umsetzung prueft <b>Widerruf, Ablauf und den Abschalter der Anwendung</b> und gibt einen
        /// ungueltigen Zugang gar nicht erst heraus. Das Geheimnis prueft sie <b>nicht</b> - das tut der
        /// Aufrufer gegen <see cref="ClientAppAccessInfo.SecretHash"/>, damit die Datenbankschicht den
        /// Klartext nie zu sehen bekommt.
        /// </remarks>
        /// <param name="clientKey">die oeffentliche Kennung der Anwendung</param>
        /// <param name="label">der Bezeichner des Zugangs</param>
        /// <param name="ct">ein Abbruchtoken</param>
        /// <returns>die Angaben zum Zugang, oder null</returns>
        Task<ClientAppAccessInfo> ResolveAsync(string clientKey, string label, CancellationToken ct = default);

        /// <summary>
        /// Vermerkt eine erfolgreiche Anmeldung.
        /// </summary>
        /// <remarks>
        /// Getrennt vom Nachschlagen, damit ein fehlgeschlagener Versuch den Zeitstempel NICHT setzt -
        /// sonst liesse sich an <c>LastUsedUtc</c> nicht mehr ablesen, ob ein Geraet noch arbeitet oder
        /// nur noch erfolglos anklopft.
        /// </remarks>
        /// <param name="clientAppAccessId">der Zugang</param>
        /// <param name="ct">ein Abbruchtoken</param>
        Task MarkUsedAsync(int clientAppAccessId, CancellationToken ct = default);
    }
}
