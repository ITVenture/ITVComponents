using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// The JSON language records (<c>{"de":…,"fr":…}</c>) of navigation entries, task forms and modules: whatever
    /// language is asked for, the reader sees a text - never the raw record.
    /// </summary>
    [TestClass]
    public class JsonTranslateTests
    {
        private const string Record = "{\"de\":\"Auszahlungen\",\"en\":\"Payouts\",\"fr\":\"Virements\"}";

        [TestMethod]
        public void Exact_Then_Language_Then_Default()
        {
            Assert.AreEqual("Virements", Record.Translate("fr-CH"));
            Assert.AreEqual("Auszahlungen", Record.Translate("de"));
            Assert.AreEqual("Vorgabe", "{\"de-CH\":\"Schweiz\",\"Default\":\"Vorgabe\"}".Translate("it-CH"));
            Assert.AreEqual("Schweiz", "{\"de-CH\":\"Schweiz\",\"Default\":\"Vorgabe\"}".Translate("de-CH"));
        }

        [TestMethod]
        public void Missing_Language_Without_Default_Falls_Back_To_English_Not_To_The_Raw_Record()
        {
            Assert.AreEqual("Payouts", Record.Translate("it-CH"));
        }

        [TestMethod]
        public void Without_English_The_First_Entry_Is_Shown()
        {
            Assert.AreEqual("Auszahlungen", "{\"de\":\"Auszahlungen\",\"fr\":\"Virements\"}".Translate("it"));
        }

        [TestMethod]
        public void Invariant_Culture_Means_Default()
        {
            Assert.AreEqual("Vorgabe", "{\"de\":\"Deutsch\",\"Default\":\"Vorgabe\"}".Translate(string.Empty));
            Assert.AreEqual("Payouts", Record.Translate(string.Empty), "no Default either: English");
        }

        [TestMethod]
        public void Plain_Text_And_Broken_Records_Stay_As_They_Are()
        {
            Assert.AreEqual("Payouts", "Payouts".Translate("de"));
            Assert.AreEqual("{not json}", "{not json}".Translate("de"));
            Assert.AreEqual("{}", "{}".Translate("de"));
        }

        [TestMethod]
        public void TryTranslate_Tells_A_Match_From_A_Fallback()
        {
            var dic = new Dictionary<string, string> { ["de"] = "Deutsch", ["en"] = "English" };

            Assert.IsTrue(dic.TryTranslate("de-AT", out var match));
            Assert.AreEqual("Deutsch", match);
            Assert.IsFalse(dic.TryTranslate("fr", out _));
            Assert.AreEqual("English", dic.Translate("fr"));
            Assert.AreEqual("none", new Dictionary<string, string>().Translate("fr", "none"), "an empty dictionary yields the given default");
        }
    }
}
