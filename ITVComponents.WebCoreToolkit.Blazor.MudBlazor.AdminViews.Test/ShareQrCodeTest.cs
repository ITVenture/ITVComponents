using System;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Der QR-Code einer Asset-Freigabe.
    /// </summary>
    /// <remarks>
    /// Geprueft wird, was ohne Browser pruefbar ist: dass ein Link ein PNG ergibt, dass ein zu langer
    /// Link keine Ausnahme nach oben durchlaesst (Ad-hoc-Tickets tragen die ganze Freigabe im Link und
    /// sprengen jeden QR-Code), und dass der Dateiname aus dem Titel benutzbar bleibt.
    /// </remarks>
    [TestClass]
    public class ShareQrCodeTest
    {
        [TestMethod]
        public void Create_ForALink_YieldsAPng()
        {
            var result = ShareQrCode.Create("https://example.org/sales/order/4711?t=abcdef", NullLogger.Instance);

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNull(result.Error);

            // Die PNG-Signatur - das Bild wird unveraendert gespeichert und gedruckt, also muss es auch
            // wirklich ein PNG sein und nicht irgendein Byte-Haufen.
            var bytes = Convert.FromBase64String(result.PngBase64!);
            CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]);
            Assert.IsTrue(result.DataUri.StartsWith("data:image/png;base64,", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Create_WithoutALink_Fails_ButDoesNotThrow()
        {
            var result = ShareQrCode.Create(" ", NullLogger.Instance);

            Assert.IsFalse(result.Success);
            Assert.IsFalse(string.IsNullOrEmpty(result.Error));
        }

        /// <summary>
        /// Die Linkformen, die es wirklich gibt, muessen hineinpassen - sonst ist der Knopf eine Zusage,
        /// die er nicht haelt.
        /// </summary>
        /// <remarks>
        /// Die Laengen sind nicht geraten, sondern aus dem Linkbau abgeleitet
        /// (<c>SharedAssetPath.BuildSegment</c> / <c>SharedAssetInfoProvider.BuildLink</c>):
        /// <list type="bullet">
        /// <item>angemeldet: Ursprung + <c>/~</c> + Base64Url einer 32-Zeichen-Guid (43) + Mandant + Pfad</item>
        /// <item>anonym: zusaetzlich <c>.</c> + Base64Url des verschluesselten Tokens - 2 Byte Laengen,
        /// 32 Byte Salt, 16 Byte IV und 80 Byte Nutzlast ergeben 174 Zeichen</item>
        /// <item>Ad-hoc-Ticket: <c>/~!</c> + Mandant + <c>.</c> + Base64Url des verschluesselten Ticket-JSON,
        /// je nach Argumentwerten einige hundert Zeichen</item>
        /// </list>
        /// Gemessen ergibt das 49, 73, 97 und 125 Module - alles weit unter der Grenze.
        /// </remarks>
        [DataTestMethod]
        [DataRow(96, DisplayName = "signed-in")]
        [DataRow(271, DisplayName = "anonymous")]
        [DataRow(535, DisplayName = "ad-hoc ticket, small payload")]
        [DataRow(939, DisplayName = "ad-hoc ticket, large payload")]
        [DataRow(2200, DisplayName = "far beyond anything the link builder produces")]
        public void Create_ForTheLinkShapesThatReallyOccur_Succeeds(int length)
        {
            var result = ShareQrCode.Create(Link(length), NullLogger.Instance);

            Assert.IsTrue(result.Success, result.Error);
        }

        /// <summary>
        /// Jenseits von rund 2300 Zeichen ist bei dieser Fehlerkorrekturstufe Schluss. So lang wird kein
        /// Link, den der Linkbau erzeugt - aber wenn doch, muss es als Satz ankommen und darf den Dialog
        /// nicht umwerfen.
        /// </summary>
        [TestMethod]
        public void Create_WithAnOverlongLink_ExplainsItselfInsteadOfThrowing()
        {
            var result = ShareQrCode.Create(Link(4000), NullLogger.Instance);

            Assert.IsFalse(result.Success);
            Assert.IsFalse(string.IsNullOrEmpty(result.Error));
        }

        /// <summary>
        /// Ein Link der gewuenschten Laenge. Kleinbuchstaben mit Absicht: sie zwingen den Code in den
        /// Byte-Modus, in dem auch die echten Base64Url-Abschnitte landen.
        /// </summary>
        private static string Link(int length)
        {
            const string origin = "https://portal.example.com/";
            return origin + new string('a', Math.Max(0, length - origin.Length));
        }

        [TestMethod]
        public void FileName_TurnsTheTitleIntoSomethingSaveable()
        {
            Assert.AreEqual("auftrag-4711-lieferschein-qr.png",
                ShareQrCode.FileName("Auftrag 4711 / Lieferschein"));
        }

        /// <summary>
        /// Ohne brauchbaren Titel darf kein Name wie "-qr.png" oder ".png" entstehen.
        /// </summary>
        [TestMethod]
        public void FileName_WithoutAUsableTitle_FallsBackToAName()
        {
            Assert.AreEqual("share-qr.png", ShareQrCode.FileName(null));
            Assert.AreEqual("share-qr.png", ShareQrCode.FileName("   "));
            Assert.AreEqual("share-qr.png", ShareQrCode.FileName("///"));
        }

        [TestMethod]
        public void FileName_StaysShort()
        {
            var name = ShareQrCode.FileName(new string('x', 300));

            Assert.AreEqual(new string('x', 60) + "-qr.png", name);
        }
    }
}
