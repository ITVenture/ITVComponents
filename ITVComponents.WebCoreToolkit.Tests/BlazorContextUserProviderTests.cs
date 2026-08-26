using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms <see cref="BlazorContextUserProvider"/> publishes the tenant override under the configured
    /// key in <see cref="BlazorContextUserProvider.RouteData"/> regardless of whether the host carries the
    /// tenant in the query (<see cref="TenantSource.Query"/>) or in the first base-URI segment
    /// (<see cref="TenantSource.PathSegment"/>) — i.e. the same engine code path resolves either source.
    /// </summary>
    [TestClass]
    public class BlazorContextUserProviderTests
    {
        [TestMethod]
        public void Query_Mode_Picks_Tenant_From_Query()
        {
            var provider = NewProvider("https://app/", "https://app/users?tenant=TenantB", TenantSource.Query);

            Assert.AreEqual("TenantB", provider.RouteData["tenant"]);
        }

        [TestMethod]
        public void Query_Mode_Ignores_Base_Segment()
        {
            var provider = NewProvider("https://app/TenantA/", "https://app/TenantA/users", TenantSource.Query);

            Assert.IsFalse(provider.RouteData.ContainsKey("tenant"));
        }

        [TestMethod]
        public void PathSegment_Mode_Picks_Tenant_From_BaseUri_First_Segment()
        {
            var provider = NewProvider("https://app/TenantA/", "https://app/TenantA/users", TenantSource.PathSegment);

            Assert.AreEqual("TenantA", provider.RouteData["tenant"]);
        }

        [TestMethod]
        public void PathSegment_Mode_With_Flat_Base_Skips_Override()
        {
            var provider = NewProvider("https://app/", "https://app/users", TenantSource.PathSegment);

            Assert.IsFalse(provider.RouteData.ContainsKey("tenant"));
        }

        [TestMethod]
        public void PathSegment_Mode_Picks_Only_First_Segment()
        {
            var provider = NewProvider("https://app/TenantA/sub/", "https://app/TenantA/sub/users", TenantSource.PathSegment);

            Assert.AreEqual("TenantA", provider.RouteData["tenant"]);
        }

        /// <summary>
        /// Endpoints that render no Blazor component — the toolkit's own <c>/{tenant}/Diagnostics</c>,
        /// <c>/ForeignKey</c> and <c>/DBW</c> are minimal-API <c>MapGet</c>s — leave the NavigationManager
        /// uninitialized. Without the fallback the tenant from the URL was lost and scope resolution served
        /// the user's default tenant instead (BUG-PRE186).
        /// </summary>
        [TestMethod]
        public void Uninitialized_Navigation_Falls_Back_To_Request_Route_Values()
        {
            var provider = NewProvider(null, null, TenantSource.PathSegment, RequestWithTenant("TenantC"));

            Assert.AreEqual("TenantC", provider.RouteData["tenant"]);
        }

        /// <summary>
        /// The window the fallback must not disturb: plugin initialization at startup has no request at all.
        /// </summary>
        [TestMethod]
        public void Uninitialized_Navigation_Without_Request_Yields_Empty()
        {
            var provider = NewProvider(null, null, TenantSource.PathSegment, new HttpContextAccessor());

            Assert.AreEqual(0, provider.RouteData.Count);
        }

        [TestMethod]
        public void Flat_Base_Falls_Back_To_Request_Route_Values()
        {
            var provider = NewProvider("https://app/", "https://app/Diagnostics/Q", TenantSource.PathSegment,
                RequestWithTenant("TenantC"));

            Assert.AreEqual("TenantC", provider.RouteData["tenant"]);
        }

        /// <summary>
        /// Inside a live circuit the base URI is the correct source — the request's route values must not
        /// overrule it (they are stale there, and the HttpContext is null anyway).
        /// </summary>
        [TestMethod]
        public void Base_Segment_Wins_Over_Request_Route_Values()
        {
            var provider = NewProvider("https://app/TenantA/", "https://app/TenantA/users", TenantSource.PathSegment,
                RequestWithTenant("TenantC"));

            Assert.AreEqual("TenantA", provider.RouteData["tenant"]);
        }

        /// <summary>
        /// The host bauart the fix from BUG-PRE186 did not reach: <c>UseTenantPathPrefix()</c> moves the
        /// tenant segment from <c>Request.Path</c> to <c>PathBase</c> before routing runs, so the route
        /// values cannot carry it — the request that renders nothing therefore still resolved to the user's
        /// default tenant (BUG-PRE187). The middleware's stash is the source that does hold it.
        /// </summary>
        [TestMethod]
        public void Uninitialized_Navigation_Falls_Back_To_Middleware_Tenant_Segment()
        {
            var provider = NewProvider(null, null, TenantSource.PathSegment, RequestBehindTenantPrefix("TenantD"));

            Assert.AreEqual("TenantD", provider.RouteData["tenant"]);
        }

        [TestMethod]
        public void Flat_Base_Falls_Back_To_Middleware_Tenant_Segment()
        {
            var provider = NewProvider("https://app/", "https://app/Diagnostics/Q", TenantSource.PathSegment,
                RequestBehindTenantPrefix("TenantD"));

            Assert.AreEqual("TenantD", provider.RouteData["tenant"]);
        }

        /// <summary>
        /// Both sources present is not a real host shape (the middleware strips what the route would match),
        /// but the order still has to be the documented one: the middleware validated its segment against the
        /// user's eligible scopes, a stray route value did not.
        /// </summary>
        [TestMethod]
        public void Middleware_Tenant_Segment_Wins_Over_Request_Route_Values()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.RouteValues["tenant"] = "TenantC";
            ctx.Items[TenantPathPrefixMiddleware.TenantSegmentItemKey] = "TenantD";
            var provider = NewProvider(null, null, TenantSource.PathSegment,
                new HttpContextAccessor { HttpContext = ctx });

            Assert.AreEqual("TenantD", provider.RouteData["tenant"]);
        }

        /// <summary>
        /// Inside a live circuit the base URI stays the source — same precedence the route-value fallback has.
        /// </summary>
        [TestMethod]
        public void Base_Segment_Wins_Over_Middleware_Tenant_Segment()
        {
            var provider = NewProvider("https://app/TenantA/", "https://app/TenantA/users", TenantSource.PathSegment,
                RequestBehindTenantPrefix("TenantD"));

            Assert.AreEqual("TenantA", provider.RouteData["tenant"]);
        }

        /// <summary>
        /// Reading the tenant must not write it into the route values of the request being served.
        /// </summary>
        [TestMethod]
        public void Reading_RouteData_Leaves_The_Requests_Route_Values_Alone()
        {
            var accessor = RequestBehindTenantPrefix("TenantD");
            var provider = NewProvider(null, null, TenantSource.PathSegment, accessor);

            _ = provider.RouteData;

            Assert.IsFalse(accessor.HttpContext.Request.RouteValues.ContainsKey("tenant"));
        }

        /// <summary>
        /// The path a caller requested is <c>PathBase + Path</c> once the middleware has moved the tenant
        /// segment over; <c>Request.Path</c> alone would name a path that resolves to a different tenant when
        /// replayed — e.g. by background work started from one of these endpoints.
        /// </summary>
        [TestMethod]
        public void Request_Path_Fallback_Carries_The_Tenant_Prefix()
        {
            var provider = NewProvider(null, null, TenantSource.PathSegment, RequestBehindTenantPrefix("TenantD"));

            Assert.AreEqual("/TenantD/diagnostics/Q", provider.RequestPath);
        }

        private static IHttpContextAccessor RequestWithTenant(string tenant)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.RouteValues["tenant"] = tenant;
            return new HttpContextAccessor { HttpContext = ctx };
        }

        /// <summary>
        /// A request as <see cref="TenantPathPrefixMiddleware"/> leaves it: segment stashed in
        /// <see cref="HttpContext.Items"/>, moved onto <c>PathBase</c>, gone from <c>Path</c> — and therefore
        /// absent from the route values, which is the whole point of the case.
        /// </summary>
        [TestMethod]
        public void Asset_Prefix_Shifts_The_Tenant_To_The_Second_Base_Segment()
        {
            // With a shared asset the base href reads /~abc/TenantA/ - the tenant is no longer the first
            // segment. Reading position 0 blindly would resolve the asset segment as a tenant name.
            var provider = NewProvider("https://app/~abc/TenantA/", "https://app/~abc/TenantA/orders",
                TenantSource.PathSegment);

            Assert.AreEqual("TenantA", provider.RouteData["tenant"]);
            Assert.AreEqual("~abc", provider.RouteData[Global.SharedAssetSegmentItemKey]);
        }

        [TestMethod]
        public void Asset_Prefix_Without_Tenant_Publishes_Only_The_Asset()
        {
            var provider = NewProvider("https://app/~abc/", "https://app/~abc/orders", TenantSource.PathSegment);

            Assert.IsFalse(provider.RouteData.ContainsKey("tenant"));
            Assert.AreEqual("~abc", provider.RouteData[Global.SharedAssetSegmentItemKey]);
        }

        [TestMethod]
        public void Without_An_Asset_Nothing_Is_Published_For_One()
        {
            var provider = NewProvider("https://app/TenantA/", "https://app/TenantA/orders", TenantSource.PathSegment);

            Assert.AreEqual("TenantA", provider.RouteData["tenant"]);
            Assert.IsFalse(provider.RouteData.ContainsKey(Global.SharedAssetSegmentItemKey));
        }

        private static IHttpContextAccessor RequestBehindTenantPrefix(string tenant)
        {
            var ctx = new DefaultHttpContext();
            ctx.Items[TenantPathPrefixMiddleware.TenantSegmentItemKey] = tenant;
            ctx.Request.PathBase = new PathString("/" + tenant);
            ctx.Request.Path = new PathString("/diagnostics/Q");
            ctx.Request.RouteValues["diagnosticsQueryName"] = "Q";
            return new HttpContextAccessor { HttpContext = ctx };
        }

        private static BlazorContextUserProvider NewProvider(string baseUri, string uri, TenantSource source,
            IHttpContextAccessor httpContextAccessor = null)
        {
            var nav = new TestNavigationManager(baseUri, uri);
            var auth = new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity()));
            var options = Microsoft.Extensions.Options.Options.Create(new ScopedPermissionScopeOptions
            {
                RouteOverrideParam = "tenant",
                TenantSource = source
            });
            return new BlazorContextUserProvider(auth, nav, new EmptyServiceProvider(), options,
                httpContextAccessor ?? new HttpContextAccessor());
        }

        private sealed class TestNavigationManager : NavigationManager
        {
            /// <summary>
            /// A null base URI leaves the manager uninitialized on purpose — that is the state a request
            /// which renders no component finds it in, and <c>Uri</c>/<c>BaseUri</c> throw there.
            /// </summary>
            public TestNavigationManager(string baseUri, string uri)
            {
                if (baseUri != null)
                {
                    Initialize(baseUri, uri);
                }
            }
        }

        private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState state;
            public TestAuthenticationStateProvider(ClaimsPrincipal user) => state = new AuthenticationState(user);
            public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(state);
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null!;
        }
    }
}
