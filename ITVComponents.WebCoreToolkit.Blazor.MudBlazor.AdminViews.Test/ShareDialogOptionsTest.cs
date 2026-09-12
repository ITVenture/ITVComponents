using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Was eine Vorlage ueber die Teilen-Maske vorgibt.
    /// </summary>
    /// <remarks>
    /// Die beiden Regeln, auf die es ankommt: eine Vorlage, die nichts sagt, veraendert nichts - und
    /// eine, die Unsinn sagt, verhindert nichts. Das Zweite ist die Gegenrichtung zum Rest dieses
    /// Bereichs und deshalb leicht zu "korrigieren", wenn es nicht festgehalten ist: es steuert die
    /// BEDIENUNG, nicht den Zugriff.
    /// </remarks>
    [TestClass]
    public class ShareDialogOptionsTest
    {
        [TestMethod]
        public void A_Template_That_Says_Nothing_Changes_Nothing()
        {
            foreach (var nothing in new[] { null, "", "   " })
            {
                var options = ShareDialogOptions.Parse(nothing, out var error);

                Assert.IsNull(error);
                Assert.IsTrue(options.Title.Shown);
                Assert.IsTrue(options.Reach.Shown);
                Assert.IsTrue(options.Anonymous.Shown);
                Assert.IsNull(options.Title.Value);
                Assert.AreEqual(0, options.Validate().Count);
            }
        }

        [TestMethod]
        public void The_Shop_Entrance_Case_Hides_Everything_And_Presets_It()
        {
            var options = ShareDialogOptions.Parse("""
                {
                  "Title":     { "Value": "Self-Checkout", "Visible": false },
                  "AdHoc":     { "Value": false,           "Visible": false },
                  "Anonymous": { "Value": true,            "Visible": false },
                  "Recipient": { "Visible": false },
                  "Reach":     { "Visible": false }
                }
                """, out var error);

            Assert.IsNull(error);
            Assert.IsFalse(options.Title.Shown);
            Assert.AreEqual("Self-Checkout", options.Title.Value);
            Assert.AreEqual(false, options.AdHoc.Value);
            Assert.AreEqual(true, options.Anonymous.Value);

            // Die Reichweite ist ausgeblendet und ohne Wert - aber die Freigabe ist anonym, und dort IST
            // "ohne Anmeldung" die Reichweite. Kein Fehler.
            Assert.AreEqual(0, options.Validate().Count, string.Join(" / ", options.Validate()));
        }

        /// <summary>
        /// Die Gegenrichtung zum Rest dieses Bereichs, und zwar mit Absicht: unlesbares JSON gibt nichts
        /// vor, verhindert aber nichts. Wer wegen eines Tippfehlers gar nicht mehr teilen kann, hat ein
        /// groesseres Problem als eine fehlende Vorbelegung.
        /// </summary>
        [TestMethod]
        public void Broken_Json_Presets_Nothing_But_Blocks_Nothing()
        {
            var options = ShareDialogOptions.Parse("{ this is not json", out var error);

            Assert.IsFalse(string.IsNullOrEmpty(error), "the reason must reach the log");
            Assert.IsTrue(options.Title.Shown, "the form has to stay usable");
            Assert.AreEqual(0, options.Validate().Count);
        }

        [TestMethod]
        public void A_Hidden_Title_Without_A_Preset_Is_A_Configuration_Error()
        {
            var options = ShareDialogOptions.Parse("""{ "Title": { "Visible": false } }""", out _);

            Assert.AreEqual(1, options.Validate().Count);
            StringAssert.Contains(options.Validate()[0], "title");
        }

        /// <summary>
        /// Ohne anonym und ohne Ad-hoc ist die Reichweite Pflicht - dann darf sie nicht ohne Wert
        /// verschwinden, sonst entstuende eine Freigabe, die niemand benutzen kann.
        /// </summary>
        [TestMethod]
        public void A_Hidden_Reach_Without_A_Preset_Is_An_Error_Only_Where_It_Is_Required()
        {
            var stored = ShareDialogOptions.Parse("""
                { "Title": { "Value": "x" }, "Reach": { "Visible": false } }
                """, out _);
            Assert.AreEqual(1, stored.Validate().Count);

            var anonymous = ShareDialogOptions.Parse("""
                { "Title": { "Value": "x" }, "Anonymous": { "Value": true }, "Reach": { "Visible": false } }
                """, out _);
            Assert.AreEqual(0, anonymous.Validate().Count);

            var ticket = ShareDialogOptions.Parse("""
                { "Title": { "Value": "x" }, "AdHoc": { "Value": true }, "Reach": { "Visible": false } }
                """, out _);
            Assert.AreEqual(0, ticket.Validate().Count);
        }

        [TestMethod]
        public void The_Reach_Survives_A_Round_Trip_By_Name()
        {
            var options = new ShareDialogOptions();
            options.Reach.Value = ShareReach.Tenants;
            options.Reach.Visible = false;
            options.Title.Value = "x";

            var json = options.ToJson();
            StringAssert.Contains(json, "Tenants", "the name, not the number - numbers shift when the enum grows");

            var back = ShareDialogOptions.Parse(json, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(ShareReach.Tenants, back.Reach.Value);
            Assert.IsFalse(back.Reach.Shown);
        }

        /// <summary>
        /// Eine Angabe, die nichts sagt, soll auch nichts speichern - sonst stuende in jeder Vorlage ein
        /// JSON-Block, der nichts bedeutet.
        /// </summary>
        [TestMethod]
        public void An_Empty_Setting_Is_Stored_As_Nothing()
        {
            Assert.IsNull(new ShareDialogOptions().ToJson());
        }
    }
}
