using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the toolkit-supplied <see cref="TenantPathPrefixMiddleware"/> validates the first URL
    /// segment against the user's eligible scopes BEFORE Blazor renders — so hosts don't need to write
    /// their own validation. Covers the four request shapes that matter in practice: eligible segment
    /// passes; ineligible segment 404s; root path redirects to a default eligible scope; auth/internal
    /// paths and Query-mode hosts are not touched.
    /// </summary>
    [TestClass]
    public class TenantPathPrefixMiddlewareTests
    {
        [TestMethod]
        public async Task Eligible_Segment_Passes_And_Stashes_And_Strips()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/TenantB/users", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider("TenantA", "TenantB");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual("TenantB", ctx.Items[TenantPathPrefixMiddleware.TenantSegmentItemKey]);
            Assert.AreEqual(StatusCodes.Status200OK, ctx.Response.StatusCode);
            Assert.AreEqual("/TenantB", ctx.Request.PathBase.Value);
            Assert.AreEqual("/users", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Path_Strip_Handles_Bare_Segment_Without_Trailing_Slash()
        {
            var (mw, _) = NewMiddleware();
            var ctx = NewContext("/TenantA", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider("TenantA");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual("/TenantA", ctx.Request.PathBase.Value);
            Assert.AreEqual("/", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Path_Strip_Handles_Segment_With_Trailing_Slash()
        {
            var (mw, _) = NewMiddleware();
            var ctx = NewContext("/TenantA/", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider("TenantA");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual("/TenantA", ctx.Request.PathBase.Value);
            Assert.AreEqual("/", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Path_Strip_Preserves_Existing_PathBase()
        {
            var (mw, _) = NewMiddleware();
            var ctx = NewContext("/TenantA/users", AuthenticatedUser());
            ctx.Request.PathBase = new PathString("/app");
            ctx.RequestServices = new ServiceProvider("TenantA");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual("/app/TenantA", ctx.Request.PathBase.Value);
            Assert.AreEqual("/users", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Ineligible_Segment_Returns_404()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/EvilCorp/users", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider("TenantA", "TenantB");

            await mw.InvokeAsync(ctx);

            Assert.IsFalse(ran.Value);
            Assert.AreEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
            Assert.IsFalse(ctx.Items.ContainsKey(TenantPathPrefixMiddleware.TenantSegmentItemKey));
        }

        [TestMethod]
        public async Task User_Without_Eligible_Scopes_Returns_403()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/anything/here", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider(/* no eligible scopes */);

            await mw.InvokeAsync(ctx);

            Assert.IsFalse(ran.Value);
            Assert.AreEqual(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
        }

        [TestMethod]
        public async Task Root_Path_Redirects_To_First_Eligible_Scope()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider("TenantA", "TenantB");

            await mw.InvokeAsync(ctx);

            Assert.IsFalse(ran.Value);
            Assert.AreEqual(StatusCodes.Status302Found, ctx.Response.StatusCode);
            Assert.AreEqual("/TenantA/", ctx.Response.Headers["Location"].ToString());
        }

        [TestMethod]
        public async Task Auth_Excluded_Path_Passes_Through_Even_With_Unknown_First_Segment()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/Identity/Account/Login", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider("TenantA");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(StatusCodes.Status200OK, ctx.Response.StatusCode);
            Assert.IsFalse(ctx.Items.ContainsKey(TenantPathPrefixMiddleware.TenantSegmentItemKey));
        }

        [TestMethod]
        public async Task Blazor_Internal_Path_Passes_Through()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/_blazor", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider("TenantA");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(StatusCodes.Status200OK, ctx.Response.StatusCode);
        }

        [TestMethod]
        public async Task Anonymous_User_Passes_Through()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/TenantB/users", new ClaimsPrincipal(new ClaimsIdentity()));
            ctx.RequestServices = new ServiceProvider("TenantA", "TenantB");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(StatusCodes.Status200OK, ctx.Response.StatusCode);
            Assert.IsFalse(ctx.Items.ContainsKey(TenantPathPrefixMiddleware.TenantSegmentItemKey));
        }

        [TestMethod]
        public async Task Query_Mode_Is_NoOp()
        {
            var (mw, ran) = NewMiddleware(TenantSource.Query);
            var ctx = NewContext("/EvilCorp/users", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider("TenantA");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(StatusCodes.Status200OK, ctx.Response.StatusCode);
        }

        private static (TenantPathPrefixMiddleware Middleware, BoolBox NextRan) NewMiddleware(TenantSource source = TenantSource.PathSegment)
        {
            var ran = new BoolBox();
            RequestDelegate next = _ => { ran.Value = true; return Task.CompletedTask; };
            var opts = Microsoft.Extensions.Options.Options.Create(new ScopedPermissionScopeOptions
            {
                RouteOverrideParam = "tenant",
                TenantSource = source
            });
            return (new TenantPathPrefixMiddleware(next, opts, NullLogger<TenantPathPrefixMiddleware>.Instance), ran);
        }

        private static HttpContext NewContext(string path, ClaimsPrincipal user)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = path;
            ctx.User = user;
            return ctx;
        }

        private static ClaimsPrincipal AuthenticatedUser()
        {
            var identity = new ClaimsIdentity(new[] { new Claim(System.Security.Claims.ClaimTypes.Name, "tester") }, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        private sealed class BoolBox { public bool Value; }

        private sealed class ServiceProvider : IServiceProvider
        {
            private readonly FakeUserNameMapper mapper = new FakeUserNameMapper();
            private readonly FakeSecurityRepository repo;

            public ServiceProvider(params string[] eligibleScopes)
            {
                repo = new FakeSecurityRepository(eligibleScopes);
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(ITVComponents.WebCoreToolkit.Security.IUserNameMapper)) return mapper;
                if (serviceType == typeof(ITVComponents.WebCoreToolkit.Security.ISecurityRepository)) return repo;
                return null;
            }
        }
    }
}
