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
            IReadOnlyDictionary<string, ConsentRecord> answered =
                configured.Any(p => p.Scope == ConsentScope.User) && !string.IsNullOrEmpty(userId)
                    ? await LatestPersonalAsync(userId, ct)
                    : new Dictionary<string, ConsentRecord>(StringComparer.Ordinal);

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
                logger.LogError("{Count} consent records were to be persisted, but their owner is unknown; nothing was written.", answers.Count);
                return 0;
            }

            if (string.IsNullOrWhiteSpace(subject.UserId) && string.IsNullOrWhiteSpace(subject.Email))
            {
                // Ein Nachweis ohne jede Kennung ist keiner - er liesse sich niemandem zuordnen.
                logger.LogError("{Count} consent records were to be persisted, but neither user nor e-mail is known; nothing was written.", answers.Count);
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
                logger.LogDebug("Persisted {Count} consent records for {Subject}.", written,
                    subject.UserId ?? subject.Email);
                return written;
            }
            catch (Exception ex)
            {
                // Der Vorgang ist an dieser Stelle durch - der Mandant besteht, das Konto besteht. Ihn
                // nachtraeglich scheitern zu lassen, waere schlimmer als der fehlende Nachweis. Aber still
                // darf es nicht bleiben: ohne diese Zeile faellt erst bei einer Auskunftsanfrage auf, dass
                // die Zustimmung nirgends steht.
                logger.LogError(ex, "Could not persist the consent records for {Subject} (tenant {TenantId}); consent was given but is not documented.",
                    subject.UserId ?? subject.Email, subject.TenantId);
                return 0;
            }
        }

        public async Task<IReadOnlyList<ConsentStanding>> GetStandingAsync(string userId,
            CancellationToken ct = default)
        {
            ConsentPoint[] personal = Configured().Where(p => p.Scope == ConsentScope.User).ToArray();
            if (personal.Length == 0 || string.IsNullOrEmpty(userId))
            {
                return Array.Empty<ConsentStanding>();
            }

            IReadOnlyDictionary<string, ConsentRecord> latest = await LatestPersonalAsync(userId, ct);

            var result = new List<ConsentStanding>();
            foreach (ConsentPoint point in personal)
            {
                var standing = new ConsentStanding { Point = point };
                if (latest.TryGetValue(point.Key, out ConsentRecord record))
                {
                    standing.Answered = true;
                    standing.Accepted = record.Accepted;
                    standing.AnsweredUtc = record.AcceptedUtc;
                    standing.AnsweredVersion = record.Version;
                    standing.Current = string.Equals(record.Version ?? string.Empty,
                        point.Version ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                }

                result.Add(standing);
            }

            return result;
        }

        /// <summary>
        /// Der juengste persoenliche Nachweis je Schluessel. Aeltere bleiben stehen - sie sind die
        /// Geschichte, und eine Zustimmung nachtraeglich zu ueberschreiben hiesse, den Nachweis zu
        /// faelschen.
        /// </summary>
        private async Task<IReadOnlyDictionary<string, ConsentRecord>> LatestPersonalAsync(string userId,
            CancellationToken ct)
        {
            var result = new Dictionary<string, ConsentRecord>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using TContext db = await dbFactory.CreateDbContextAsync(ct);
                List<ConsentRecord> rows = await db.ConsentRecords.AsNoTracking()
                    .Where(r => r.UserId == userId && r.Scope == ConsentScope.User)
                    .OrderByDescending(r => r.AcceptedUtc)
                    .ToListAsync(ct);

                foreach (ConsentRecord row in rows)
                {
                    // Absteigend sortiert - der erste Treffer je Schluessel ist der juengste.
                    if (!result.ContainsKey(row.ConsentKey))
                    {
                        result[row.ConsentKey] = row;
                    }
                }
            }
            catch (Exception ex)
            {
                // Die Maske zeigt dann "nie gefragt". Das ist die harmlosere Auskunft als eine erfundene
                // Zustimmung - aber ohne diese Zeile bliebe unklar, warum sie nichts weiss.
                logger.LogError(ex, "Could not read the consents of user {UserId}; the form shows them as unanswered.", userId);
            }

            return result;
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
                    logger.LogError("A configured consent point has no key and is skipped.");
                    continue;
                }

                if (!seen.Add(point.Key))
                {
                    // Zwei Punkte mit demselben Schluessel wuerden zwei Nachweise schreiben, die sich nicht
                    // auseinanderhalten lassen.
                    logger.LogError("The consent point '{Key}' is configured more than once; only the first one applies.", point.Key);
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

            logger.LogError("The consent point '{Key}' names the unknown scope '{Scope}'; '{Fallback}' applies. Allowed are User, Tenant and Both.",
                key, value, ConsentScope.User);
            return ConsentScope.User;
        }

        /// <summary>
        /// Ist der Punkt in der GELTENDEN Fassung beantwortet? Eine neue Fassung macht den alten Nachweis
        /// nicht ungueltig, aber sie verlangt bei der naechsten Gelegenheit eine neue Antwort.
        /// </summary>
        /// <remarks>
        /// Gefragt wird nach BEANTWORTET, nicht nach zugestimmt: wer den Newsletter einmal abgelehnt hat,
        /// soll nicht bei jeder Gelegenheit erneut gefragt werden. Bei einem Pflicht-Punkt macht das keinen
        /// Unterschied - ohne Zustimmung kommt niemand durch, es kann also gar kein abgelehnter Nachweis
        /// entstanden sein.
        /// </remarks>
        private static bool IsAnswered(IReadOnlyDictionary<string, ConsentRecord> answered, ConsentPoint point)
        {
            if (!answered.TryGetValue(point.Key, out ConsentRecord recorded))
            {
                return false;
            }

            return string.Equals(recorded.Version ?? string.Empty, point.Version ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
