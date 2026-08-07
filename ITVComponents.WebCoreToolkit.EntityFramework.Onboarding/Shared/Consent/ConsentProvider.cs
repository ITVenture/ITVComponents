using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Consent
{
    /// <summary>
    /// Standard-Implementierung von <see cref="IConsentProvider"/>: liest die Punkte aus den GlobalSettings
    /// und legt die Nachweise im Onboarding-Kontext ab.
    /// </summary>
    /// <remarks>
    /// Der Kontext wird je Vorgang geoeffnet und sofort wieder geschlossen (<see cref="IDbContextFactory{TContext}"/>)
    /// - der Provider haengt an einem Blazor-Circuit und darf keinen Kontext ueber die Dauer eines
    /// Formulars halten.
    /// </remarks>
    public class ConsentProvider<TContext> : IConsentProvider
        where TContext : DbContext, IOnboardingConsentContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IGlobalSettings<ConsentOptions> options;
        private readonly ILogger<ConsentProvider<TContext>> logger;

        public ConsentProvider(IDbContextFactory<TContext> dbFactory, IGlobalSettings<ConsentOptions> options,
            ILogger<ConsentProvider<TContext>> logger)
        {
            this.dbFactory = dbFactory;
            this.options = options;
            this.logger = logger;
        }

        public async Task<ConsentSet> DescribeAsync(ConsentOccasionKind occasion, string userId,
            CancellationToken ct = default)
        {
            var result = new ConsentSet();
            IReadOnlyList<ConsentPoint> configured = Configured();
            if (configured.Count == 0)
            {
                return result;
            }

            // Was der Benutzer persoenlich schon beantwortet hat - nur noetig, wenn es ihn gibt UND
            // ueberhaupt ein Punkt in Frage kommt, der sich dadurch erledigen koennte.
            IReadOnlyDictionary<string, string> answered =
                configured.Any(p => p.Scope == ConsentScope.User) && !string.IsNullOrEmpty(userId)
                    ? await AnsweredAsync(userId, ct)
                    : new Dictionary<string, string>(StringComparer.Ordinal);

            var forUser = new List<ConsentPoint>();
            var forTenant = new List<ConsentPoint>();
            foreach (ConsentPoint point in configured)
            {
                switch (point.Scope)
                {
                    case ConsentScope.Tenant:
                        // Beim blossen Anlegen eines Kontos gibt es keinen Mandanten, dem zuzustimmen waere.
                        if (occasion != ConsentOccasionKind.AccountRegistration)
                        {
                            forTenant.Add(point);
                        }

                        break;

                    case ConsentScope.Both:
                        // Entsteht auch ein Mandant, gehoert der Punkt in dessen Teil - und NUR dorthin,
                        // sonst stuende derselbe Schalter zweimal auf einer Seite.
                        if (occasion == ConsentOccasionKind.AccountRegistration)
                        {
                            forUser.Add(point);
                        }
                        else
                        {
                            forTenant.Add(point);
                        }

                        break;

                    default:
                        if (IsAnswered(answered, point))
                        {
                            // Schon beantwortet, in genau dieser Fassung - danach nochmals zu fragen, waere
                            // eine Zumutung ohne Erkenntnisgewinn.
                            continue;
                        }

                        forUser.Add(point);
                        break;
                }
            }

            result.ForUser = forUser;
            result.ForTenant = forTenant;
            return result;
        }

        public ConsentCheckResult Validate(IReadOnlyList<ConsentPoint> points, IReadOnlyDictionary<string, bool> switches)
        {
            if (points == null)
            {
                return ConsentCheckResult.Ok();
            }

            foreach (ConsentPoint point in points)
            {
                if (point == null || !point.Required || string.IsNullOrWhiteSpace(point.Key))
                {
                    continue;
                }

                bool accepted = switches != null && switches.TryGetValue(point.Key, out bool v) && v;
                if (!accepted)
                {
                    return ConsentCheckResult.Missing(point.Key);
                }
            }

            return ConsentCheckResult.Ok();
        }

        public async Task<int> RecordAsync(IReadOnlyList<ConsentAnswer> answers, ConsentSubject subject,
            CancellationToken ct = default)
        {
            if (answers == null || answers.Count == 0)
            {
                return 0;
            }

            if (subject == null)
            {
                logger.LogError("Es sollten {Count} Zustimmungs-Nachweise abgelegt werden, aber es ist nicht bekannt, wem sie gehoeren; es wurde nichts geschrieben.", answers.Count);
                return 0;
            }

            if (string.IsNullOrWhiteSpace(subject.UserId) && string.IsNullOrWhiteSpace(subject.Email))
            {
                // Ein Nachweis ohne jede Kennung ist keiner - er liesse sich niemandem zuordnen.
                logger.LogError("Es sollten {Count} Zustimmungs-Nachweise abgelegt werden, aber weder Benutzer noch E-Mail sind bekannt; es wurde nichts geschrieben.", answers.Count);
                return 0;
            }

            try
            {
                await using TContext db = await dbFactory.CreateDbContextAsync(ct);
                string origin = subject.Origin.ToString();
                int written = 0;
                foreach (ConsentAnswer answer in answers)
                {
                    if (answer == null || string.IsNullOrWhiteSpace(answer.Key))
                    {
                        continue;
                    }

                    db.ConsentRecords.Add(new ConsentRecord
                    {
                        ConsentKey = answer.Key,
                        Version = answer.Version,
                        Accepted = answer.Accepted,
                        // Der Zeitpunkt kommt aus der Antwort, nicht von hier: bei einem geparkten
                        // Onboarding wird Tage nach der Zustimmung geschrieben.
                        AcceptedUtc = answer.AcceptedUtc == default ? DateTime.UtcNow : answer.AcceptedUtc,
                        UserId = subject.UserId,
                        Email = subject.Email,
                        // Eine rein persoenliche Zustimmung bekommt KEINE Mandanten-Nummer, auch wenn sie
                        // bei der Anlage eines Mandanten erteilt wurde: sie gilt der Person und nicht der
                        // Firma. Stuende der Mandant daran, saehe es so aus, als muesste sie mit ihm neu
                        // erteilt werden.
                        TenantId = answer.Scope == ConsentScope.User ? null : subject.TenantId,
                        Culture = answer.Culture,
                        HelpSlug = answer.HelpSlug,
                        Scope = answer.Scope,
                        Origin = origin
                    });
                    written++;
                }

                await db.SaveChangesAsync(ct);
                logger.LogDebug("{Count} Zustimmungs-Nachweise fuer {Subject} abgelegt.", written,
                    subject.UserId ?? subject.Email);
                return written;
            }
            catch (Exception ex)
            {
                // Der Vorgang ist an dieser Stelle durch - der Mandant besteht, das Konto besteht. Ihn
                // nachtraeglich scheitern zu lassen, waere schlimmer als der fehlende Nachweis. Aber still
                // darf es nicht bleiben: ohne diese Zeile faellt erst bei einer Auskunftsanfrage auf, dass
                // die Zustimmung nirgends steht.
                logger.LogError(ex, "Die Zustimmungs-Nachweise fuer {Subject} (Mandant {TenantId}) konnten nicht abgelegt werden; die Zustimmung wurde erteilt, ist aber nicht dokumentiert.",
                    subject.UserId ?? subject.Email, subject.TenantId);
                return 0;
            }
        }

        /// <summary>
        /// Die konfigurierten Punkte, bereinigt: ohne abgeschaltete, ohne schluessellose, ohne Doppel.
        /// </summary>
        private IReadOnlyList<ConsentPoint> Configured()
        {
            ConsentPointOptions[] configured = options.ValueOrDefault?.Points;
            if (configured == null || configured.Length == 0)
            {
                return Array.Empty<ConsentPoint>();
            }

            var result = new List<ConsentPoint>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConsentPointOptions point in configured)
            {
                if (point == null || point.Disabled)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(point.Key))
                {
                    // Ohne Schluessel liesse sich der Nachweis spaeter nicht zuordnen - der Punkt wuerde
                    // angezeigt und seine Zustimmung waere wertlos.
                    logger.LogError("Ein konfigurierter Zustimmungspunkt hat keinen Schluessel und wird uebergangen.");
                    continue;
                }

                if (!seen.Add(point.Key))
                {
                    // Zwei Punkte mit demselben Schluessel wuerden zwei Nachweise schreiben, die sich nicht
                    // auseinanderhalten lassen.
                    logger.LogError("Der Zustimmungspunkt '{Key}' ist mehrfach konfiguriert; nur der erste gilt.", point.Key);
                    continue;
                }

                result.Add(new ConsentPoint
                {
                    Key = point.Key,
                    Label = point.Label,
                    HelpSlug = point.HelpSlug,
                    LinkText = point.LinkText,
                    Required = point.Required,
                    Version = point.Version,
                    Scope = ParseScope(point.Scope, point.Key)
                });
            }

            return result;
        }

        /// <summary>
        /// Liest den Geltungsbereich aus der Konfiguration. Unbekanntes faellt auf
        /// <see cref="ConsentScope.User"/> zurueck - mit Protokollzeile, denn stillschweigend die
        /// vorsichtigste Auslegung zu nehmen, waere hier gefaehrlich: der Betrieb glaubt, er hole eine
        /// Zustimmung des Mandanten ein, und bekommt eine des Benutzers.
        /// </summary>
        private ConsentScope ParseScope(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ConsentScope.User;
            }

            if (Enum.TryParse(value.Trim(), true, out ConsentScope parsed))
            {
                return parsed;
            }

            logger.LogError("Der Zustimmungspunkt '{Key}' nennt den unbekannten Geltungsbereich '{Scope}'; es gilt '{Fallback}'. Erlaubt sind User, Tenant und Both.",
                key, value, ConsentScope.User);
            return ConsentScope.User;
        }

        /// <summary>
        /// Die persoenlich bereits beantworteten Punkte des Benutzers, je Schluessel die Fassung des
        /// juengsten Nachweises.
        /// </summary>
        /// <remarks>
        /// Gefragt wird nach BEANTWORTET, nicht nach zugestimmt: wer den Newsletter einmal abgelehnt hat,
        /// soll nicht bei jeder Gelegenheit erneut gefragt werden. Bei einem Pflicht-Punkt macht das keinen
        /// Unterschied - ohne Zustimmung kommt niemand durch, es kann also gar kein abgelehnter Nachweis
        /// entstanden sein.
        /// <para>
        /// Beruecksichtigt werden nur Nachweise mit <see cref="ConsentScope.User"/>: eine Zustimmung, die
        /// fuer einen Mandanten erteilt wurde, sagt nichts darueber aus, was die Person fuer sich selbst
        /// erklaert hat.
        /// </para>
        /// </remarks>
        private async Task<IReadOnlyDictionary<string, string>> AnsweredAsync(string userId, CancellationToken ct)
        {
            try
            {
                await using TContext db = await dbFactory.CreateDbContextAsync(ct);
                var rows = await db.ConsentRecords.AsNoTracking()
                    .Where(r => r.UserId == userId && r.Scope == ConsentScope.User)
                    .OrderByDescending(r => r.AcceptedUtc)
                    .Select(r => new { r.ConsentKey, r.Version })
                    .ToListAsync(ct);

                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    // Absteigend sortiert - der erste Treffer je Schluessel ist der juengste.
                    if (!result.ContainsKey(row.ConsentKey))
                    {
                        result[row.ConsentKey] = row.Version;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                // Im Zweifel fragen: eine ueberfluessige Frage ist zumutbar, eine uebersprungene
                // Zustimmung nicht.
                logger.LogError(ex, "Die bereits erteilten Zustimmungen von Benutzer {UserId} konnten nicht gelesen werden; es wird erneut gefragt.", userId);
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// Ist der Punkt in der GELTENDEN Fassung beantwortet? Eine neue Fassung macht den alten Nachweis
        /// nicht ungueltig, aber sie verlangt bei der naechsten Gelegenheit eine neue Antwort.
        /// </summary>
        private static bool IsAnswered(IReadOnlyDictionary<string, string> answered, ConsentPoint point)
        {
            if (!answered.TryGetValue(point.Key, out string recorded))
            {
                return false;
            }

            return string.Equals(recorded ?? string.Empty, point.Version ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
