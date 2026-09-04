using System;
using System.Collections.Generic;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.Routing;
using ITVComponents.WebCoreToolkit.Routing;
using ITVComponents.WebCoreToolkit.Routing.Impl;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms that a link built from stored navigation data comes out in the form the HOST resolves
    /// correctly - and that the two forms are not interchangeable.
    /// <para>
    /// The bug behind these tests: menu urls were assembled by hand as <c>/{tenant}/{url}</c>. Root-absolute
    /// under a Blazor <c>&lt;base href="/c/de-CH/T001/"&gt;</c> means "outside the base-URI space", so the
    /// click is not intercepted, the browser reloads the document, and every prefix that lived in
    /// <c>PathBase</c> - the pinned language above all - is gone. The tenant survived only because it was the
    /// one prefix somebody had remembered to prepend.
    /// </para>
    /// </summary>
    [TestClass]
    public class AppLinkTests
    {
        [TestMethod]
        public void In_A_Circuit_The_Link_Is_Relative_So_The_Base_Href_Carries_Every_Prefix()
        {
            var link = NewCircuitLink("https://app/c/de-CH/T001/", "https://app/c/de-CH/T001/Workflow/Tasks", "T001");

            // No leading slash: the browser resolves it against the base href, which already spells out
            // language, asset and tenant. Nothing here has to know any of them.
            Assert.AreEqual("Workflow/Tasks", link.Resolve("/Workflow/Tasks"));
            Assert.AreEqual("Workflow/Tasks", link.Resolve("Workflow/Tasks"));
        }

        [TestMethod]
        public void The_Application_Root_Keeps_The_Base_Path()
        {
            // An empty href resolves to the current document rather than to the base, so the root is the one
            // entry that stays absolute - which is not an escape: the base path IS the base-URI space.
            var link = NewCircuitLink("https://app/c/de-CH/T001/", "https://app/c/de-CH/T001/Workflow", "T001");

            Assert.AreEqual("/c/de-CH/T001/", link.Resolve("/"));
        }

        [TestMethod]
        public void An_Entry_Without_A_Url_Yields_No_Link()
        {
            var link = NewCircuitLink("https://app/T001/", "https://app/T001/x", "T001");

            Assert.AreEqual(string.Empty, link.Resolve(null));
            Assert.AreEqual(string.Empty, link.Resolve("   "));
        }

        [TestMethod]
        public void Without_A_Circuit_The_Link_Is_Absolute_And_Carries_The_Whole_Prefix()
        {
            // The host's Identity pages and every classic MVC view render without a base href. A relative
            // href would resolve against the CURRENT PAGE there, so the full prefix has to be in the link.
            var http = NewHttpContext("/c/de-CH/T001", "/Workflow/Tasks");
            var link = new CircuitAppLink(new FakeHttpContextUserProvider(http), new FakeScope("T001"),
                new UninitializedNavigationManager());

            Assert.AreEqual("/c/de-CH/T001/Workflow/Tasks", link.Resolve("/Workflow/Tasks"));
        }

        [TestMethod]
        public void The_Tenant_Is_Only_Prepended_When_It_Is_Not_Already_In_The_Path_Base()
        {
            // MVC keeps the tenant in a cookie or a route value, so a root-absolute link has to name it;
            // a host that moved it into PathBase would get it twice.
            var inPathBase = new HttpAppLink(
                new FakeHttpContextUserProvider(NewHttpContext("/c/fr/T001", "/x")), new FakeScope("T001"));
            var asRouteValue = new HttpAppLink(
                new FakeHttpContextUserProvider(NewHttpContext("/c/fr", "/x")), new FakeScope("T001"));

            Assert.AreEqual("/c/fr/T001/Orders", inPathBase.Resolve("Orders"));
            Assert.AreEqual("/c/fr/T001/Orders", asRouteValue.Resolve("Orders"));
        }

        [TestMethod]
        public void The_Current_Module_Url_Is_Free_Of_Every_Prefix()
        {
            // This is what the "am I the active entry?" comparison runs on. Compared against the rendered
            // link instead, it stopped matching the moment a language was pinned - and with it went the
            // active marker and the help button that hangs off the selected entry.
            var circuit = NewCircuitLink("https://app/c/de-CH/~abc/T001/", "https://app/c/de-CH/~abc/T001/Workflow/Tasks", "T001");
            var http = new HttpAppLink(
                new FakeHttpContextUserProvider(NewHttpContext("/c/de-CH", "/T001/Workflow/Tasks")),
                new FakeScope("T001"));

            Assert.AreEqual("/Workflow/Tasks", circuit.CurrentModuleUrl);
            Assert.AreEqual("/Workflow/Tasks", http.CurrentModuleUrl);
        }

        [TestMethod]
        public void On_An_Auth_Path_The_Base_Href_Falls_Back_But_The_Language_Is_Still_Stripped()
        {
            // TenantBaseHref emits "/" on the excluded auth paths while the address can still carry a
            // language - the leftovers get the same treatment as anywhere else.
            var link = NewCircuitLink("https://app/", "https://app/c/de-CH/Identity/Account/Login", null);

            Assert.AreEqual("/Identity/Account/Login", link.CurrentModuleUrl);
        }

        [TestMethod]
        public void The_Culture_Prefix_Is_Read_From_Whichever_Source_The_Host_Has()
        {
            var circuit = NewCircuitLink("https://app/c/de-CH/T001/", "https://app/c/de-CH/T001/x", "T001");
            var http = NewHttpContext("/c/fr/T001", "/x");
            http.Items[Global.CulturePathPrefixItemKey] = "/c/fr";

            Assert.AreEqual("/c/de-CH", circuit.CulturePrefix);
            Assert.AreEqual("/c/fr",
                new HttpAppLink(new FakeHttpContextUserProvider(http), new FakeScope("T001")).CulturePrefix);
            Assert.AreEqual(string.Empty,
                NewCircuitLink("https://app/T001/", "https://app/T001/x", "T001").CulturePrefix);
        }

        [TestMethod]
        public void Stripping_Leaves_A_Tenant_Alone_That_Only_Looks_Like_One()
        {
            // "T001" is the scope; a path segment that merely STARTS with it is a different module.
            Assert.AreEqual("/T001x/Orders", AppLinkPath.StripPrefixes("/T001x/Orders", "T001"));
            Assert.AreEqual("/Orders", AppLinkPath.StripPrefixes("/T001/Orders", "T001"));
            Assert.AreEqual("/", AppLinkPath.StripPrefixes("/T001", "T001"));
        }

        private static CircuitAppLink NewCircuitLink(string baseUri, string uri, string tenant)
            => new CircuitAppLink(new FakeContextUserProvider(), new FakeScope(tenant),
                new TestNavigationManager(baseUri, uri));

        private static HttpContext NewHttpContext(string pathBase, string path)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.PathBase = new PathString(pathBase);
            ctx.Request.Path = new PathString(path);
            return ctx;
        }

        /// <summary>
        /// A navigation manager sitting at a fixed address, with the base uri the host's base href would
        /// have produced.
        /// </summary>
        private sealed class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager(string baseUri, string uri) => Initialize(baseUri, uri);

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
            }
        }

        /// <summary>
        /// What the navigation manager is outside a circuit: constructed, but never initialized - reading
        /// <see cref="NavigationManager.BaseUri"/> throws, and that is the signal this build relies on.
        /// </summary>
        private sealed class UninitializedNavigationManager : NavigationManager
        {
            protected override void NavigateToCore(string uri, bool forceLoad)
            {
            }
        }

        private sealed class FakeContextUserProvider : IContextUserProvider
        {
            public ClaimsPrincipal User => new(new ClaimsIdentity());
            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();
            public string RequestPath => null;
            public IServiceProvider Services => null;
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
                IsScopeExplicit = !string.IsNullOrEmpty(tenant);
            }

            protected override string GetPermissionScopePrefix() => tenant;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
            {
            }
        }
    }
}
