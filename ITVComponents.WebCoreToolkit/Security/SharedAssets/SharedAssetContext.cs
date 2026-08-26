using System;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Default <see cref="ISharedAssetContext"/>. Resolves the asset of the current context from - in this
    /// order - the path segment the middleware stashed, the route data a Blazor circuit publishes from its
    /// base URI, and finally the deprecated query form.
    /// <para>
    /// Resolution is memoized for the lifetime of the scope: within one request/circuit the answer can not
    /// change, and consumers ask repeatedly (claims transformation, permission checks, link building).
    /// </para>
    /// </summary>
    public class SharedAssetContext : ISharedAssetContext
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IContextUserProvider contextUser;
        private readonly IOptions<SharedAssetPathOptions> options;

        private bool resolved;
        private string assetKey;
        private string accessToken;
        private string segment;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedAssetContext"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">access to the live request, when there is one</param>
        /// <param name="contextUser">the host-neutral context; a Blazor circuit answers from here</param>
        /// <param name="options">the shared-asset path options</param>
        public SharedAssetContext(IHttpContextAccessor httpContextAccessor, IContextUserProvider contextUser,
            IOptions<SharedAssetPathOptions> options)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.contextUser = contextUser;
            this.options = options;
        }

        /// <inheritdoc/>
        public bool HasAsset
        {
            get
            {
                Resolve();
                return !string.IsNullOrEmpty(assetKey);
            }
        }

        /// <inheritdoc/>
        public string AssetKey
        {
            get
            {
                Resolve();
                return assetKey;
            }
        }

        /// <inheritdoc/>
        public string AccessToken
        {
            get
            {
                Resolve();
                return accessToken;
            }
        }

        /// <inheritdoc/>
        public string Segment
        {
            get
            {
                Resolve();
                return segment;
            }
        }

        private void Resolve()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;
            if (ResolveFromItems() || ResolveFromRouteData() || ResolveFromQuery())
            {
                return;
            }

            assetKey = null;
            accessToken = null;
            segment = null;
        }

        /// <summary>
        /// The normal path: <see cref="SharedAssetPathMiddleware"/> parsed the segment and stashed both
        /// halves before authentication ran.
        /// </summary>
        private bool ResolveFromItems()
        {
            var items = httpContextAccessor?.HttpContext?.Items;
            if (items == null || !items.TryGetValue(Global.SharedAssetKeyItemKey, out var raw) || raw is not string key
                || string.IsNullOrEmpty(key))
            {
                return false;
            }

            assetKey = key;
            accessToken = items.TryGetValue(Global.SharedAssetTokenItemKey, out var rawToken)
                ? rawToken as string
                : null;
            segment = items.TryGetValue(Global.SharedAssetSegmentItemKey, out var rawSegment)
                ? rawSegment as string
                : SharedAssetPath.BuildSegment(key, accessToken);
            return true;
        }

        /// <summary>
        /// A live Blazor circuit has no HttpContext of its own; its context provider publishes the segment
        /// of the current base URI into the route data, the same way it does with the tenant segment.
        /// </summary>
        private bool ResolveFromRouteData()
        {
            var routeData = contextUser?.RouteData;
            if (routeData == null || !routeData.TryGetValue(Global.SharedAssetSegmentItemKey, out var raw)
                || raw is not string rawSegment || !SharedAssetPath.TryParseSegment(rawSegment, out var key, out var token))
            {
                return false;
            }

            assetKey = key;
            accessToken = token;
            segment = rawSegment;
            return true;
        }

        /// <summary>
        /// The deprecated form. Kept readable so links that were already sent out keep working; see
        /// <see cref="SharedAssetPathOptions.AcceptQuerySharedAssetKey"/>.
        /// </summary>
        private bool ResolveFromQuery()
        {
            var request = httpContextAccessor?.HttpContext?.Request;
            if (request == null || !options.Value.AcceptQuerySharedAssetKey)
            {
                return false;
            }

            var query = request.Query;
            if (query == null || !query.ContainsKey(Global.FixedAssetRequestQueryParameter))
            {
                query = options.Value.AcceptRefererFallback ? request.GetRefererQuery() : null;
                if (query == null || !query.ContainsKey(Global.FixedAssetRequestQueryParameter))
                {
                    return false;
                }
            }

            var key = query[Global.FixedAssetRequestQueryParameter].ToString();
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            assetKey = key;
            accessToken = query.ContainsKey(Global.FixedAssetTokenQueryParameter)
                ? query[Global.FixedAssetTokenQueryParameter].ToString()
                : null;
            // No segment: a query-form request carries no prefix, so nothing may be prepended to links -
            // doing so would send the visitor to a path that does not exist for this host.
            segment = null;
            return true;
        }
    }
}
