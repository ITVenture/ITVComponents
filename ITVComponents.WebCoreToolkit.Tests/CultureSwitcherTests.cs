using System.Collections.Generic;
using System.Globalization;
using ITVComponents.WebCoreToolkit.Blazor.Localization;
using ITVComponents.WebCoreToolkit.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms a language switch exchanges the culture prefix instead of stacking another one on top,
    /// keeps everything behind it (tenant, asset, page path, query, fragment) untouched, and therefore
    /// stays a switch no matter how often it is repeated.
    /// </summary>
    [TestClass]
    public class CultureSwitcherTests
    {
        [TestMethod]
        public void Switch_Exchanges_The_Prefix_And_Keeps_The_Rest()
        {
            var switcher = NewSwitcher("https://app/c/de-CH/ADM/orders?id=42#top");

            Assert.AreEqual("/c/fr/ADM/orders?id=42#top", switcher.BuildUrlFor("fr"));
        }

        [TestMethod]
        public void Switching_Twice_Does_Not_Stack_Prefixes()
        {
            // The failure this service exists to prevent: prepending instead of replacing turns the second
            // switch into /c/fr/c/it/… and the tenant is suddenly one segment too deep.
            var once = NewSwitcher("https://app/c/de-CH/ADM/orders").BuildUrlFor("fr");
            var twice = NewSwitcher("https://app" + once).BuildUrlFor("it");

            Assert.AreEqual("/c/fr/ADM/orders", once);
            Assert.AreEqual("/c/it/ADM/orders", twice);
        }

        [TestMethod]
        public void First_Choice_On_A_Path_Without_A_Prefix_Adds_One()
        {
            // The visitor arrived through Accept-Language and picks a language for the first time.
            var switcher = NewSwitcher("https://app/ADM/orders");

            Assert.AreEqual("/c/fr/ADM/orders", switcher.BuildUrlFor("fr"));
        }

        [TestMethod]
        public void Empty_Culture_Drops_The_Prefix()
        {
            // "System language" as an entry in the picker: back to cookie / Accept-Language.
            var switcher = NewSwitcher("https://app/c/de-CH/ADM/orders");

            Assert.AreEqual("/ADM/orders", switcher.BuildUrlFor(null));
        }

        [TestMethod]
        public void The_Asset_Segment_Survives_A_Language_Switch()
        {
            var switcher = NewSwitcher("https://app/c/de-CH/~QWJjZGVm/ADM/orders/42");

            Assert.AreEqual("/c/fr/~QWJjZGVm/ADM/orders/42", switcher.BuildUrlFor("fr"),
                "a shared link that gets translated is still the same shared link");
        }

        [TestMethod]
        public void Current_Culture_Comes_From_The_Url_When_There_Is_One()
        {
            Assert.AreEqual("de-CH", NewSwitcher("https://app/c/de-CH/ADM/orders").CurrentCulture);
        }

        [TestMethod]
        public void Available_Cultures_Are_The_Supported_Ui_Cultures()
        {
            var available = NewSwitcher("https://app/ADM/orders").AvailableCultures;

            Assert.AreEqual(3, available.Count);
            Assert.AreEqual("de-CH", available[0].Name);
        }

        [TestMethod]
        public void Replace_Is_Reversible_On_The_Root()
        {
            Assert.AreEqual("/c/fr/", CulturePath.Replace("/c/de-CH/", "fr"));
            Assert.AreEqual("/", CulturePath.Replace("/c/de-CH/", null));
        }

        private static ICultureSwitcher NewSwitcher(string uri)
        {
            var localization = new RequestLocalizationOptions
            {
                SupportedUICultures = new List<CultureInfo>
                {
                    new CultureInfo("de-CH"),
                    new CultureInfo("fr"),
                    new CultureInfo("it")
                }
            };

            return new CultureSwitcher(
                new TestNavigationManager(uri),
                Microsoft.Extensions.Options.Options.Create(localization),
                NullLogger<CultureSwitcher>.Instance);
        }

        /// <summary>
        /// A navigation manager sitting at a fixed address - enough for everything the switcher reads.
        /// </summary>
        private sealed class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager(string uri)
            {
                var root = new System.Uri(uri).GetLeftPart(System.UriPartial.Authority) + "/";
                Initialize(root, uri);
            }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                LastNavigation = uri;
                LastForceLoad = forceLoad;
            }

            public string LastNavigation { get; private set; }

            public bool LastForceLoad { get; private set; }
        }

        [TestMethod]
        public void Switching_Is_A_Full_Page_Load()
        {
            // The base href changes with the language, so a soft navigation would leave the circuit
            // pointing at a base it no longer has.
            var navigation = new TestNavigationManager("https://app/c/de-CH/ADM/orders");
            var localization = new RequestLocalizationOptions
            {
                SupportedUICultures = new List<CultureInfo> { new CultureInfo("de-CH"), new CultureInfo("fr") }
            };
            var switcher = new CultureSwitcher(navigation,
                Microsoft.Extensions.Options.Options.Create(localization),
                NullLogger<CultureSwitcher>.Instance);

            switcher.SwitchTo("fr");

            Assert.AreEqual("/c/fr/ADM/orders", navigation.LastNavigation);
            Assert.IsTrue(navigation.LastForceLoad);
        }
    }
}
