using System;
using ITVComponents.WebCoreToolkit.Globalization;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Routing.Impl
{
    /// <summary>
    /// <see cref="IAppLink"/> for a host that serves ordinary documents - MVC, Razor Pages, anything without
    /// a <c>&lt;base href&gt;</c>. There a relative href would resolve against the current page, so every
    /// link is root-absolute and has to carry the whole prefix itself: <c>PathBase</c> (which is where
    /// <c>UseCulturePath</c> and <c>UseSharedAssetPath</c> have moved the culture and the asset segment) plus
    /// the tenant, if and only if it is not already part of it.
    /// <para>
    /// This is also the implementation a Blazor host falls back to for its non-Blazor endpoints (Identity
    /// pages and friends), which render without a circuit and without the base href the circuit relies on.
    /// </para>
    /// </summary>
    public class HttpAppLink : IAppLink
    {
        private readonly IContextUserProvider userProvider;
        private readonly IPermissionScope permissionScope;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpAppLink"/> class.
        /// </summary>
        /// <param name="userProvider">the ambient context; carries the live request when there is one</param>
        /// <param name="permissionScope">the current scope, for the tenant half of the prefix</param>
        public HttpAppLink(IContextUserProvider userProvider, IPermissionScope permissionScope)
        {
            this.userProvider = userProvider;
            this.permissionScope = permissionScope;
        }

        /// <summary>
        /// The live request, or null outside one (background work started from a request, a circuit).
        /// </summary>
        protected HttpContext HttpContext => (userProvider as IHttpContextUserProvider)?.HttpContext;

        /// <summary>
        /// The tenant when the scope was chosen explicitly, otherwise null - the same condition under which
        /// <see cref="IUrlFormat"/> puts the scope into its placeholders.
        /// </summary>
        protected string Tenant => permissionScope != null && permissionScope.IsScopeExplicit
            ? permissionScope.PermissionPrefix
            : null;

        /// <inheritdoc/>
        public virtual string CulturePrefix
        {
            get
            {
                var http = HttpContext;
                if (http != null)
                {
                    return http.Items.TryGetValue(Global.CulturePathPrefixItemKey, out var value)
                           && value is string prefix
                        ? prefix
                        : string.Empty;
                }

                // No request at hand: the only thing left that can carry the language is the path the
                // context remembers. It has none more often than not, and an empty prefix is the right
                // answer then - not a guess.
                return CulturePath.TryRead(userProvider?.RequestPath ?? string.Empty, out _, out var read)
                    ? read
                    : string.Empty;
            }
        }

        /// <inheritdoc/>
        public virtual string Resolve(string moduleUrl)
        {
            var path = AppLinkPath.Normalize(moduleUrl);
            if (path.Length == 0)
            {
                return string.Empty;
            }

            if (path == "/")
            {
                path = string.Empty;
            }

            var pathBase = HttpContext?.Request.PathBase.Value ?? CulturePrefix;
            var prefix = pathBase?.TrimEnd('/') ?? string.Empty;
            return prefix + ScopeUnderBase(prefix) + (path.Length == 0 ? "/" : path);
        }

        /// <inheritdoc/>
        public virtual string CurrentModuleUrl
        {
            get
            {
                var http = HttpContext;
                // Request.Path is already free of everything PathBase swallowed (culture, asset, and the
                // tenant on a host that moves it there); what can still be in front of the module part is a
                // tenant that travels as a route segment. StripPrefixes takes care of both worlds.
                var path = http != null ? http.Request.Path.Value : userProvider?.RequestPath;
                return AppLinkPath.StripPrefixes(path, Tenant);
            }
        }

        /// <summary>
        /// The part of the prefix a root-absolute link still has to prepend after <paramref name="pathBase"/>.
        /// The tenant sits in <c>PathBase</c> only on hosts that strip it from the path (Blazor's
        /// path-segment mode); elsewhere it is a route value or a cookie, and only the former belongs in the
        /// link - but the cookie case is indistinguishable here and has always been prefixed, so leaving it
        /// out now would change behaviour on hosts that work today.
        /// </summary>
        /// <param name="pathBase">the current path base, without a trailing slash</param>
        /// <returns>the remaining prefix with a leading slash, or an empty string</returns>
        protected string ScopeUnderBase(string pathBase)
        {
            var tenant = Tenant;
            if (string.IsNullOrEmpty(tenant))
            {
                return string.Empty;
            }

            return !string.IsNullOrEmpty(pathBase)
                   && pathBase.EndsWith("/" + tenant, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : "/" + tenant;
        }
    }
}
