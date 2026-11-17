using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Globalization;
using ITVComponents.WebCoreToolkit.Middleware;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// The sign-in cookie belongs to the application, not to the language it is read in: under
    /// <c>/c/{culture}</c> its path leaves the prefix out, and a sign-out there hits the cookie that a sign-in
    /// without prefix wrote.
    /// </summary>
    [TestClass]
    public class CulturePathCookieTests
    {
        private const string Scheme = "Identity.Application";

        [TestMethod]
        public async Task Cookie_Path_Leaves_Out_The_Culture_Prefix()
        {
            var options = CookieOptionsFor(Scheme);
            Assert.IsInstanceOfType(options.Cookie, typeof(CulturePathCookieBuilder));

            Assert.AreEqual("/", await CookiePathUnder(options, "", "/c/de/Account/Logout"));
            Assert.AreEqual("/app", await CookiePathUnder(options, "/app", "/c/de-CH/Account/Logout"));
            Assert.AreEqual("/app", await CookiePathUnder(options, "/app", "/Account/Logout"), "without a prefix nothing changes");
        }

        [TestMethod]
        public void Settings_Of_The_Cookie_Are_Kept()
        {
            var options = CookieOptionsFor(Scheme, o =>
            {
                o.Cookie.Name = "MyAuth";
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.HttpOnly = true;
            });

            Assert.AreEqual("MyAuth", options.Cookie.Name);
            Assert.AreEqual(SameSiteMode.Strict, options.Cookie.SameSite);
            Assert.IsTrue(options.Cookie.HttpOnly);
        }

        [TestMethod]
        public async Task An_Explicit_Cookie_Path_Is_Left_Alone()
        {
            var options = CookieOptionsFor(Scheme, o => o.Cookie.Path = "/shop");

            Assert.IsNotInstanceOfType(options.Cookie, typeof(CulturePathCookieBuilder));
            Assert.AreEqual("/shop", await CookiePathUnder(options, "", "/c/de/Account/Logout"));
        }

        [TestMethod]
        public void A_Cookie_Path_That_Only_Starts_Like_The_Prefix_Is_Not_Cut()
        {
            var ctx = new DefaultHttpContext();
            ctx.Items[Global.CulturePathPrefixItemKey] = "/c/de";
            ctx.Items[Global.CulturePathOuterPathBaseItemKey] = "";

            Assert.AreEqual("/c/de-CH", CulturePathCookieBuilder.WithoutCulturePrefix(ctx, "/c/de-CH"));
            Assert.AreEqual("/~asset", CulturePathCookieBuilder.WithoutCulturePrefix(ctx, "/c/de/~asset"));
        }

        [TestMethod]
        public async Task SignOut_Under_A_Prefix_Also_Deletes_The_Cookie_A_Former_Sign_In_There_Wrote()
        {
            var options = CookieOptionsFor(Scheme);
            string[] setCookies = null;

            await RunUnderCulturePath("", "/c/de/Account/Logout", async ctx =>
            {
                var cookieOptions = options.Cookie.Build(ctx);
                var signingOut = new CookieSigningOutContext(ctx,
                    new AuthenticationScheme(Scheme, null, typeof(CookieAuthenticationHandler)),
                    options, new AuthenticationProperties(), cookieOptions);
                await options.Events.SigningOut(signingOut);
                setCookies = ctx.Response.Headers.SetCookie.ToArray();
            });

            Assert.IsNotNull(setCookies);
            Assert.IsTrue(setCookies.Any(c => c.StartsWith(options.Cookie.Name + "=") && c.Contains("path=/c/de")),
                "the cookie at the prefixed path is expired: " + string.Join(" | ", setCookies));
        }

        private static CookieAuthenticationOptions CookieOptionsFor(string scheme, System.Action<CookieAuthenticationOptions> configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCulturePath();
            services.AddAuthentication().AddCookie(scheme, o => configure?.Invoke(o));
            return services.BuildServiceProvider()
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(scheme);
        }

        private static async Task<string> CookiePathUnder(CookieAuthenticationOptions options, string pathBase, string path)
        {
            string cookiePath = null;
            await RunUnderCulturePath(pathBase, path, ctx =>
            {
                cookiePath = options.Cookie.Build(ctx).Path;
                return Task.CompletedTask;
            });

            return cookiePath;
        }

        private static async Task RunUnderCulturePath(string pathBase, string path, RequestDelegate next)
        {
            var localization = new RequestLocalizationOptions();
            localization.AddSupportedCultures("de", "de-CH");
            localization.AddSupportedUICultures("de", "de-CH");
            var mw = new CulturePathMiddleware(next,
                Microsoft.Extensions.Options.Options.Create(new CulturePathOptions()),
                Microsoft.Extensions.Options.Options.Create(localization),
                NullLogger<CulturePathMiddleware>.Instance);
            var ctx = new DefaultHttpContext();
            ctx.Request.PathBase = pathBase;
            ctx.Request.Path = path;
            await mw.InvokeAsync(ctx);
        }
    }
}
