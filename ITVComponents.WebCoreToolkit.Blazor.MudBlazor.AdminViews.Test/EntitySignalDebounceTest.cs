using System;
using ITVComponents.WebCoreToolkit.Caching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Die Sammelfenster der Aenderungs-Meldung - die eine Stelle, an der aus Einstellungen eine Dauer wird.
    /// </summary>
    /// <remarks>
    /// Geprueft wird hier die Auswahl, nicht das Sammeln selbst: dass ein Thema seinen eigenen Wert bekommt
    /// und wie die Vorgabe wirkt. Genau daran haengt, ob eine Rechte-Aenderung sofort zieht, waehrend eine
    /// Fortschritts-Meldung gebremst wird.
    /// </remarks>
    [TestClass]
    public class EntitySignalDebounceTest
    {
        [TestMethod]
        public void WithoutSettings_EverythingIsImmediate()
        {
            var settings = new EntitySignalDebounceSettings();

            Assert.AreEqual(TimeSpan.Zero, settings.WindowFor(EntityChangeTopics.Security));
            Assert.AreEqual(TimeSpan.Zero, settings.WindowFor("anything"));
        }

        [TestMethod]
        public void TheDefaultAppliesToTopicsWithoutTheirOwnWindow()
        {
            var settings = new EntitySignalDebounceSettings { DefaultMilliseconds = 200 };

            Assert.AreEqual(TimeSpan.FromMilliseconds(200), settings.WindowFor("anything"));
        }

        /// <summary>
        /// Der Punkt, um den es geht: Themen sind unterschiedlich eilig. Ein eigener Wert gewinnt gegen die
        /// Vorgabe - auch die 0, sonst koennte man ein einzelnes Thema nicht wieder scharf stellen.
        /// </summary>
        [TestMethod]
        public void ATopicWindowWinsAgainstTheDefault()
        {
            var settings = new EntitySignalDebounceSettings { DefaultMilliseconds = 200 };
            settings.Topics["WorkflowProgress"] = 250;
            settings.Topics[EntityChangeTopics.Security] = 0;

            Assert.AreEqual(TimeSpan.FromMilliseconds(250), settings.WindowFor("WorkflowProgress"));
            Assert.AreEqual(TimeSpan.Zero, settings.WindowFor(EntityChangeTopics.Security));
        }

        /// <summary>Themennamen sind Zeichenketten aus Konfiguration - Schreibweise darf nicht entscheiden.</summary>
        [TestMethod]
        public void TopicNamesAreCaseInsensitive()
        {
            var settings = new EntitySignalDebounceSettings();
            settings.Topics["workflowprogress"] = 250;

            Assert.AreEqual(TimeSpan.FromMilliseconds(250), settings.WindowFor("WorkflowProgress"));
        }
    }
}
