using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        private readonly IServiceProvider services;
        private readonly ILogger<SharedAssetContext> logger;

        private bool resolved;
        private string assetKey;
        private string accessToken;
        private string segment;

        private bool infoResolved;
        private AssetInfo info;

        private readonly Dictionary<string, string> confirmed = new(StringComparer.OrdinalIgnoreCase);
        private bool denied;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedAssetContext"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">access to the live request, when there is one</param>
        /// <param name="contextUser">the host-neutral context; a Blazor circuit answers from here</param>
        /// <param name="options">the shared-asset path options</param>
        /// <param name="services">
        /// resolves the asset adapter and the argument resolvers on demand. Deliberately not injected
        /// directly: the adapter asks this context for the current segment while canonicalizing paths, so
        /// a constructor dependency would be a cycle.
        /// </param>
        /// <param name="logger">a logger for refused confirmations</param>
        public SharedAssetContext(IHttpContextAccessor httpContextAccessor, IContextUserProvider contextUser,
            IOptions<SharedAssetPathOptions> options, IServiceProvider services,
            ILogger<SharedAssetContext> logger)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.contextUser = contextUser;
            this.options = options;
            this.services = services;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public AssetArgumentEnforcement Enforcement => Info?.Enforcement ?? AssetArgumentEnforcement.None;

        /// <inheritdoc/>
        public bool Denied => denied;

        /// <inheritdoc/>
        public bool Confirmed
        {
            get
            {
                if (denied)
                {
                    return false;
                }

                var current = Info;
                if (current == null)
                {
                    return true;
                }

                // Vollstaendig heisst: JEDES Pflichtargument. Teilweise bestaetigt ist nicht bestaetigt -
                // sonst genuegte es, das harmloseste von zwei Argumenten zu belegen.
                foreach (var declaration in current.Arguments)
                {
                    if (declaration.Required && !confirmed.ContainsKey(declaration.Name))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <inheritdoc/>
        public bool MustHoldBack
        {
            get
            {
                if (!HasAsset)
                {
                    return false;
                }

                if (denied)
                {
                    return true;
                }

                return Enforcement != AssetArgumentEnforcement.None && !Confirmed;
            }
        }

        /// <inheritdoc/>
        public bool Require(string name, object value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return !MustHoldBack;
            }

            var current = Info;
            if (current == null || current.Arguments.Length == 0)
            {
                // Kein Asset oder eine Vorlage ohne Argumente: es gibt nichts zu bestaetigen. Der Aufrufer
                // soll deswegen nicht anders arbeiten muessen.
                return !denied;
            }

            var declaration = Array.Find(current.Arguments,
                n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
            if (declaration == null)
            {
                // Ein Argument, das die Freigabe nicht kennt, kann sie weder bestaetigen noch verletzen -
                // es ist schlicht nicht ihre Ebene. Vielleicht kann ein Aufloeser daraus eine machen.
                return ResolveUpwards(current, name, value);
            }

            return Check(current, declaration, value, name);
        }

        /// <inheritdoc/>
        public void ResetConfirmation()
        {
            if (Enforcement != AssetArgumentEnforcement.Strict)
            {
                // Unter Confirmed gilt eine Bestaetigung fuer den ganzen Kontext - genau darin besteht der
                // Unterschied der beiden Grade.
                return;
            }

            confirmed.Clear();
        }

        /// <inheritdoc/>
        public bool Require(IDictionary<string, object> values)
        {
            if (values == null)
            {
                return !MustHoldBack;
            }

            var result = true;
            foreach (var pair in values)
            {
                // Bewusst ohne Kurzschluss: es sollen ALLE Fehlschlaege im Log stehen, nicht nur der erste.
                result &= Require(pair.Key, pair.Value);
            }

            return result;
        }

        /// <summary>
        /// Vergleicht einen gemeldeten Wert mit dem, worauf die Freigabe zeigt, und merkt sich das
        /// Ergebnis.
        /// </summary>
        private bool Check(AssetInfo current, AssetArgumentDeclaration declaration, object value, string reportedAs)
        {
            if (!current.Values.Matches(declaration.Name, value, declaration.Type))
            {
                // Das ist der Moment, in dem ein Link auf ein fremdes Objekt zeigt. Er gehoert ins Log,
                // auch wenn der Aufrufer die Antwort selbst auswertet - sonst sieht man nur eine leere
                // Seite und weiss nicht, warum.
                logger.LogWarning(
                    "Shared asset '{AssetKey}' does not cover {Argument} = '{Value}' (reported as '{ReportedAs}'); the access is refused.",
                    assetKey, declaration.Name, value, reportedAs);
                denied = true;
                return false;
            }

            confirmed[declaration.Name] = current.Values[declaration.Name];
            return true;
        }

        /// <summary>
        /// Versucht, ein gemeldetes Unter-Objekt auf die Ebene der Freigabe zu heben - "zu dieser Position
        /// gehoert Auftrag 4711". Zustaendig ist der Host, benannt wird er an der Vorlage.
        /// </summary>
        private bool ResolveUpwards(AssetInfo current, string name, object value)
        {
            var resolvers = services?.GetService(typeof(IEnumerable<IAssetArgumentResolver>))
                as IEnumerable<IAssetArgumentResolver>;
            if (resolvers != null)
            {
                foreach (var declaration in current.Arguments)
                {
                    if (string.IsNullOrEmpty(declaration.ResolverKey))
                    {
                        continue;
                    }

                    foreach (var resolver in resolvers)
                    {
                        if (!string.Equals(resolver.Key, declaration.ResolverKey, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (resolver.TryResolve(declaration.Name, name, value, out var resolved))
                        {
                            return Check(current, declaration, resolved, name);
                        }
                    }
                }
            }

            // Nichts zu vergleichen: das ist keine Bestaetigung, aber auch keine Verletzung. Ob es
            // trotzdem nicht raus darf, entscheidet die Strenge ueber MustHoldBack.
            logger.LogDebug(
                "Nothing in shared asset '{AssetKey}' corresponds to '{Argument}'; the value was neither confirmed nor refused.",
                assetKey, name);
            return !MustHoldBack;
        }

        /// <summary>
        /// Die Angaben zur Freigabe, einmal je Scope geholt. Ohne Adapter (oder ohne Zugriff) bleibt es
        /// null - dann gibt es nichts zu vergleichen.
        /// </summary>
        private AssetInfo Info
        {
            get
            {
                if (infoResolved)
                {
                    return info;
                }

                infoResolved = true;
                if (!HasAsset)
                {
                    return null;
                }

                var adapter = services?.GetService(typeof(ISharedAssetAdapter)) as ISharedAssetAdapter;
                if (adapter == null)
                {
                    logger.LogDebug(
                        "No ISharedAssetAdapter is registered; the arguments of shared asset '{AssetKey}' can not be checked.",
                        assetKey);
                    return null;
                }

                info = adapter.GetAssetInfo(assetKey, contextUser?.User);
                return info;
            }
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
