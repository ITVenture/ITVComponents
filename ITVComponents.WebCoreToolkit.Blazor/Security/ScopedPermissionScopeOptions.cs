using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Options for <see cref="ScopedPermissionScope"/> — the in-memory, route-driven permission-scope strategy
    /// for Blazor. Mirrors the resolution-relevant subset of the cookie-scope options without any cookie
    /// specifics (no cookie name, no encryption).
    /// </summary>
    public class ScopedPermissionScopeOptions
    {
        /// <summary>
        /// Gets or sets the route/query value name that selects the tenant for the current circuit (e.g. "tenant").
        /// The value taken from the route must be one of the user's eligible scopes — the eligibility gate is
        /// enforced server-side by the shared resolution engine. Leave null/empty to disable route-based selection.
        /// In <see cref="TenantSource.PathSegment"/> mode this is just the key under which
        /// <see cref="BlazorContextUserProvider"/> publishes the first base-URI segment to the engine.
        /// </summary>
        public string? RouteOverrideParam { get; set; }

        /// <summary>
        /// Gets or sets where the tenant value is read from. Defaults to <see cref="TenantSource.Query"/> for
        /// backward compatibility; set to <see cref="TenantSource.PathSegment"/> when the host emits a dynamic
        /// <c>&lt;base href="/{tenant}/"&gt;</c> and the URL carries the tenant as its first path segment.
        /// </summary>
        public TenantSource TenantSource { get; set; } = TenantSource.Query;

        /// <summary>
        /// Gets or sets the expression that picks a default scope when the route carries none (or an ineligible
        /// one). Defaults to the first eligible scope.
        /// </summary>
        public Func<IContextUserProvider, ScopeInfo[], string> DefaultScopeExpression { get; set; }
            = (_, eligibles) => eligibles.FirstOrDefault()?.ScopeName!;

        /// <summary>
        /// Gets or sets how long (in minutes) a resolved token is trusted before its permissions/features are
        /// re-resolved from the security repository. Defaults to 30, matching the cookie strategy.
        /// </summary>
        public int RenewalMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the name of the authentication scheme that turns a shared-asset link into a principal
        /// (the anonymous-asset scheme from <c>ITVComponents.WebCoreToolkit.Extras</c>, registered by the
        /// <c>AnonymousAssetShares</c> web part). <see cref="TenantPathPrefixMiddleware"/> asks it explicitly
        /// when a request carries an asset segment but arrives unauthenticated — otherwise the tenant segment
        /// would never be stripped for anonymous asset links and the request would 404 before any asset logic
        /// runs. Set it when the host renamed the scheme; set it to null/empty to switch the lookup off.
        /// <para>
        /// The default is the scheme name shipped by that web part. It is a plain string on purpose: this
        /// assembly does not reference <c>Extras</c>, and a host that does not use anonymous asset links must
        /// not need it either — an unregistered scheme is skipped, not an error.
        /// </para>
        /// </summary>
        public string? SharedAssetAuthenticationScheme { get; set; } = "Shared-Asset-Key";

        /// <summary>
        /// Gets the list of (case-insensitive) URL-path prefixes that <see cref="TenantUrlGuard"/> must
        /// <em>not</em> rewrite. Defaults cover the common ASP.NET Core Identity / auth endpoints so the tenant
        /// query never leaks into login/logout/account flows. Add hosts-specific routes (e.g. callback paths)
        /// as needed.
        /// </summary>
        public IList<string> AuthPathExclusions { get; } = new List<string>
        {
            "/Account/",
            "/Identity/Account/",
            "/Logout",
            "/Login",
            "/signin-",
            "/signout-"
        };
    }
}
