using System;
using System.Collections.Generic;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Routing.Impl;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the two placeholder families of <see cref="UrlFormatImpl"/>. They exist because two kinds of
    /// caller need two different answers: a root-absolute <c>href</c> needs the WHOLE prefix, while a caller
    /// that writes <c>~[…]</c> lets the client script prepend the base url first and must only receive what
    /// is NOT in there yet. Handing the full prefix to the second kind prepends it twice.
    /// </summary>
    [TestClass]
    public class UrlFormatImplTests
    {
        [TestMethod]
        public void Full_Prefix_Puts_The_Asset_In_Front_Of_The_Tenant()
        {
            var format = NewFormat("TenantA", "~abc", pathBase: "/~abc");

            Assert.AreEqual("/~abc/TenantA/help/x", format.FormatUrl("[SlashPermissionScope]/help/x"));
        }

        [TestMethod]
        public void Full_Prefix_Without_Asset_Is_What_It_Always_Was()
        {
            var format = NewFormat("TenantA", null, pathBase: string.Empty);

            Assert.AreEqual("/TenantA/help/x", format.FormatUrl("[SlashPermissionScope]/help/x"));
        }

        [TestMethod]
        public void UnderBase_Yields_Only_The_Tenant_When_The_Asset_Is_In_PathBase()
        {
            // The MVC shape: the asset segment sits in PathBase (so "~/" already carries it), the tenant is
            // still a route value and has to be written out.
            var format = NewFormat("TenantA", "~abc", pathBase: "/~abc");

            Assert.AreEqual("~/TenantA/ForeignKey", format.FormatUrl("~[SlashScopeUnderBase]/ForeignKey"));
        }

        [TestMethod]
        public void UnderBase_Yields_Nothing_When_The_Tenant_Is_In_PathBase_Too()
        {
            // The Blazor shape: tenant AND asset are in PathBase, so a "~/"-caller must add nothing at all.
            var format = NewFormat("TenantA", "~abc", pathBase: "/~abc/TenantA");

            Assert.AreEqual("~/ForeignKey", format.FormatUrl("~[SlashScopeUnderBase]/ForeignKey"));
        }

        [TestMethod]
        public void Asset_Segment_Is_Available_On_Its_Own()
        {
            var format = NewFormat("TenantA", "~abc", pathBase: "/~abc");

            Assert.AreEqual("/~abc", format.FormatUrl("[SlashAssetSegment]"));
            Assert.AreEqual(string.Empty, NewFormat("TenantA", null, string.Empty).FormatUrl("[SlashAssetSegment]"));
        }

        private static UrlFormatImpl NewFormat(string tenant, string assetSegment, string pathBase)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.PathBase = new PathString(pathBase);
            return new UrlFormatImpl(new FakeHttpContextUserProvider(ctx), new FakeScope(tenant),
                new FakeAssetContext(assetSegment));
        }

        private sealed class FakeHttpContextUserProvider : IHttpContextUserProvider
        {
            public FakeHttpContextUserProvider(HttpContext context) => HttpContext = context;
            public HttpContext HttpContext { get; }
            public ClaimsPrincipal User => new(new ClaimsIdentity());
            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();
            public string RequestPath => HttpContext.Request.Path.Value;
            public IServiceProvider Services => null;
        }

        private sealed class FakeScope : PermissionScopeBase
        {
            private readonly string tenant;

            public FakeScope(string tenant)
            {
                this.tenant = tenant;
                IsScopeExplicit = true;
            }

            protected override string GetPermissionScopePrefix() => tenant;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
            {
            }
        }

        /// <summary>
        /// Nur die Kennung des Assets zaehlt hier - der Formatter setzt einen Abschnitt in die
        /// Platzhalter ein und hat mit der Bestaetigung der Argumente nichts zu tun.
        /// </summary>
        private sealed class FakeAssetContext : ISharedAssetContext
        {
            public FakeAssetContext(string segment) => Segment = segment;
            public bool HasAsset => !string.IsNullOrEmpty(Segment);
            public string AssetKey => HasAsset ? "abc" : null;
            public string AccessToken => null;
            public string Segment { get; }
            public AssetSegmentKind SegmentKind => HasAsset ? AssetSegmentKind.StoredAsset : AssetSegmentKind.None;
            public AssetInfo CurrentAsset => null;
            public string TicketTenant => null;
            public string TicketPayload => null;
            public AssetArgumentEnforcement Enforcement => AssetArgumentEnforcement.None;
            public bool Confirmed => true;
            public bool Denied => false;
            public bool MustHoldBack => false;
            public bool Require(string name, object value) => true;
            public bool Require(IDictionary<string, object> values) => true;
            public void ResetConfirmation() { }
        }
    }
}
