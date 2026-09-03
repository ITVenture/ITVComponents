using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Globalization;
using ITVComponents.WebCoreToolkit.Middleware;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the culture prefix <c>/c/{culture}</c> is recognized, stashed and moved onto
    /// <c>PathBase</c> - the move being what makes every relative link, redirect and sub-resource stay in
    /// the chosen language without knowing about it - and that it comes off IN FRONT of the asset and the
    /// tenant, so both of those keep seeing the path they have always seen.
    /// </summary>
    [TestClass]
    public class CulturePathMiddlewareTests
    {
        [TestMethod]
        public async Task Culture_Prefix_Is_Stashed_And_Stripped()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/c/de-CH/TenantA/orders");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual("de-CH", ctx.Items[Global.CulturePathCultureItemKey]);
            Assert.AreEqual("/c/de-CH", ctx.Items[Global.CulturePathPrefixItemKey]);
            Assert.AreEqual("/c/de-CH", ctx.Request.PathBase.Value);
            Assert.AreEqual("/TenantA/orders", ctx.Request.Path.Value,
                "asset and tenant have to stay in the path - that is where the middlewares behind this one look for them");
        }

        [TestMethod]
        public async Task Culture_Prefix_Leads_The_Asset_Segment()
        {
            var segment = SharedAssetPath.BuildSegment("abc-123");
            var (mw, _) = NewMiddleware();
            var ctx = NewContext($"/c/fr/{segment}/ADM/orders/42");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual("/c/fr", ctx.Request.PathBase.Value);
            Assert.AreEqual($"/{segment}/ADM/orders/42", ctx.Request.Path.Value,
                "the asset middleware runs next and has to find its segment leading the path again");
        }

        [TestMethod]
        public async Task Bare_Prefix_Without_Trailing_Slash_Yields_Root()
        {
            var (mw, _) = NewMiddleware();
            var ctx = NewContext("/c/de-CH");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual("/c/de-CH", ctx.Request.PathBase.Value);
            Assert.AreEqual("/", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Prefix_Is_Appended_To_An_Existing_PathBase()
        {
            // A virtual directory: PathBase already carries something, and the culture goes BEHIND it.
            var (mw, _) = NewMiddleware();
            var ctx = NewContext("/c/fr/orders");
            ctx.Request.PathBase = new PathString("/app");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual("/app/c/fr", ctx.Request.PathBase.Value);
            Assert.AreEqual("/orders", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Path_Without_Prefix_Is_Left_Untouched()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/TenantA/orders");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(string.Empty, ctx.Request.PathBase.Value);
            Assert.AreEqual("/TenantA/orders", ctx.Request.Path.Value);
            Assert.IsFalse(ctx.Items.ContainsKey(Global.CulturePathCultureItemKey));
        }

        [TestMethod]
        public async Task Segment_Name_Without_A_Culture_Behind_It_Stays_A_Page_Path()
        {
            // This is the collision the word segment buys: a tenant that happens to be called "c". As long
            // as what follows is not shaped like a culture, the path is none of our business.
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/c/orders/42");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(string.Empty, ctx.Request.PathBase.Value);
            Assert.AreEqual("/c/orders/42", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Unsupported_Culture_Is_Stripped_But_Not_Applied()
        {
            // A link that was sent out with a language the host has meanwhile dropped must still lead to
            // the page - just in the default language.
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/c/it/orders");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual("/c/it", ctx.Request.PathBase.Value);
            Assert.AreEqual("/orders", ctx.Request.Path.Value);
            Assert.IsFalse(ctx.Items.ContainsKey(Global.CulturePathCultureItemKey),
                "an unsupported culture must not be handed to the localization");
        }

        [TestMethod]
        public async Task Culture_Is_Resolved_To_The_Configured_Spelling_While_The_Prefix_Stays_Raw()
        {
            // The prefix is what a base href is built from, so it has to match the browser's URI character
            // for character. The culture handed to the localization is the configured spelling.
            var (mw, _) = NewMiddleware();
            var ctx = NewContext("/c/DE-ch/orders");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual("de-CH", ctx.Items[Global.CulturePathCultureItemKey]);
            Assert.AreEqual("/c/DE-ch", ctx.Items[Global.CulturePathPrefixItemKey]);
            Assert.AreEqual("/c/DE-ch", ctx.Request.PathBase.Value);
        }

        [TestMethod]
        public async Task Provider_Reads_The_Stashed_Culture()
        {
            var (mw, _) = NewMiddleware();
            var ctx = NewContext("/c/fr/orders");
            await mw.InvokeAsync(ctx);

            var result = await new CulturePathRequestCultureProvider().DetermineProviderCultureResult(ctx);

            Assert.IsNotNull(result);
            Assert.AreEqual("fr", result.Cultures[0].Value);
            Assert.AreEqual("fr", result.UICultures[0].Value);
        }

        [TestMethod]
        public async Task Provider_Stays_Silent_Without_A_Prefix()
        {
            var (mw, _) = NewMiddleware();
            var ctx = NewContext("/orders");
            await mw.InvokeAsync(ctx);

            Assert.IsNull(await new CulturePathRequestCultureProvider().DetermineProviderCultureResult(ctx),
                "no prefix means the other providers - cookie, Accept-Language - decide");
        }

        [TestMethod]
        public void Canonicalize_Removes_The_Culture_In_Front_Of_Asset_And_Tenant()
        {
            // The path filters of a shared asset match against the page path. Everything that leads it has
            // to come off, in the order it appears in the URL.
            Assert.AreEqual("/orders/42",
                SharedAssetPath.Canonicalize("/c/de-CH/~abc/TenantA/orders/42", "~abc", "TenantA"));
            Assert.AreEqual("/orders/42",
                SharedAssetPath.Canonicalize("/c/de-CH/orders/42", null, null));
            Assert.AreEqual("/c/orders/42",
                SharedAssetPath.Canonicalize("/c/orders/42", null, null),
                "without a culture behind it the segment is an ordinary path segment");
        }

        [TestMethod]
        public void Prefix_Building_And_Reading_Agree()
        {
            Assert.AreEqual("/c/de-CH", CulturePath.BuildPrefix("de-CH"));
            Assert.AreEqual(string.Empty, CulturePath.BuildPrefix(null));
            Assert.IsTrue(CulturePath.TryRead("/c/zh-Hans-CN/orders", out var culture, out var prefix));
            Assert.AreEqual("zh-Hans-CN", culture);
            Assert.AreEqual("/c/zh-Hans-CN", prefix);
            Assert.IsFalse(CulturePath.TryRead("/orders", out _, out _));
            Assert.IsFalse(CulturePath.LooksLikeCulture("orders"));
        }

        private static (CulturePathMiddleware Middleware, Ref<bool> NextRan) NewMiddleware()
        {
            var ran = new Ref<bool>();
            var localization = new RequestLocalizationOptions();
            localization.SupportedUICultures = new List<CultureInfo>
            {
                new CultureInfo("de-CH"),
                new CultureInfo("fr")
            };
            localization.RequestCultureProviders.Insert(0, new CulturePathRequestCultureProvider());

            var mw = new CulturePathMiddleware(_ =>
                {
                    ran.Value = true;
                    return Task.CompletedTask;
                },
                Microsoft.Extensions.Options.Options.Create(new CulturePathOptions()),
                Microsoft.Extensions.Options.Options.Create(localization),
                NullLogger<CulturePathMiddleware>.Instance);
            return (mw, ran);
        }

        private static DefaultHttpContext NewContext(string path)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = new PathString(path);
            return ctx;
        }

        private sealed class Ref<T>
        {
            public T Value { get; set; }
        }
    }
}
