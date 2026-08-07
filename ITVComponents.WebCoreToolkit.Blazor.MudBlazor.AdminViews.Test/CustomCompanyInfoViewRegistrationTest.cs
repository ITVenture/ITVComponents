using System;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Extensibility;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test.Samples;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Der Weg von <c>ViewKey</c> zur eigenen Maske - der Teil, der nicht im Browser haengt.
    /// </summary>
    /// <remarks>
    /// Dass <see cref="SiteSurveyView"/> hier ueberhaupt als Typargument angegeben werden kann, ist bereits
    /// die halbe Aussage: <c>RegisterView&lt;T&gt;</c> verlangt <c>IComponent</c> UND
    /// <c>ICustomCompanyInfoView</c> beim Uebersetzen. Eine Maske, die den Vertrag verletzt, kaeme hier gar
    /// nicht durch den Compiler - und nicht erst dann, wenn ein Benutzer den Reiter oeffnet.
    /// </remarks>
    [TestClass]
    public class CustomCompanyInfoViewRegistrationTest
    {
        [TestMethod]
        public void RegisteredView_IsFoundByItsKey()
        {
            var config = new CustomCompanyInfoViewConfiguration();
            config.RegisterView<SiteSurveyView>("sample.sitesurvey");

            Assert.AreEqual(typeof(SiteSurveyView), config.GetViewType("sample.sitesurvey"));
        }

        /// <summary>
        /// Der Schluessel des Moduls und der registrierte Schluessel muessen zusammenpassen - hier wird
        /// nachgesehen, dass die Vorlage aus dem Leitfaden das auch tut.
        /// </summary>
        [TestMethod]
        public void SampleModule_AnnouncesTheKeyThatIsRegistered()
        {
            var module = new SiteSurveyModule();
            var config = new CustomCompanyInfoViewConfiguration();
            config.RegisterView<SiteSurveyView>(module.ViewKey);

            Assert.IsNotNull(config.GetViewType(module.ViewKey));
        }

        /// <summary>
        /// Gross-/Kleinschreibung darf nicht entscheiden: der Schluessel steht einmal im Code des Moduls und
        /// einmal in der Startkonfiguration des Hosts, und diese beiden Stellen schreibt selten dieselbe
        /// Person.
        /// </summary>
        [TestMethod]
        public void Key_IsCaseInsensitive()
        {
            var config = new CustomCompanyInfoViewConfiguration();
            config.RegisterView<SiteSurveyView>("Sample.SiteSurvey");

            Assert.IsNotNull(config.GetViewType("sample.sitesurvey"));
        }

        /// <summary>
        /// Ein doppelt vergebener Schluessel wird abgewiesen und nicht still ueberschrieben: sonst
        /// entschiede die Reihenfolge der Registrierungen darueber, welche Maske ein Modul bekommt.
        /// </summary>
        [TestMethod]
        public void DuplicateKey_IsRejected()
        {
            var config = new CustomCompanyInfoViewConfiguration();
            config.RegisterView<SiteSurveyView>("sample.sitesurvey");

            Assert.ThrowsException<InvalidOperationException>(
                () => config.RegisterView<SiteSurveyView>("sample.sitesurvey"));
        }

        /// <summary>
        /// Ein unbekannter Schluessel liefert null - die Erfassung faellt dann auf die generische Maske
        /// zurueck, statt den Reiter wegzulassen.
        /// </summary>
        [TestMethod]
        public void UnknownKey_YieldsNull()
        {
            var config = new CustomCompanyInfoViewConfiguration();

            Assert.IsNull(config.GetViewType("gibt.es.nicht"));
            Assert.IsNull(config.GetViewType(null));
            Assert.IsNull(config.GetViewType("   "));
        }
    }
}
