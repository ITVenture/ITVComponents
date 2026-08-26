using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Recognizes the shared-asset segment in the first path segment, stashes asset key and access token in
    /// <see cref="HttpContext.Items"/> and moves the segment from <c>Request.Path</c> to
    /// <c>Request.PathBase</c> - the same trick the tenant path prefix uses, one level further out.
    /// <para>
    /// Because of that move the application routes unchanged, and every relative link, redirect and
    /// sub-resource stays inside the asset context on its own: <c>PathBase</c> is what
    /// <c>IUrlHelper</c> prepends and what a Blazor <c>&lt;base href&gt;</c> is built from. Leaving the
    /// prefix means leaving that space, which is a full page load - the context can not wander off silently.
    /// </para>
    /// <para>
    /// Register it as EARLY as possible: before <c>UseStaticFiles</c> (so sub-resources under the prefix are
    /// found), before <c>UseAuthentication</c> (the <c>Shared-Asset-Key</c> scheme reads what is stashed
    /// here), before <c>UseRouting</c> (or the route would see one segment too many) and before
    /// <c>UseTenantPathPrefix</c> (so the tenant middleware sees the tenant as the first remaining segment,
    /// exactly as it does without an asset).
    /// </para>
    /// </summary>
    public sealed class SharedAssetPathMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<SharedAssetPathMiddleware> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedAssetPathMiddleware"/> class.
        /// </summary>
        /// <param name="next">the next middleware in the pipeline</param>
        /// <param name="logger">a logger for rejected segments</param>
        public SharedAssetPathMiddleware(RequestDelegate next, ILogger<SharedAssetPathMiddleware> logger)
        {
            this.next = next;
            this.logger = logger;
        }

        /// <summary>
        /// Runs the middleware for the given request.
        /// </summary>
        /// <param name="context">the current request</param>
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "/";
            var segment = SharedAssetPath.FirstSegment(path);
            if (!SharedAssetPath.IsAssetSegment(segment))
            {
                await next(context);
                return;
            }

            if (!SharedAssetPath.TryParseSegment(segment, out var assetKey, out var accessToken))
            {
                // A segment carrying the marker is never a page path, so letting it through would only
                // produce a confusing 404 further down. Answering here keeps the reason in one place - and
                // in the log, because a malformed link is exactly what somebody will report as "the link
                // does not work".
                logger.LogInformation(
                    "SharedAssetPath: segment '{Segment}' carries the asset marker but could not be decoded; responding 404 for {Path}.",
                    segment, path);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Items[Global.SharedAssetSegmentItemKey] = segment;
            context.Items[Global.SharedAssetKeyItemKey] = assetKey;
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Items[Global.SharedAssetTokenItemKey] = accessToken;
            }

            var prefix = "/" + segment;
            context.Request.PathBase = context.Request.PathBase.Add(new PathString(prefix));
            context.Request.Path = path.Length > prefix.Length
                ? new PathString(path.Substring(prefix.Length))
                : new PathString("/");

            await next(context);
        }

        /// <summary>
        /// Indicates whether this middleware has run for the given request. Used by the consumers that need
        /// the key (the authentication scheme above all) to tell "no asset in this request" from "the
        /// middleware was registered too late", which otherwise look identical and cost hours.
        /// </summary>
        /// <param name="context">the current request, may be null</param>
        /// <returns>true when an asset segment was recognized for this request</returns>
        public static bool HasRun(HttpContext context)
            => context?.Items.ContainsKey(Global.SharedAssetKeyItemKey) == true;

        /// <summary>
        /// Indicates whether the given request still carries an unprocessed asset segment - which means this
        /// middleware never ran, or ran too late in the pipeline.
        /// </summary>
        /// <param name="context">the current request, may be null</param>
        /// <returns>true when an asset segment is still sitting in the path</returns>
        public static bool HasUnprocessedSegment(HttpContext context)
        {
            if (context == null || HasRun(context))
            {
                return false;
            }

            return SharedAssetPath.IsAssetSegment(SharedAssetPath.FirstSegment(context.Request.Path.Value));
        }
    }
}
