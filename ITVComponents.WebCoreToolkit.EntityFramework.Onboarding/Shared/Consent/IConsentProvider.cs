using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Consent
{
    /// <summary>
    /// Die Zustimmungen eines Vorgangs: welche eingeholt werden, ob sie vollstaendig sind und wie der
    /// Nachweis abgelegt wird.
    /// </summary>
    public interface IConsentProvider
    {
        /// <summary>
        /// Die Punkte, die in diesem Vorgang gelten, getrennt nach Anzeigeort. Leer = es ist nichts
        /// konfiguriert; die Oberflaeche zeigt dann ihren einzelnen eingebauten Schalter.
        /// </summary>
        /// <param name="occasion">der Vorgang</param>
        /// <param name="userId">
        /// der angemeldete Benutzer, sofern es ihn schon gibt. Nur dann laesst sich feststellen, was er
        /// persoenlich bereits beantwortet hat - entsteht das Konto gerade erst, ist alles offen.
        /// </param>
        /// <param name="ct">Abbruch</param>
        Task<ConsentSet> DescribeAsync(ConsentOccasionKind occasion, string userId, CancellationToken ct = default);

        /// <summary>
        /// Prueft, ob alle Pflicht-Punkte zugestimmt sind. Bewusst synchron und ohne Datenbank - es ist ein
        /// Abgleich zweier Listen, und er laeuft auf jeder Kante, die abschickt.
        /// </summary>
        ConsentCheckResult Validate(IReadOnlyList<ConsentPoint> points, IReadOnlyDictionary<string, bool> switches);

        /// <summary>
        /// Legt die Nachweise ab. Fehlschlaege werden protokolliert, aber nicht geworfen: der Vorgang, zu
        /// dem sie gehoeren, ist an dieser Stelle bereits abgeschlossen, und ihn nachtraeglich scheitern zu
        /// lassen waere schlimmer als ein fehlender Nachweis.
        /// </summary>
        /// <returns>wie viele Nachweise geschrieben wurden</returns>
        Task<int> RecordAsync(IReadOnlyList<ConsentAnswer> answers, ConsentSubject subject, CancellationToken ct = default);

        /// <summary>
        /// Wie der Benutzer zu seinen PERSOENLICHEN Punkten steht - je konfiguriertem Punkt mit
        /// <see cref="ConsentScope.User"/> die letzte Antwort, die von ihm vorliegt.
        /// </summary>
        /// <remarks>
        /// Nur persoenliche Punkte: was fuer einen Mandanten erklaert wurde, gehoert in dessen Verwaltung
        /// und nicht in das Konto einer Person - selbst wenn sie es war, die geklickt hat.
        /// </remarks>
        Task<IReadOnlyList<ConsentStanding>> GetStandingAsync(string userId, CancellationToken ct = default);
    }
}
