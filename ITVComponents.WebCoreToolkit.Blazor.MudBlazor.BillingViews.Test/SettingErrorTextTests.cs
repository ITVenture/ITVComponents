using System.Collections.Generic;
using System.Globalization;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Test
{
    /// <summary>
    /// Die Geräte-Einstellungen in der Sprache des Benutzers: Beschriftungen und Fehler des Agenten.
    /// </summary>
    /// <remarks>
    /// Der Agent prüft, das Web zeigt an. Was der Agent sagt, ist englisch und nennt das Feld beim Namen; die
    /// Maske macht daraus einen Satz mit der übersetzten Beschriftung.
    /// </remarks>
    [TestClass]
    public class SettingErrorTextTests
    {
        private static readonly IReadOnlyList<SettingDescriptor> Fields =
        [
            new() { Name = "connection", Label = """{"de":"Anschluss","fr":"Connexion","Default":"Connection"}""" },
            new() { Name = "port", Label = "Port" }
        ];

        private CultureInfo? previous;

        [TestInitialize]
        public void SwitchToFrench()
        {
            previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("fr");
        }

        [TestCleanup]
        public void Restore() => CultureInfo.CurrentUICulture = previous!;

        [TestMethod]
        public void Translate_ResolvesAJsonRecordAndLeavesPlainTextAlone()
        {
            Assert.AreEqual("Connexion", SettingFieldProjection.Translate(Fields[0].Label));
            Assert.AreEqual("Port", SettingFieldProjection.Translate("Port"), "An agent writing plain labels notices nothing.");
            Assert.IsNull(SettingFieldProjection.Translate(null));
        }

        [TestMethod]
        public void DescribeError_PhrasesTheKnownKindsWithTheTranslatedLabel()
        {
            var required = new SettingError { SettingName = "connection", Kind = SettingErrorKind.Required, Message = "'connection' is required." };
            var invalid = new SettingError { SettingName = "port", Kind = SettingErrorKind.Invalid, Detail = "not a number", Message = "x" };
            var unknown = new SettingError { SettingName = "bogus", Kind = SettingErrorKind.Unknown, Message = "x" };

            Assert.AreEqual("R:Connexion", Describe(required));
            Assert.AreEqual("I:Port/not a number", Describe(invalid));
            Assert.AreEqual("U:bogus", Describe(unknown));
        }

        [TestMethod]
        public void DescribeError_FallsBackToTheAgentsMessage_ForAnOlderAgent()
        {
            // Ein Agent vor dieser Aenderung setzt keine Art - dann zaehlt sein Text, wie bisher.
            var old = new SettingError { SettingName = "port", Message = "'Port' is required." };
            Assert.AreEqual("'Port' is required.", Describe(old));
        }

        [TestMethod]
        public void DescribeError_FallsBackToTheFieldName_WhenTheFieldIsNotDescribed()
        {
            var error = new SettingError { SettingName = "gone", Kind = SettingErrorKind.Required, Message = "x" };
            Assert.AreEqual("R:gone", Describe(error));
        }

        private static string Describe(SettingError error)
            => SettingFieldProjection.DescribeError(error, Fields, "R:{0}", "I:{0}/{1}", "U:{0}");
    }
}
