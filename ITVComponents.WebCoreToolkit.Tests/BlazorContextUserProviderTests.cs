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

        private static IHttpContextAccessor RequestWithTenant(string tenant)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.RouteValues["tenant"] = tenant;
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
