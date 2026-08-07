using System;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Consent
{
    /// <summary>
    /// Wen eine Zustimmung betrifft.
    /// </summary>
    /// <remarks>
    /// Die Unterscheidung ist keine Formalie: eine Datenschutzerklaerung betrifft die natuerliche Person
    /// hinter dem Konto und gilt fuer sie ein fuer alle Mal, waehrend Nutzungsbedingungen ein Vertrag sein
    /// koennen, den jeder neue Mandant fuer sich schliesst. Welches von beidem zutrifft, haengt am
    /// Geschaeftsmodell des Betriebs - deshalb steht es in der Konfiguration und nicht im Code.
    /// </remarks>
    public enum ConsentScope
    {
        /// <summary>
        /// Betrifft den Benutzer als natuerliche Person (Datenschutz, Newsletter, Nutzungsregeln). Wird
        /// EINMAL erfasst; wer den Punkt in der geltenden Fassung schon beantwortet hat, wird nicht wieder
        /// gefragt. Der Nachweis traegt keine Mandanten-Nummer.
        /// </summary>
        User,

        /// <summary>
        /// Betrifft den Mandanten (der Vertrag, den die Firma schliesst). Wird bei JEDER Mandanten-Anlage
        /// erfasst - der zweite Mandant ist ein zweiter Vertrag - und erscheint beim blossen Anlegen eines
        /// Kontos gar nicht.
        /// </summary>
        Tenant,

        /// <summary>
        /// Betrifft beide Ebenen (typisch die Nutzungsbedingungen). Erscheint beim Anlegen eines Kontos
        /// ebenso wie bei jeder Mandanten-Anlage - dort EINMAL, im Mandanten-Teil, und nicht zusaetzlich
        /// noch beim Konto. Eine frueher erteilte persoenliche Zustimmung unterdrueckt den Punkt bei der
        /// Mandanten-Anlage NICHT: sie deckt den Vertrag fuer diesen Mandanten nicht ab.
        /// </summary>
        Both
    }

    /// <summary>Der Vorgang, in dem Zustimmungen eingeholt werden.</summary>
    public enum ConsentOccasionKind
    {
        /// <summary>
        /// Nur ein Konto wird angelegt - jemand folgt einer Einladung und erstellt keinen Mandanten.
        /// </summary>
        AccountRegistration,

        /// <summary>
        /// Nur ein Mandant wird angelegt; der Benutzer besteht bereits und ist angemeldet.
        /// </summary>
        TenantOnboarding,

        /// <summary>
        /// Konto UND Mandant entstehen in einem Zug (der anonyme Direkt-Start). Beides muss abgedeckt
        /// werden - dies ist der Fall, in dem die Aufteilung nach Anzeigeort ueberhaupt gebraucht wird.
        /// </summary>
        AccountAndTenant
    }

    /// <summary>
    /// Die einzuholenden Zustimmungen eines Vorgangs, getrennt nach dem Ort, an dem sie erscheinen.
    /// </summary>
    /// <remarks>
    /// Die Trennung ist eine Frage der Verstaendlichkeit: die Zustimmung soll dort stehen, wo ihr
    /// Gegenstand steht - die persoenliche beim Konto (Mailadresse, Kennwort), die des Mandanten bei den
    /// Firmendaten. Ein Punkt taucht nie in beiden Listen auf.
    /// </remarks>
    public class ConsentSet
    {
        /// <summary>Was den Benutzer betrifft - gehoert zum Konto-Teil der Maske.</summary>
        public IReadOnlyList<ConsentPoint> ForUser { get; set; } = new List<ConsentPoint>();

        /// <summary>Was den Mandanten betrifft - gehoert zum Firmen-Teil der Maske.</summary>
        public IReadOnlyList<ConsentPoint> ForTenant { get; set; } = new List<ConsentPoint>();

        /// <summary>Alle Punkte des Vorgangs, in einer Liste - zum Pruefen und Einsammeln.</summary>
        public IReadOnlyList<ConsentPoint> All
        {
            get
            {
                var all = new List<ConsentPoint>(ForUser);
                all.AddRange(ForTenant);
                return all;
            }
        }

        /// <summary>Ist ueberhaupt etwas einzuholen?</summary>
        public bool Any => ForUser.Count > 0 || ForTenant.Count > 0;
    }

    /// <summary>
    /// Ein anzuzeigender Zustimmungspunkt, aufbereitet aus der Konfiguration.
    /// </summary>
    public class ConsentPoint
    {
        /// <summary>Der stabile Schluessel des Punktes.</summary>
        public string Key { get; set; }

        /// <summary>Der Text neben dem Schalter; Klartext oder Kultur-JSON, ggf. mit <c>{0}</c> fuer den Verweis.</summary>
        public string Label { get; set; }

        /// <summary>Das Hilfe-Thema mit dem Wortlaut des Dokuments, oder leer.</summary>
        public string HelpSlug { get; set; }

        /// <summary>Die Beschriftung des Verweises; Klartext oder Kultur-JSON, oder leer.</summary>
        public string LinkText { get; set; }

        /// <summary>Muss zugestimmt werden, um fortzufahren?</summary>
        public bool Required { get; set; }

        /// <summary>Der Stand des Dokuments, der gerade gilt.</summary>
        public string Version { get; set; }

        /// <summary>Wen die Zustimmung betrifft.</summary>
        public ConsentScope Scope { get; set; }
    }

    /// <summary>
    /// Was ein Benutzer zu einem Punkt geantwortet hat - der eigentliche Nachweis.
    /// </summary>
    /// <remarks>
    /// Die Antwort traegt <see cref="Version"/> und <see cref="HelpSlug"/> selbst mit sich und liest sie
    /// beim Ablegen NICHT erneut aus der Konfiguration: zwischen der Zustimmung und ihrer Ablage kann bei
    /// einem geparkten Onboarding viel Zeit liegen, und in der Zwischenzeit kann eine neue Fassung der
    /// Nutzungsbedingungen konfiguriert worden sein. Der Nachweis muss festhalten, was der Benutzer
    /// gesehen hat, nicht was heute gilt. Aus demselben Grund steht auch der Zeitpunkt hier und wird nicht
    /// beim Schreiben gestempelt.
    /// </remarks>
    public class ConsentAnswer
    {
        public string Key { get; set; }

        /// <summary>Zugestimmt oder ausdruecklich abgelehnt.</summary>
        public bool Accepted { get; set; }

        /// <summary>Wann der Benutzer geantwortet hat.</summary>
        public DateTime AcceptedUtc { get; set; }

        /// <summary>In welcher Sprache ihm der Text angezeigt wurde.</summary>
        public string Culture { get; set; }

        /// <summary>Der Stand des Dokuments, der ihm dabei angezeigt wurde.</summary>
        public string Version { get; set; }

        /// <summary>Das Hilfe-Thema, das dabei verlinkt war.</summary>
        public string HelpSlug { get; set; }

        /// <summary>
        /// Wen die Zustimmung betraf. Entscheidet mit darueber, ob der Nachweis eine Mandanten-Nummer
        /// bekommt: eine rein persoenliche Zustimmung gilt unabhaengig davon, bei welcher Gelegenheit sie
        /// erteilt wurde.
        /// </summary>
        public ConsentScope Scope { get; set; }
    }

    /// <summary>
    /// Wie ein Benutzer zu einem persoenlichen Zustimmungspunkt steht: der Punkt selbst und die letzte
    /// Antwort, die von ihm dazu vorliegt.
    /// </summary>
    /// <remarks>
    /// Bewusst beides zusammen: die Punkte stehen in der Konfiguration, die Antworten in der Datenbank,
    /// und keine der beiden Seiten allein ergibt eine anzeigbare Auskunft. Ein konfigurierter Punkt ohne
    /// Nachweis heisst "wurde nie gefragt" - was sich von "hat abgelehnt" unterscheidet.
    /// </remarks>
    public class ConsentStanding
    {
        /// <summary>Der Punkt, so wie er heute konfiguriert ist.</summary>
        public ConsentPoint Point { get; set; }

        /// <summary>Liegt ueberhaupt eine Antwort vor?</summary>
        public bool Answered { get; set; }

        /// <summary>Die letzte Antwort - nur gueltig, wenn <see cref="Answered"/>.</summary>
        public bool Accepted { get; set; }

        /// <summary>Wann sie erteilt wurde.</summary>
        public DateTime? AnsweredUtc { get; set; }

        /// <summary>Der Stand des Dokuments, dem damals zugestimmt wurde.</summary>
        public string AnsweredVersion { get; set; }

        /// <summary>
        /// Bezieht sich die Antwort auf die HEUTE geltende Fassung? Ist sie es nicht, wurde inzwischen eine
        /// neue Fassung konfiguriert und der Punkt wird bei naechster Gelegenheit erneut gestellt.
        /// </summary>
        public bool Current { get; set; }
    }

    /// <summary>Wem die Zustimmungen zugeschrieben werden.</summary>
    public class ConsentSubject
    {
        /// <summary>Der Benutzer, sofern er beim Ablegen feststeht.</summary>
        public string UserId { get; set; }

        /// <summary>Die E-Mail, unter der zugestimmt wurde.</summary>
        public string Email { get; set; }

        /// <summary>Der Mandant, um dessen Anlage es ging; null beim blossen Anlegen eines Kontos.</summary>
        public int? TenantId { get; set; }

        /// <summary>Aus welchem Vorgang die Zustimmungen stammen.</summary>
        public ConsentOccasionKind Origin { get; set; }
    }

    /// <summary>Das Ergebnis der Pruefung, ob alle Pflicht-Punkte beantwortet sind.</summary>
    public class ConsentCheckResult
    {
        private ConsentCheckResult(bool valid, string missingKey)
        {
            Valid = valid;
            MissingKey = missingKey;
        }

        /// <summary>Sind alle Pflicht-Punkte zugestimmt?</summary>
        public bool Valid { get; }

        /// <summary>Der Schluessel des ersten Punktes, an dem es fehlt - zum Markieren an Ort und Stelle.</summary>
        public string MissingKey { get; }

        public static ConsentCheckResult Ok() => new ConsentCheckResult(true, null);

        public static ConsentCheckResult Missing(string key) => new ConsentCheckResult(false, key);
    }

    /// <summary>Bequemlichkeit fuer die Oberflaeche: die Antworten zu einer Punkte-Liste einsammeln.</summary>
    public static class ConsentAnswerExtensions
    {
        /// <summary>
        /// Baut aus den Schalterstellungen die Nachweis-Antworten. <paramref name="acceptedUtc"/> wird
        /// EINMAL uebergeben und nicht je Punkt gestempelt - der Benutzer hat mit einem Klick abgeschickt,
        /// und leicht auseinanderliegende Zeitstempel wuerden eine Genauigkeit vortaeuschen, die es nicht
        /// gibt.
        /// </summary>
        public static List<ConsentAnswer> ToAnswers(this IEnumerable<ConsentPoint> points,
            IReadOnlyDictionary<string, bool> switches, string culture, DateTime acceptedUtc)
        {
            var result = new List<ConsentAnswer>();
            if (points == null)
            {
                return result;
            }

            foreach (ConsentPoint point in points)
            {
                if (point == null || string.IsNullOrWhiteSpace(point.Key))
                {
                    continue;
                }

                bool accepted = switches != null && switches.TryGetValue(point.Key, out bool v) && v;
                result.Add(new ConsentAnswer
                {
                    Key = point.Key,
                    Accepted = accepted,
                    AcceptedUtc = acceptedUtc,
                    Culture = culture,
                    Version = point.Version,
                    HelpSlug = point.HelpSlug,
                    Scope = point.Scope
                });
            }

            return result;
        }
    }
}
