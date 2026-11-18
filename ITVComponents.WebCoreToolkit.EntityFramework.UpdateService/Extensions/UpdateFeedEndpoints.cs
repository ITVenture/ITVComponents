using System;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.Logging;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Extensions
{
    /// <summary>
    /// Delivers the feed to the update services - anonymously: the security lies in the hash of every package, the
    /// Authenticode signature of the MSI and, if a <see cref="IFeedSigner"/> is registered, the signature of the manifest.
    /// </summary>
    public static class UpdateFeedEndpoints
    {
        /// <summary>
        /// Maps
        /// <list type="bullet">
        /// <item><c>{prefix}/{product}/{channel}.json</c> - the manifest, with <c>ETag</c>;</item>
        /// <item><c>{prefix}/{product}/{channel}.json.sig</c> - its signature (404 without <see cref="IFeedSigner"/>);</item>
        /// <item><c>{prefix}/{product}/packages/{releaseId}/{fileName}</c> - a package, with HTTP range for an interrupted
        /// download over a weak line.</item>
        /// </list>
        /// The update service is configured with <c>FeedUrl = https://host{prefix}/{product}</c>.
        /// </summary>
        public static RouteGroupBuilder MapUpdateFeed<TContext>(this IEndpointRouteBuilder endpoints, string prefix = "/updates")
            where TContext : DbContext, IUpdateServiceContext
        {
            var group = endpoints.MapGroup(prefix).AllowAnonymous();

            group.MapGet("/{product}/{channel}.json", async (string product, string channel, HttpContext http, UpdateFeedBuilder<TContext> builder, CancellationToken ct) =>
            {
                var feed = await builder.BuildAsync(product, channel, ct).ConfigureAwait(false);
                if (feed == null)
                {
                    return Results.NotFound();
                }

                http.Response.Headers.ETag = feed.EntityTag;
                http.Response.Headers.CacheControl = "no-cache";
                if (http.Request.Headers.IfNoneMatch.Any(tag => string.Equals(tag, feed.EntityTag, StringComparison.Ordinal)))
                {
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }

                return Results.Bytes(feed.Manifest, "application/json");
            });

            group.MapGet("/{product}/{channel}.json.sig", async (string product, string channel, HttpContext http, UpdateFeedBuilder<TContext> builder, CancellationToken ct) =>
            {
                var signer = http.RequestServices.GetService(typeof(IFeedSigner)) as IFeedSigner;
                if (signer == null)
                {
                    return Results.NotFound();
                }

                var feed = await builder.BuildAsync(product, channel, ct).ConfigureAwait(false);
                if (feed == null)
                {
                    return Results.NotFound();
                }

                http.Response.Headers.CacheControl = "no-cache";
                return Results.Bytes(Encoding.UTF8.GetBytes(signer.Sign(feed.Manifest)), "application/json");
            });

            group.MapGet("/{product}/packages/{releaseId:int}/{fileName}", async (string product, int releaseId, string fileName, UpdateFeedBuilder<TContext> builder, IUpdatePackageStore store, CancellationToken ct) =>
            {
                var release = await builder.FindOfferedReleaseAsync(product, releaseId, ct).ConfigureAwait(false);
                if (release == null)
                {
                    return Results.NotFound();
                }

                var content = await store.OpenAsync(release.StorageKey, ct).ConfigureAwait(false);
                if (content == null)
                {
                    LogEnvironment.LogEvent($"The package of release {release.UpdateReleaseId} ({release.Version}) is missing in the package store ({release.StorageKey}).", LogSeverity.Error);
                    return Results.NotFound();
                }

                return Results.Stream(content, "application/octet-stream", release.FileName, entityTag: new EntityTagHeaderValue("\"" + release.Sha256 + "\""), enableRangeProcessing: true);
            });

            return group;
        }
    }
}
