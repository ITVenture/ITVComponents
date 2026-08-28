using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.Blazor.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        public async Task Ineligible_Segment_With_Referer_From_Own_Tenant_Is_Logged_As_Warning()
        {
            // The 404 that costs hours: a component emitted a root-absolute link, the browser resolved it
            // outside the base-URI space and left the circuit, so TenantUrlGuard had no chance to rewrite it.
            // The response must stay a plain 404 (no information leak), but the log has to name the cause —
            // otherwise the search starts at the host wiring, which is exactly what happened in PRE141.
            var (mw, _, log) = NewMiddlewareWithLog();
            var ctx = NewContext("/Workflow/Definitions", AuthenticatedUser());
            ctx.Request.Host = new HostString("host.example");
            ctx.Request.Headers["Referer"] = "https://host.example/TenantA/Workflow";
            ctx.RequestServices = new ServiceProvider("TenantA", "TenantB");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
            var entry = log.Entries.Single();
            Assert.AreEqual(LogLevel.Warning, entry.Level);
            StringAssert.Contains(entry.Message, "TenantA");
            StringAssert.Contains(entry.Message, "relative");
        }

        [TestMethod]
        public async Task Ineligible_Segment_Without_Usable_Referer_Stays_Information()
        {
            // A foreign or mistyped tenant segment is an expected, uninteresting event — it must not raise
            // the noise floor of the log.
            var (mw, _, log) = NewMiddlewareWithLog();
            var ctx = NewContext("/EvilCorp/users", AuthenticatedUser());
            ctx.Request.Host = new HostString("host.example");
            ctx.RequestServices = new ServiceProvider("TenantA");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
            Assert.AreEqual(LogLevel.Information, log.Entries.Single().Level);
        }

        [TestMethod]
        public async Task Ineligible_Segment_With_Foreign_Referer_Stays_Information()
        {
            // A referer from another origin, or from a tenant the user isn't eligible for, proves nothing
            // about our own link emission — no warning.
            var (mw, _, log) = NewMiddlewareWithLog();
            var ctx = NewContext("/EvilCorp/users", AuthenticatedUser());
            ctx.Request.Host = new HostString("host.example");
            ctx.Request.Headers["Referer"] = "https://elsewhere.example/TenantA/Workflow";
            ctx.RequestServices = new ServiceProvider("TenantA");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual(LogLevel.Information, log.Entries.Single().Level);
        }

        [TestMethod]
        public async Task User_Without_Eligible_Scopes_Passes_Through_Untouched()
        {
            // A tenant-less authenticated user must still reach tenant-neutral pages that only require a
            // signed-in user (Home, account, …). The middleware stays transparent: no 403, no segment strip,
            // no stashed tenant. Any page that genuinely needs an active tenant enforces that itself against
            // the null scope the engine resolves for this user.
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/anything/here", AuthenticatedUser());
            ctx.RequestServices = new ServiceProvider(/* no eligible scopes */);

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(StatusCodes.Status200OK, ctx.Response.StatusCode);
            Assert.AreEqual("/anything/here", ctx.Request.Path.Value, "path must be left untouched for a tenant-less user");
            Assert.AreEqual("", ctx.Request.PathBase.Value, "no tenant segment may be stripped to PathBase");
            Assert.IsFalse(ctx.Items.ContainsKey(TenantPathPrefixMiddleware.TenantSegmentItemKey));
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
        public async Task Anonymous_Asset_Request_Is_Authenticated_Here_And_Strips_The_Tenant()
        {
            // BUG-PRE197: the principal of an anonymous asset link is only established by the authorization
            // policy - which needs an endpoint, which needs routing, which happens after this middleware. So
            // the request arrived anonymous, kept its tenant segment, and 404'd in routing before any asset
            // logic ran. Asking the scheme here is what makes it an ordinary request again.
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/ADM/CustomerCare/Customers/3", new ClaimsPrincipal(new ClaimsIdentity()));
            ctx.Items[Global.SharedAssetKeyItemKey] = "f69ce06b";
            var assetUser = AuthenticatedUser();
            ctx.RequestServices = new ServiceProvider("ADM")
            {
                Schemes = SchemeProviderWith("Shared-Asset-Key"),
                AuthenticationService = new FakeAuthenticationService("Shared-Asset-Key", assetUser)
            };

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreSame(assetUser, ctx.User, "the asset principal must be the user of this request from here on");
            Assert.AreEqual("ADM", ctx.Items[TenantPathPrefixMiddleware.TenantSegmentItemKey]);
            Assert.AreEqual("/ADM", ctx.Request.PathBase.Value);
            Assert.AreEqual("/CustomerCare/Customers/3", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Asset_Request_Without_Registered_Scheme_Passes_Through_And_Says_So()
        {
            // A host that never registered the anonymous-asset web part is a legitimate state - links for
            // signed-in recipients work without it. It is indistinguishable from a forgotten web part for
            // whoever is holding an anonymous link, though, so it must not be silent.
            var (mw, ran, log) = NewMiddlewareWithLog();
            var ctx = NewContext("/ADM/CustomerCare/Customers/3", new ClaimsPrincipal(new ClaimsIdentity()));
            ctx.Items[Global.SharedAssetKeyItemKey] = "f69ce06b";
            ctx.RequestServices = new ServiceProvider("ADM") { Schemes = SchemeProviderWith() };

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual("/ADM/CustomerCare/Customers/3", ctx.Request.Path.Value);
            var entry = log.Entries.Single();
            Assert.AreEqual(LogLevel.Warning, entry.Level);
            StringAssert.Contains(entry.Message, "Shared-Asset-Key");
        }

        [TestMethod]
        public async Task Asset_Request_Whose_Scheme_Yields_Nothing_Passes_Through_And_Says_So()
        {
            // Expired, revoked or wrong token: the visitor sees a 404 either way, but the log has to be able
            // to tell "the link is dead" from "the wiring is wrong".
            var (mw, ran, log) = NewMiddlewareWithLog();
            var ctx = NewContext("/ADM/CustomerCare/Customers/3", new ClaimsPrincipal(new ClaimsIdentity()));
            ctx.Items[Global.SharedAssetKeyItemKey] = "f69ce06b";
            ctx.RequestServices = new ServiceProvider("ADM")
            {
                Schemes = SchemeProviderWith("Shared-Asset-Key"),
                AuthenticationService = new FakeAuthenticationService("Shared-Asset-Key", null)
            };

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual("/ADM/CustomerCare/Customers/3", ctx.Request.Path.Value);
            Assert.AreEqual(LogLevel.Warning, log.Entries.Single().Level);
        }

        [TestMethod]
        public async Task Unprocessed_Asset_Segment_Is_Reported_As_A_Pipeline_Order_Error()
        {
            // The diagnostic hole this closes: the message that names the wrong pipeline order lives in the
            // asset authentication handler - which, in exactly this constellation, is never called. So the
            // one hint for "the link does nothing" was silent whenever the link did nothing.
            // The message is written once per process, so this stays the only test that feeds an
            // unprocessed segment; a second one would find the flag already set.
            var (mw, ran, log) = NewMiddlewareWithLog();
            var ctx = NewContext("/~f69ce06b.tok/ADM/CustomerCare/Customers/3", new ClaimsPrincipal(new ClaimsIdentity()));
            ctx.RequestServices = new ServiceProvider("ADM");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            var entry = log.Entries.Single();
            Assert.AreEqual(LogLevel.Error, entry.Level);
            StringAssert.Contains(entry.Message, "UseSharedAssetPath()");
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

        private static (TenantPathPrefixMiddleware Middleware, BoolBox NextRan, CapturingLogger Log) NewMiddlewareWithLog(TenantSource source = TenantSource.PathSegment)
        {
            var ran = new BoolBox();
            RequestDelegate next = _ => { ran.Value = true; return Task.CompletedTask; };
            var opts = Microsoft.Extensions.Options.Options.Create(new ScopedPermissionScopeOptions
            {
                RouteOverrideParam = "tenant",
                TenantSource = source
            });
            var log = new CapturingLogger();
            return (new TenantPathPrefixMiddleware(next, opts, log), ran, log);
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

        /// <summary>
        /// Builds a scheme provider that knows exactly the given scheme names. The real provider is used
        /// rather than a double: whether a scheme counts as registered is precisely what is under test here.
        /// </summary>
        private static IAuthenticationSchemeProvider SchemeProviderWith(params string[] schemes)
        {
            var authOptions = new AuthenticationOptions();
            foreach (var scheme in schemes)
            {
                authOptions.AddScheme(scheme, b => b.HandlerType = typeof(FakeAuthenticationHandler));
            }

            return new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(authOptions));
        }

        private sealed class BoolBox { public bool Value; }

        /// <summary>
        /// Stands in for the asset authentication scheme: answers with the given principal for the given
        /// scheme name, and with "no result" for everything else.
        /// </summary>
        private sealed class FakeAuthenticationService : IAuthenticationService
        {
            private readonly string scheme;
            private readonly ClaimsPrincipal principal;

            public FakeAuthenticationService(string scheme, ClaimsPrincipal principal)
            {
                this.scheme = scheme;
                this.principal = principal;
            }

            public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string scheme)
            {
                if (principal == null || !string.Equals(scheme, this.scheme, StringComparison.Ordinal))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, scheme)));
            }

            public Task ChallengeAsync(HttpContext context, string scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties properties) => Task.CompletedTask;

            public Task ForbidAsync(HttpContext context, string scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties properties) => Task.CompletedTask;

            public Task SignInAsync(HttpContext context, string scheme, ClaimsPrincipal principal, Microsoft.AspNetCore.Authentication.AuthenticationProperties properties) => Task.CompletedTask;

            public Task SignOutAsync(HttpContext context, string scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties properties) => Task.CompletedTask;
        }

        /// <summary>Never invoked - it only gives the registered scheme a handler type.</summary>
        private sealed class FakeAuthenticationHandler : IAuthenticationHandler
        {
            public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;

            public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

            public Task ChallengeAsync(Microsoft.AspNetCore.Authentication.AuthenticationProperties properties) => Task.CompletedTask;

            public Task ForbidAsync(Microsoft.AspNetCore.Authentication.AuthenticationProperties properties) => Task.CompletedTask;
        }

        private sealed class CapturingLogger : ILogger<TenantPathPrefixMiddleware>
        {
            public System.Collections.Generic.List<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class ServiceProvider : IServiceProvider
        {
            private readonly FakeUserNameMapper mapper = new FakeUserNameMapper();
            private readonly FakeSecurityRepository repo;

            public ServiceProvider(params string[] eligibleScopes)
            {
                repo = new FakeSecurityRepository(eligibleScopes);
            }

            /// <summary>The scheme provider the middleware asks before it authenticates; null = none in DI.</summary>
            public IAuthenticationSchemeProvider Schemes { get; set; }

            /// <summary>Serves <c>context.AuthenticateAsync</c>; null = none in DI.</summary>
            public IAuthenticationService AuthenticationService { get; set; }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(ITVComponents.WebCoreToolkit.Security.IUserNameMapper)) return mapper;
                if (serviceType == typeof(ITVComponents.WebCoreToolkit.Security.ISecurityRepository)) return repo;
                if (serviceType == typeof(IAuthenticationSchemeProvider)) return Schemes;
                if (serviceType == typeof(IAuthenticationService)) return AuthenticationService;
                return null;
            }
        }
    }
}
