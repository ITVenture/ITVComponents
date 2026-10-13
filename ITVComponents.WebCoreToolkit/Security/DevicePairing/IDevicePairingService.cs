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
        /// <para>
        /// Laeuft anonym - das Geraet hat noch keine Identitaet.
        /// </para>
        /// <para>
        /// <b>Der Regelfall: das Geraet sagt nur, WAS es ist</b> (<paramref name="typeKey"/>, der Typ-Schluessel
        /// eines Templates). Welche Anwendung - und damit welcher Mandant - es wird, waehlt beim Bestaetigen,
        /// wer bestaetigt. So muss auf dem Geraet nichts eingetragen werden, und es kann nicht versehentlich
        /// an der Anwendung einer anderen Kasse landen.
        /// </para>
        /// <para>
        /// Mit <paramref name="clientKey"/> legt das Geraet die Anwendung selbst fest (der fruehere Weg); sie
        /// bringt dann den Mandanten mit, und <paramref name="typeKey"/> wird nicht gebraucht.
        /// </para>
        /// </remarks>
        /// <param name="clientKey">die oeffentliche Kennung einer Anwendung, oder null</param>
        /// <param name="typeKey">der Typ-Schluessel des Templates, oder null, wenn <paramref name="clientKey"/> gesetzt ist</param>
        /// <param name="deviceLabel">wie sich das Geraet nennt (nur beschreibend)</param>
        /// <param name="ct">ein Abbruchtoken</param>
        /// <returns>
        /// die Codes, oder null wenn es die Anwendung oder den Typ nicht gibt, die Anwendung abgeschaltet ist
        /// oder keins von beidem genannt wurde
        /// </returns>
        Task<PairingRequest> StartAsync(string clientKey, string typeKey, string deviceLabel, CancellationToken ct = default);

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
        /// <para>
        /// Legt den Zugang an - noch <b>ohne</b> Geheimnis. Die Anwendung muss zum Mandanten des Benutzers
        /// gehoeren; die Rechtepruefung macht der Aufrufer (der Endpunkt).
        /// </para>
        /// <para>
        /// <b>Eine Anwendung hat hoechstens eine aktive Instanz.</b> Ist schon ein anderes Geraet an ihr
        /// gekoppelt, wird abgewiesen - es muss zuerst widerrufen werden. Dasselbe Geraet (gleicher Name)
        /// erneut zu koppeln ersetzt seinen bisherigen Zugang.
        /// </para>
        /// </remarks>
        /// <param name="userCode">der abgetippte Code</param>
        /// <param name="clientAppId">
        /// die gewaehlte Anwendung. Pflicht, wenn das Geraet keine festgelegt hat; hat es eine festgelegt,
        /// muss das null oder genau diese sein.
        /// </param>
        /// <param name="ct">ein Abbruchtoken</param>
        Task<PairingConfirmation> ConfirmAsync(string userCode, int? clientAppId, CancellationToken ct = default);

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
