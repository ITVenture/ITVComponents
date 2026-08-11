using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Der Weg von <c>RendererKey</c> zum Renderer - der Teil, der nicht im Browser haengt.
    /// </summary>
    /// <remarks>
    /// Dass <see cref="ScribanWidgetRenderer"/> hier ueberhaupt als Typargument angegeben werden kann, ist
    /// bereits die halbe Aussage: <c>RegisterRenderer&lt;T&gt;</c> verlangt <c>IComponent</c> UND
    /// <c>IWidgetRenderer</c>.
    /// </remarks>
    [TestClass]
    public class WidgetRendererRegistrationTest
    {
        /// <summary>
        /// Ohne Argumente registriert: Schluessel und Beschriftungen kommen aus dem Attribut am Typ. Genau
        /// das ist der Punkt daran - der Schluessel steht einmal und nicht an jeder Registrierung neu.
        /// </summary>
        [TestMethod]
        public void KeyAndLabels_ComeFromTheAttribute()
        {
            var config = new WidgetRendererConfiguration();
            config.RegisterRenderer<ScribanWidgetRenderer>();

            WidgetRendererDescriptor? descriptor = config.Get("scriban");
            Assert.IsNotNull(descriptor);
            Assert.AreEqual(typeof(ScribanWidgetRenderer), descriptor!.ComponentType);
            Assert.AreEqual("html", descriptor.EditorLanguage);
            StringAssert.Contains(descriptor.DisplayName, "Scriban");
        }

        /// <summary>
        /// Der leere Schluessel ist gueltig und meint "Widgets ohne Angabe" - die Zeilen aus der Zeit vor
        /// der Spalte. Ohne diesen Eintrag saehe jedes bestehende Widget eine Fehler-Kachel.
        /// </summary>
        [TestMethod]
        public void EmptyKey_IsTheDefaultRenderer()
        {
            var config = new WidgetRendererConfiguration();
            config.RegisterRenderer<ScribanWidgetRenderer>(string.Empty);

            Assert.IsNotNull(config.Get(null));
            Assert.IsNotNull(config.Get(string.Empty));
        }

        /// <summary>
        /// Gross-/Kleinschreibung darf nicht entscheiden: der Schluessel steht einmal am Typ und einmal in
        /// einer Datenbankspalte, die ein Mensch fuellt.
        /// </summary>
        [TestMethod]
        public void Key_IsCaseInsensitive()
        {
            var config = new WidgetRendererConfiguration();
            config.RegisterRenderer<ScribanWidgetRenderer>("Chart.Scriban");

            Assert.IsNotNull(config.Get("chart.scriban"));
        }

        /// <summary>
        /// Ein doppelt vergebener Schluessel wird abgewiesen und nicht still ueberschrieben: sonst
        /// entschiede die Reihenfolge der Registrierungen darueber, womit eine Kachel gezeichnet wird.
        /// </summary>
        [TestMethod]
        public void DuplicateKey_IsRejected()
        {
            var config = new WidgetRendererConfiguration();
            config.RegisterRenderer<ScribanWidgetRenderer>("chart");

            Assert.ThrowsException<InvalidOperationException>(
                () => config.RegisterRenderer<ScribanWidgetRenderer>("chart"));
        }

        /// <summary>
        /// Der Typ-Weg ist der fuer die Konfiguration, wo ein String steht und der Uebersetzer nichts
        /// pruefen kann. Er muss die Pruefung deshalb selbst machen - beim START und nicht beim Zeichnen.
        /// </summary>
        [TestMethod]
        public void TypeWithoutContract_IsRejected()
        {
            var config = new WidgetRendererConfiguration();

            Assert.ThrowsException<ArgumentException>(() => config.RegisterRenderer(typeof(string), "nope"));
        }

        /// <summary>
        /// Ein Typ ohne Attribut und ohne mitgegebenen Schluessel ist ein Registrierungsfehler - nicht
        /// einer, der als leere Auswahl im Editor endet.
        /// </summary>
        [TestMethod]
        public void MissingKey_IsRejected()
        {
            var config = new WidgetRendererConfiguration();

            Assert.ThrowsException<ArgumentException>(() => config.RegisterRenderer(typeof(NoAttributeRenderer)));
        }

        /// <summary>Die Meldung der Fehler-Kachel braucht die Liste der verfuegbaren Schluessel.</summary>
        [TestMethod]
        public void KnownKeys_NamesTheDefaultReadably()
        {
            var config = new WidgetRendererConfiguration();
            config.RegisterRenderer<ScribanWidgetRenderer>(string.Empty);
            config.RegisterRenderer<ScribanWidgetRenderer>("chart");

            CollectionAssert.AreEquivalent(new[] { "(default)", "chart" }, config.KnownKeys.ToArray());
        }
    }
}
