using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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

        private static BlazorContextUserProvider NewProvider(string baseUri, string uri, TenantSource source)
        {
            var nav = new TestNavigationManager(baseUri, uri);
            var auth = new TestAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity()));
            var options = Microsoft.Extensions.Options.Options.Create(new ScopedPermissionScopeOptions
            {
                RouteOverrideParam = "tenant",
                TenantSource = source
            });
            return new BlazorContextUserProvider(auth, nav, new EmptyServiceProvider(), options);
        }

        private sealed class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager(string baseUri, string uri) => Initialize(baseUri, uri);
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
