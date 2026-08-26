using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the shared-asset segment is recognized in the first path segment, stashed and moved onto
    /// <c>PathBase</c> - the move being what makes every relative link, redirect and sub-resource stay
    /// inside the asset context without knowing about it.
    /// </summary>
    [TestClass]
    public class SharedAssetPathMiddlewareTests
    {
        [TestMethod]
        public async Task Asset_Segment_Is_Stashed_And_Stripped()
        {
            var segment = SharedAssetPath.BuildSegment("abc-123");
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext($"/{segment}/TenantA/orders");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(segment, ctx.Items[Global.SharedAssetSegmentItemKey]);
            Assert.AreEqual("abc-123", ctx.Items[Global.SharedAssetKeyItemKey]);
            Assert.IsFalse(ctx.Items.ContainsKey(Global.SharedAssetTokenItemKey));
            Assert.AreEqual($"/{segment}", ctx.Request.PathBase.Value);
            Assert.AreEqual("/TenantA/orders", ctx.Request.Path.Value,
                "the tenant has to stay in the path - that is where the route and the tenant middleware look for it");
        }

        [TestMethod]
        public async Task Access_Token_Is_Stashed_Separately()
        {
            var segment = SharedAssetPath.BuildSegment("abc-123", "T0k3n");
            var (mw, _) = NewMiddleware();
            var ctx = NewContext($"/{segment}/orders");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual("abc-123", ctx.Items[Global.SharedAssetKeyItemKey]);
            Assert.AreEqual("T0k3n", ctx.Items[Global.SharedAssetTokenItemKey]);
        }

        [TestMethod]
        public async Task Bare_Segment_Without_Trailing_Slash_Yields_Root()
        {
            var segment = SharedAssetPath.BuildSegment("abc-123");
            var (mw, _) = NewMiddleware();
            var ctx = NewContext($"/{segment}");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual($"/{segment}", ctx.Request.PathBase.Value);
            Assert.AreEqual("/", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Prefix_Is_Appended_To_An_Existing_PathBase()
        {
            // A virtual directory: PathBase already carries something, and the asset segment goes BEHIND it.
            var segment = SharedAssetPath.BuildSegment("abc-123");
            var (mw, _) = NewMiddleware();
            var ctx = NewContext($"/{segment}/orders");
            ctx.Request.PathBase = new PathString("/app");

            await mw.InvokeAsync(ctx);

            Assert.AreEqual($"/app/{segment}", ctx.Request.PathBase.Value);
            Assert.AreEqual("/orders", ctx.Request.Path.Value);
        }

        [TestMethod]
        public async Task Path_Without_Marker_Is_Left_Untouched()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/TenantA/orders");

            await mw.InvokeAsync(ctx);

            Assert.IsTrue(ran.Value);
            Assert.AreEqual(string.Empty, ctx.Request.PathBase.Value);
            Assert.AreEqual("/TenantA/orders", ctx.Request.Path.Value);
            Assert.IsFalse(ctx.Items.ContainsKey(Global.SharedAssetKeyItemKey));
        }

        [TestMethod]
        public async Task Malformed_Segment_Answers_404_Instead_Of_Passing_It_On()
        {
            var (mw, ran) = NewMiddleware();
            var ctx = NewContext("/~###/orders");

            await mw.InvokeAsync(ctx);

            Assert.IsFalse(ran.Value, "a marked but undecodable segment is not a page path");
            Assert.AreEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        }

        [TestMethod]
        public async Task Unprocessed_Segment_Is_Detectable_For_The_Wiring_Guard()
        {
            var segment = SharedAssetPath.BuildSegment("abc-123");
            var untouched = NewContext($"/{segment}/orders");

            Assert.IsTrue(SharedAssetPathMiddleware.HasUnprocessedSegment(untouched),
                "this is how a consumer notices UseSharedAssetPath() is missing or registered too late");

            var (mw, _) = NewMiddleware();
            await mw.InvokeAsync(untouched);

            Assert.IsFalse(SharedAssetPathMiddleware.HasUnprocessedSegment(untouched));
            Assert.IsTrue(SharedAssetPathMiddleware.HasRun(untouched));
        }

        private static (SharedAssetPathMiddleware Middleware, Ref<bool> NextRan) NewMiddleware()
        {
            var ran = new Ref<bool>();
            var mw = new SharedAssetPathMiddleware(_ =>
            {
                ran.Value = true;
                return Task.CompletedTask;
            }, NullLogger<SharedAssetPathMiddleware>.Instance);
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
