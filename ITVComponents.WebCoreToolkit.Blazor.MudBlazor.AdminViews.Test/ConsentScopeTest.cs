using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Consent;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Die Verteilung der Zustimmungspunkte auf Konto- und Firmen-Teil, je nach Geltungsbereich und
    /// Vorgang.
    /// </summary>
    /// <remarks>
    /// Die Regeln sind klein, aber nicht offensichtlich - besonders <c>Both</c>: der Punkt erscheint beim
    /// Anlegen eines Kontos im Konto-Teil, bei einer Mandanten-Anlage dagegen im Firmen-Teil, und dort NUR
    /// EINMAL. Wer das spaeter umbaut, soll es hier merken und nicht am Kunden.
    /// <para>
    /// Alle Faelle laufen ohne angemeldeten Benutzer. Das ist kein Ausweichen vor der Datenbank, sondern
    /// eine eigene Aussage: solange niemand angemeldet ist, gibt es nichts nachzusehen, und der Provider
    /// darf die Datenbank gar nicht erst anfassen. Die Kontext-Fabrik hier wirft deshalb - ein Test, der
    /// sie ausloest, faellt sofort auf.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ConsentScopeTest
    {
        [TestMethod]
        public async Task AccountRegistration_TakesUserAndBoth_ButNeverTenant()
        {
            ConsentSet set = await DescribeAsync(ConsentOccasionKind.AccountRegistration);

            CollectionAssert.AreEquivalent(new[] { "privacy", "tos" }, Keys(set.ForUser));
            Assert.AreEqual(0, set.ForTenant.Count,
                "Beim blossen Anlegen eines Kontos gibt es keinen Mandanten, dem zuzustimmen waere.");
        }

        [TestMethod]
        public async Task TenantOnboarding_PutsBothIntoTheTenantPart()
        {
            ConsentSet set = await DescribeAsync(ConsentOccasionKind.TenantOnboarding);

            CollectionAssert.AreEquivalent(new[] { "privacy" }, Keys(set.ForUser));
            CollectionAssert.AreEquivalent(new[] { "tos", "contract" }, Keys(set.ForTenant));
        }

        /// <summary>
        /// Der Fall, fuer den die Aufteilung ueberhaupt gebraucht wird: Konto UND Mandant entstehen in
        /// einem Zug. Persoenliches gehoert zum Konto-Teil, alles andere zum Firmen-Teil.
        /// </summary>
        [TestMethod]
        public async Task AccountAndTenant_SplitsByScope()
        {
            ConsentSet set = await DescribeAsync(ConsentOccasionKind.AccountAndTenant);

            CollectionAssert.AreEquivalent(new[] { "privacy" }, Keys(set.ForUser));
            CollectionAssert.AreEquivalent(new[] { "tos", "contract" }, Keys(set.ForTenant));
        }

        /// <summary>
        /// Ein Punkt darf nie in beiden Listen stehen - sonst stuende derselbe Schalter zweimal auf einer
        /// Seite und der Benutzer muesste zweimal dasselbe bestaetigen.
        /// </summary>
        [TestMethod]
        public async Task NoPoint_AppearsInBothLists()
        {
            foreach (ConsentOccasionKind kind in Enum.GetValues<ConsentOccasionKind>())
            {
                ConsentSet set = await DescribeAsync(kind);
                string[] twice = Keys(set.ForUser).Intersect(Keys(set.ForTenant)).ToArray();

                Assert.AreEqual(0, twice.Length,
                    $"'{string.Join(", ", twice)}' steht bei {kind} in beiden Listen.");
            }
        }

        /// <summary>
        /// Fehlt der Geltungsbereich oder ist er unbekannt, gilt <c>User</c>. Die vorsichtigere Auslegung
        /// waere <c>Tenant</c> - aber die persoenliche Zustimmung ist der haeufigere Fall, und ein
        /// Tippfehler soll nicht dazu fuehren, dass eine Datenschutz-Zustimmung am Mandanten haengt.
        /// </summary>
        [TestMethod]
        public async Task UnknownOrMissingScope_FallsBackToUser()
        {
            var options = new ConsentOptions
            {
                Points = new[]
                {
                    new ConsentPointOptions { Key = "kein-scope" },
                    new ConsentPointOptions { Key = "quatsch", Scope = "Voelliger Unfug" }
                }
            };

            ConsentSet set = await DescribeAsync(ConsentOccasionKind.AccountAndTenant, options);

            CollectionAssert.AreEquivalent(new[] { "kein-scope", "quatsch" }, Keys(set.ForUser));
            Assert.AreEqual(0, set.ForTenant.Count);
        }

        [TestMethod]
        public async Task ScopeParsing_IgnoresCasing()
        {
            var options = new ConsentOptions
            {
                Points = new[] { new ConsentPointOptions { Key = "vertrag", Scope = "tEnAnT" } }
            };

            ConsentSet set = await DescribeAsync(ConsentOccasionKind.TenantOnboarding, options);

            CollectionAssert.AreEquivalent(new[] { "vertrag" }, Keys(set.ForTenant));
        }

        /// <summary>
        /// Abgeschaltete, schluessellose und doppelt vergebene Punkte fliegen raus. Beim Doppel gilt der
        /// erste - zwei Nachweise unter demselben Schluessel liessen sich spaeter nicht auseinanderhalten.
        /// </summary>
        [TestMethod]
        public async Task Disabled_Keyless_AndDuplicatePoints_AreDropped()
        {
            var options = new ConsentOptions
            {
                Points = new[]
                {
                    new ConsentPointOptions { Key = "gilt", Label = "erster" },
                    new ConsentPointOptions { Key = "gilt", Label = "zweiter" },
                    new ConsentPointOptions { Key = "aus", Disabled = true },
                    new ConsentPointOptions { Key = "   " },
                    null!
                }
            };

            ConsentSet set = await DescribeAsync(ConsentOccasionKind.AccountRegistration, options);

            CollectionAssert.AreEquivalent(new[] { "gilt" }, Keys(set.ForUser));
            Assert.AreEqual("erster", set.ForUser[0].Label, "Beim Doppel gilt der erste.");
        }

        [TestMethod]
        public async Task NothingConfigured_YieldsNothing()
        {
            ConsentSet set = await DescribeAsync(ConsentOccasionKind.AccountAndTenant, new ConsentOptions());

            Assert.IsFalse(set.Any);
        }

        [TestMethod]
        public void Validate_ReportsTheFirstMissingRequiredPoint()
        {
            IConsentProvider provider = Provider(Standard());
            var points = new[]
            {
                new ConsentPoint { Key = "tos", Required = true },
                new ConsentPoint { Key = "privacy", Required = true },
                new ConsentPoint { Key = "newsletter", Required = false }
            };

            ConsentCheckResult result = provider.Validate(points,
                new Dictionary<string, bool> { ["tos"] = true });

            Assert.IsFalse(result.Valid);
            Assert.AreEqual("privacy", result.MissingKey);
        }

        /// <summary>
        /// Ein freiwilliger Punkt darf nie blockieren - er ist die Frage nach dem Newsletter, nicht nach
        /// den Nutzungsbedingungen.
        /// </summary>
        [TestMethod]
        public void Validate_PassesWhenOnlyOptionalPointsAreUnchecked()
        {
            IConsentProvider provider = Provider(Standard());
            var points = new[]
            {
                new ConsentPoint { Key = "tos", Required = true },
                new ConsentPoint { Key = "newsletter", Required = false }
            };

            ConsentCheckResult result = provider.Validate(points,
                new Dictionary<string, bool> { ["tos"] = true, ["newsletter"] = false });

            Assert.IsTrue(result.Valid);
        }

        // ---- Hilfsmittel ----------------------------------------------------------------------------

        /// <summary>Nutzungsbedingungen als Both, Datenschutz als User, ein reiner Mandanten-Vertrag.</summary>
        private static ConsentOptions Standard()
            => new ConsentOptions
            {
                Points = new[]
                {
                    new ConsentPointOptions { Key = "tos", Scope = "Both", Required = true },
                    new ConsentPointOptions { Key = "privacy", Scope = "User", Required = true },
                    new ConsentPointOptions { Key = "contract", Scope = "Tenant", Required = true }
                }
            };

        /// <summary>
        /// Beschreibt den Vorgang OHNE angemeldeten Benutzer - siehe die Anmerkung an der Klasse.
        /// </summary>
        private static Task<ConsentSet> DescribeAsync(ConsentOccasionKind kind, ConsentOptions? options = null)
            // null als Benutzer ist ausdruecklich vorgesehen ("sofern es ihn schon gibt"); der Vertrag ist
            // nur nicht nullable-annotiert, weil seine Bibliothek ohne Nullable-Kontext uebersetzt.
            => Provider(options ?? Standard()).DescribeAsync(kind, null!);

        private static IConsentProvider Provider(ConsentOptions options)
            => new ConsentProvider<FakeConsentContext>(
                new ExplodingFactory(),
                new FixedSettings<ConsentOptions>(options),
                NullLogger<ConsentProvider<FakeConsentContext>>.Instance);

        private static string[] Keys(IReadOnlyList<ConsentPoint> points)
            => points.Select(p => p.Key).ToArray();

        /// <summary>Ein Kontext, den es nur gibt, damit der Provider seinen Typparameter bekommt.</summary>
        private class FakeConsentContext : DbContext, IOnboardingConsentContext
        {
            public DbSet<ConsentRecord> ConsentRecords { get; set; }
        }

        /// <summary>
        /// Wirft beim ersten Zugriff. Damit ist "der Provider fasst ohne Benutzer keine Datenbank an" nicht
        /// nur behauptet, sondern gepruft.
        /// </summary>
        private sealed class ExplodingFactory : IDbContextFactory<FakeConsentContext>
        {
            public FakeConsentContext CreateDbContext()
                => throw new InvalidOperationException(
                    "Ohne angemeldeten Benutzer darf der Provider die Datenbank nicht anfassen.");
        }

        private sealed class FixedSettings<T> : IGlobalSettings<T> where T : class, new()
        {
            private readonly T value;

            public FixedSettings(T value)
            {
                this.value = value;
            }

            public T Value => value ?? new T();

            public T ValueOrDefault => value;

            public T GetValue(string explicitSettingName) => Value;

            public T GetValueOrDefault(string explicitSettingName) => value;
        }
    }
}
