using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.DevicePairing
{
    /// <summary>
    /// Der Geraete-Kopplungs-Ablauf: das Geraet fragt einen Code an, ein angemeldeter Benutzer bestaetigt
    /// ihn, das Geraet holt daraufhin seinen Schluessel ab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Der Punkt des Verfahrens:</b> der Benutzer tippt acht Zeichen ab. Kein Schluessel zum
    /// Abschreiben, kein Passwort in einer Konfigurationsdatei, keine Datei auf einem USB-Stick.
    /// </para>
    /// <para>
    /// <b>Das Geheimnis entsteht erst beim ABHOLEN, nicht beim Bestaetigen.</b> Das ist die Stelle, an der
    /// dieser Ablauf sich von der naheliegenden Fassung unterscheidet, und der Grund ist einfach: wuerde
    /// es beim Bestaetigen erzeugt, muesste es bis zum Abholen irgendwo im Klartext liegen. So gibt es
    /// den Klartext genau einmal - in der Antwort, die ihn ausliefert.
    /// </para>
    /// </remarks>
    public interface IDevicePairingService
    {
        /// <summary>
        /// Vom GERAET gerufen. Legt einen offenen Vorgang an und gibt die Codes zurueck.
        /// </summary>
        /// <remarks>
        /// Laeuft anonym - das Geraet hat noch keine Identitaet. Die Anwendung wird ueber ihre oeffentliche
        /// Kennung benannt; sie bringt den Mandanten mit.
        /// </remarks>
        /// <param name="clientKey">die oeffentliche Kennung der Anwendung</param>
        /// <param name="deviceLabel">wie sich das Geraet nennt (nur beschreibend)</param>
        /// <param name="ct">ein Abbruchtoken</param>
        /// <returns>die Codes, oder null wenn es die Anwendung nicht gibt oder sie abgeschaltet ist</returns>
        Task<PairingRequest> StartAsync(string clientKey, string deviceLabel, CancellationToken ct = default);

        /// <summary>
        /// Was der Benutzercode betrifft - fuer die Anzeige VOR dem Bestaetigen.
        /// </summary>
        /// <param name="userCode">der abgetippte Code</param>
        /// <param name="ct">ein Abbruchtoken</param>
        Task<PairingPreview> DescribeAsync(string userCode, CancellationToken ct = default);

        /// <summary>
        /// Von einem ANGEMELDETEN Benutzer gerufen, der die Kopplung bestaetigt.
        /// </summary>
        /// <remarks>
        /// Legt den Zugang an - noch <b>ohne</b> Geheimnis. Der Vorgang muss zum Mandanten des Benutzers
        /// gehoeren; die Rechtepruefung macht der Aufrufer (der Endpunkt).
        /// </remarks>
        /// <param name="userCode">der abgetippte Code</param>
        /// <param name="ct">ein Abbruchtoken</param>
        Task<PairingConfirmation> ConfirmAsync(string userCode, CancellationToken ct = default);

        /// <summary>
        /// Vom GERAET gerufen, waehrend es wartet. Liefert den Schluessel <b>genau einmal</b>.
        /// </summary>
        /// <param name="deviceCode">der Geraetecode aus <see cref="StartAsync"/></param>
        /// <param name="ct">ein Abbruchtoken</param>
        Task<PairingResult> PollAsync(string deviceCode, CancellationToken ct = default);

        /// <summary>
        /// Widerruft einen Zugang. Das Geraet kommt beim naechsten Aufruf nicht mehr herein.
        /// </summary>
        /// <param name="label">der Bezeichner des Zugangs</param>
        /// <param name="ct">ein Abbruchtoken</param>
        /// <returns>true, wenn ein Zugang widerrufen wurde</returns>
        Task<bool> RevokeAsync(string label, CancellationToken ct = default);
    }
}
