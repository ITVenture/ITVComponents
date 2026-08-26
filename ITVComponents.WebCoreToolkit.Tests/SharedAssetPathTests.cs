using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the one place that knows how a shared asset appears in a URL: segment round-trip, the prefix
    /// every link needs, and the canonical form the asset's location check compares against.
    /// </summary>
    [TestClass]
    public class SharedAssetPathTests
    {
        [TestMethod]
        public void Segment_RoundTrips_Without_Token()
        {
            var segment = SharedAssetPath.BuildSegment("abc-123");

            Assert.IsTrue(segment.StartsWith(Global.SharedAssetPathMarker));
            Assert.IsTrue(SharedAssetPath.TryParseSegment(segment, out var key, out var token));
            Assert.AreEqual("abc-123", key);
            Assert.IsNull(token, "a link for signed-in recipients carries no access token");
        }

        [TestMethod]
        public void Segment_RoundTrips_With_Token()
        {
            var segment = SharedAssetPath.BuildSegment("abc-123", "T0k3n-_x");

            Assert.IsTrue(SharedAssetPath.TryParseSegment(segment, out var key, out var token));
            Assert.AreEqual("abc-123", key);
            Assert.AreEqual("T0k3n-_x", token);
        }

        [TestMethod]
        public void Segment_Survives_Keys_That_Are_Not_Url_Safe()
        {
            // The key is host-supplied; encoding it is what keeps a key with a slash or a question mark from
            // silently splitting the segment in two.
            var segment = SharedAssetPath.BuildSegment("a/b?c=d");

            Assert.IsFalse(segment.Contains('/'));
            Assert.IsTrue(SharedAssetPath.TryParseSegment(segment, out var key, out _));
            Assert.AreEqual("a/b?c=d", key);
        }

        [TestMethod]
        public void Malformed_Segment_Is_Rejected_Not_Guessed()
        {
            Assert.IsFalse(SharedAssetPath.TryParseSegment("~", out _, out _));
            Assert.IsFalse(SharedAssetPath.TryParseSegment("~###", out _, out _));
            Assert.IsFalse(SharedAssetPath.TryParseSegment("~.token", out _, out _));
            Assert.IsFalse(SharedAssetPath.TryParseSegment("users", out _, out _));
        }

        [TestMethod]
        public void Prefix_Puts_The_Asset_In_Front_Of_The_Tenant()
        {
            Assert.AreEqual("/~abc/TenantA", SharedAssetPath.BuildPrefix("~abc", "TenantA"));
            Assert.AreEqual("/~abc", SharedAssetPath.BuildPrefix("~abc", null));
            Assert.AreEqual("/TenantA", SharedAssetPath.BuildPrefix(null, "TenantA"));
            Assert.AreEqual(string.Empty, SharedAssetPath.BuildPrefix(null, null));
        }

        [TestMethod]
        public void Canonicalize_Strips_Both_Prefixes_When_They_Lead_The_Path()
        {
            // The MVC shape: the tenant is still a route value, so it is still in the path.
            Assert.AreEqual("/orders/42",
                SharedAssetPath.Canonicalize("/~abc/TenantA/orders/42", "~abc", "TenantA"));
        }

        [TestMethod]
        public void Canonicalize_Is_Correct_When_A_Prefix_Is_Already_Gone()
        {
            // The Blazor shape: the tenant middleware moved the tenant into PathBase before this is asked.
            Assert.AreEqual("/orders/42", SharedAssetPath.Canonicalize("/orders/42", "~abc", "TenantA"));
            Assert.AreEqual("/orders/42", SharedAssetPath.Canonicalize("/TenantA/orders/42", null, "TenantA"));
        }

        [TestMethod]
        public void Canonicalize_Leaves_A_Path_That_Only_Looks_Similar_Alone()
        {
            // "TenantAlpha" starts with "TenantA" - cutting by length instead of by segment would eat half of it.
            Assert.AreEqual("/TenantAlpha/orders",
                SharedAssetPath.Canonicalize("/TenantAlpha/orders", null, "TenantA"));
        }

        [TestMethod]
        public void Canonicalize_Yields_Root_When_Nothing_Is_Left()
        {
            Assert.AreEqual("/", SharedAssetPath.Canonicalize("/~abc/TenantA", "~abc", "TenantA"));
            Assert.AreEqual("/", SharedAssetPath.Canonicalize("/", "~abc", "TenantA"));
        }
    }
}
