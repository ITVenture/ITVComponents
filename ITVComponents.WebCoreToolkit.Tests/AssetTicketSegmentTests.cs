using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the URL form of an ad-hoc ticket. The whole point of the second marker is that a parser
    /// can tell — before touching the database — whether there is anything to look up at all; a ticket
    /// carries everything it needs with it.
    /// </summary>
    [TestClass]
    public class AssetTicketSegmentTests
    {
        [TestMethod]
        public void Ticket_Segment_RoundTrips()
        {
            var segment = SharedAssetPath.BuildTicketSegment("TenantA", "cipher-text");

            Assert.IsTrue(SharedAssetPath.TryParse(segment, out var parsed));
            Assert.AreEqual(AssetSegmentKind.Ticket, parsed.Kind);
            Assert.AreEqual("TenantA", parsed.TenantName);
            Assert.AreEqual("cipher-text", parsed.Payload);
            Assert.IsNull(parsed.AssetKey);
        }

        [TestMethod]
        public void Stored_Asset_Is_Still_Recognized_As_Such()
        {
            var segment = SharedAssetPath.BuildSegment("abc-123", "T0k3n");

            Assert.IsTrue(SharedAssetPath.TryParse(segment, out var parsed));
            Assert.AreEqual(AssetSegmentKind.StoredAsset, parsed.Kind);
            Assert.AreEqual("abc-123", parsed.AssetKey);
            Assert.AreEqual("T0k3n", parsed.AccessToken);
            Assert.IsNull(parsed.TenantName);
        }

        [TestMethod]
        public void A_Ticket_Is_Not_Mistaken_For_An_Asset_Key()
        {
            // Ohne den Marker-Zweig wuerde der Schluessel-Parser das "!" als Base64 lesen und daran
            // scheitern - ein gueltiges Ticket ergaebe einen 404.
            var segment = SharedAssetPath.BuildTicketSegment("TenantA", "cipher");

            Assert.IsFalse(SharedAssetPath.TryParseSegment(segment, out _, out _));
        }

        [TestMethod]
        public void Malformed_Tickets_Are_Refused()
        {
            Assert.IsFalse(SharedAssetPath.TryParse("~!", out _), "no body at all");
            Assert.IsFalse(SharedAssetPath.TryParse("~!nopayload", out _), "no separator");
            Assert.IsFalse(SharedAssetPath.TryParse("~!.cipher", out _), "no tenant");
            Assert.IsFalse(SharedAssetPath.TryParse("~!VGVuYW50QQ.", out _), "no payload");
            Assert.IsFalse(SharedAssetPath.TryParse("~!###.cipher", out _), "tenant is not Base64Url");
        }

        [TestMethod]
        public void The_Tenant_Travels_In_Clear_Because_It_Has_To()
        {
            // Ohne den Mandanten laesst sich der Schluessel zum Entschluesseln nicht bestimmen. Kein
            // Verlust: bei Hosts mit Mandant im Pfad steht er ohnehin in derselben URL.
            var segment = SharedAssetPath.BuildTicketSegment("TenantA", "cipher");

            Assert.IsTrue(SharedAssetPath.TryParse(segment, out var parsed));
            Assert.AreEqual("TenantA", parsed.TenantName);
        }

        [TestMethod]
        public void A_Ticket_Segment_Is_Stripped_Like_Any_Other()
        {
            var segment = SharedAssetPath.BuildTicketSegment("TenantA", "cipher");

            Assert.AreEqual($"/{segment}/TenantA", SharedAssetPath.BuildPrefix(segment, "TenantA"));
            Assert.AreEqual("/orders", SharedAssetPath.Canonicalize($"/{segment}/TenantA/orders", segment, "TenantA"));
        }
    }
}
