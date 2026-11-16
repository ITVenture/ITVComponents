using System;
using System.Globalization;
using System.Linq;
using ITVComponents.WebCoreToolkit.Localization;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Sprache und Zeitzone eines Mandanten, für das, was er ausgibt (Belege, Mails).
    /// </summary>
    /// <remarks>
    /// Der Rückfall ist zweistufig: das Repository liefert den gespeicherten (und im Baum vererbten) Wert, der
    /// Resolver ergänzt die Vorgabe des Hosts. Getestet wird hier die zweite Stufe.
    /// </remarks>
    [TestClass]
    public class TenantLocaleResolverTest
    {
        private static TenantLocaleResolver Create(FakeSecurityRepository repo, string defaultCulture = "de-CH",
            params string[] supported)
        {
            var localization = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(defaultCulture),
                SupportedUICultures = supported.Select(c => new CultureInfo(c)).ToList()
            };
            var cultures = new CultureOptions();
            cultures.MapCulture("de", "de-CH");
            return new TenantLocaleResolver(repo, Microsoft.Extensions.Options.Options.Create(localization),
                Microsoft.Extensions.Options.Options.Create(cultures), NullLogger<TenantLocaleResolver>.Instance);
        }

        [TestMethod]
        public void StoredCulture_Wins_AsStored()
        {
            var repo = new FakeSecurityRepository();
            repo.Locales["shop"] = new TenantLocale { Culture = "fr-CH" };
            var resolver = Create(repo, "de-CH", "de-CH", "fr-CH", "it-CH", "en");

            Assert.AreEqual("fr-CH", resolver.GetUICulture("shop").Name);
        }

        [TestMethod]
        public void StoredLanguageOnly_IsKept_AndGetsItsFormatThroughTheHostMapping()
        {
            var repo = new FakeSecurityRepository();
            repo.Locales["shop"] = new TenantLocale { Culture = "de" };
            var resolver = Create(repo, "de-CH", "de-CH", "fr-CH");

            Assert.AreEqual("de", resolver.GetUICulture("shop").Name);
            Assert.AreEqual("de-CH", resolver.GetCulture("shop").Name, "Number format follows the host mapping when only the language is stored.");
        }

        [TestMethod]
        public void SpecificCulture_PassesTheHostMappingUnchanged()
        {
            // Zuerich und Muenchen auf demselben Host: die Zuordnung des Hosts (de -> de-CH) darf den
            // deutschen Laden nicht zum Schweizer machen.
            var repo = new FakeSecurityRepository();
            repo.Locales["shop"] = new TenantLocale { Culture = "de-DE" };
            var resolver = Create(repo, "de-CH", "de-CH", "de-DE", "fr-CH");

            Assert.AreEqual("de-DE", resolver.GetUICulture("shop").Name);
            Assert.AreEqual("de-DE", resolver.GetCulture("shop").Name);
        }

        [TestMethod]
        public void OtherRegionOfASupportedLanguage_IsKept()
        {
            // de-AT fuehrt der Host nicht eigens; die Texte kommen ueber den Rueckfall des Localizers, das Format
            // bleibt das oesterreichische.
            var repo = new FakeSecurityRepository();
            repo.Locales["shop"] = new TenantLocale { Culture = "de-AT" };
            var resolver = Create(repo, "de-CH", "de-CH", "fr-CH");

            Assert.AreEqual("de-AT", resolver.GetUICulture("shop").Name);
            Assert.AreEqual("de-AT", resolver.GetCulture("shop").Name);
        }

        [TestMethod]
        public void NothingStored_FallsBackToTheHostDefault_AsConfigured()
        {
            var resolver = Create(new FakeSecurityRepository(), "it-CH", "de-CH", "fr-CH", "it-CH");

            Assert.AreEqual("it-CH", resolver.GetUICulture("shop").Name);
        }

        [TestMethod]
        public void UnsupportedLanguage_FallsBackToTheHostDefault()
        {
            // Eine Sprache, die der Host (nicht mehr) anbietet: der Beleg erscheint in der Vorgabe, nicht in
            // einer Sprache, fuer die es keine Texte gibt.
            var repo = new FakeSecurityRepository();
            repo.Locales["shop"] = new TenantLocale { Culture = "es-ES" };
            var resolver = Create(repo, "de-CH", "de-CH", "fr-CH");

            Assert.AreEqual("de-CH", resolver.GetUICulture("shop").Name);
        }

        [TestMethod]
        public void InvalidLanguage_FallsBackInsteadOfThrowing()
        {
            var repo = new FakeSecurityRepository();
            repo.Locales["shop"] = new TenantLocale { Culture = "no such culture!" };
            var resolver = Create(repo, "de-CH", "de-CH", "fr-CH");

            Assert.AreEqual("de-CH", resolver.GetUICulture("shop").Name);
        }

        [TestMethod]
        public void TimeZone_StoredOrServer()
        {
            var repo = new FakeSecurityRepository();
            var zone = TimeZoneInfo.GetSystemTimeZones().First(z => z.Id != TimeZoneInfo.Local.Id);
            repo.Locales["shop"] = new TenantLocale { TimeZone = zone.Id };
            var resolver = Create(repo, "de-CH", "de-CH");

            Assert.AreEqual(zone.Id, resolver.GetTimeZone("shop").Id);
            Assert.AreEqual(TimeZoneInfo.Local.Id, resolver.GetTimeZone("other").Id);
        }

        [TestMethod]
        public void SupportedCultures_AreAsConfigured_WithoutDuplicates()
        {
            var resolver = Create(new FakeSecurityRepository(), "de-CH", "de-CH", "de-DE", "fr-CH", "en", "de-ch");

            CollectionAssert.AreEqual(new[] { "de-CH", "de-DE", "fr-CH", "en" }, resolver.SupportedCultures.Select(c => c.Name).ToArray());
        }

        [TestMethod]
        public void FindSupportedCulture_SameName_ThenSameLanguage_ThenNothing()
        {
            var resolver = Create(new FakeSecurityRepository(), "de-CH", "de-CH", "de-DE", "fr-CH");

            Assert.AreEqual("de-DE", resolver.FindSupportedCulture(new CultureInfo("de-DE"))?.Name);
            Assert.AreEqual("de-CH", resolver.FindSupportedCulture(new CultureInfo("de-AT"))?.Name, "Same language: the first offered entry.");
            Assert.AreEqual("fr-CH", resolver.FindSupportedCulture(new CultureInfo("fr"))?.Name);
            Assert.IsNull(resolver.FindSupportedCulture(new CultureInfo("es-ES")));
            Assert.IsNull(resolver.FindSupportedCulture(CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void FindSupportedCulture_WithoutAList_IsTheCultureItself()
        {
            var resolver = Create(new FakeSecurityRepository(), "de-CH");

            Assert.AreEqual("pt-BR", resolver.FindSupportedCulture(new CultureInfo("pt-BR"))?.Name);
        }

        [TestMethod]
        public void RepositoryIsAskedOncePerTenantAndScope()
        {
            // Ein Beleg fragt Zeitzone, Sprache und Kultur nacheinander - das soll eine Rundreise sein, nicht drei.
            var repo = new FakeSecurityRepository();
            repo.Locales["shop"] = new TenantLocale { Culture = "fr" };
            var resolver = Create(repo, "de-CH", "de-CH", "fr-CH");

            resolver.GetTimeZone("shop");
            resolver.GetUICulture("shop");
            resolver.GetCulture("SHOP");

            Assert.AreEqual(1, repo.LocaleQueries);
        }
    }
}
